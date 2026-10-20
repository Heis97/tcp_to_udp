// Тест камер через Media Foundation (MSMF): открытие, FullHD 30 к/с и кадр JPEG
// прямо от камеры, без распаковки и повторного сжатия.
//
// Запуск (шлюз tcp_to_udp должен быть ЗАКРЫТ — камеру держит один процесс):
//   MsmfTest.exe                 камеры 0,1,2; 1920×1080; аппаратные преобразования выключены
//   MsmfTest.exe 1               только камера 1
//   MsmfTest.exe 0,1 --hw        с аппаратными преобразованиями (как по умолчанию в OpenCV) —
//                                для сравнения времени открытия
//   MsmfTest.exe 0 --debug       подробный журнал OpenCV: почему MSMF не открыл камеру
//   MsmfTest.exe 2 640x480       другой размер кадра
//
// Результат — в консоли; кадры от камер сохраняются рядом с программой: msmf_cam<N>.jpg
// (это байты, пришедшие от камеры, без перекодирования).

using System.Diagnostics;
using System.Runtime.InteropServices;
using Emgu.CV;
using Emgu.CV.CvEnum;
/*namespace tcp_to_udp
{
    internal static class Program
    {
        const string ENV_HW = "OPENCV_VIDEOIO_MSMF_ENABLE_HW_TRANSFORMS";

        static int Main(string[] args)
        {
            // Переменные окружения OpenCV читает при загрузке своей библиотеки, поэтому программа
            // перезапускает сама себя с нужными переменными (флаг --child).
            if (!args.Contains("--child"))
                return RelaunchWithEnvironment(args);

            var cams = new List<int> { 0, 1, 2 };
            int w = 1920, h = 1080, fps = 30;
            foreach (var a in args)
            {
                if (a.StartsWith("--")) continue;
                if (a.Contains('x'))
                {
                    var p = a.Split('x');
                    w = int.Parse(p[0]); h = int.Parse(p[1]);
                }
                else
                {
                    cams = a.Split(',').Select(int.Parse).ToList();
                }
            }

            Console.WriteLine("=== Тест MSMF: камеры " + string.Join(",", cams) + ", " + w + "x" + h + " " + fps + " к/с");
            Console.WriteLine(ENV_HW + " = " + Environment.GetEnvironmentVariable(ENV_HW) +
                              (Environment.GetEnvironmentVariable(ENV_HW) == "0"
                                  ? "  (аппаратные преобразования выключены — открытие должно быть быстрым)"
                                  : "  (как по умолчанию в OpenCV)"));
            PrintVideoIoBuildInfo();

            foreach (int cam in cams)
            {
                try { TestCamera(cam, w, h, fps); }
                catch (Exception e) { Console.WriteLine("  ОШИБКА: " + e.GetType().Name + ": " + e.Message); }
            }
            Console.WriteLine();
            Console.WriteLine("Готово. Нажмите любую клавишу...");
            Console.ReadKey();
            return 0;
        }

        static int RelaunchWithEnvironment(string[] args)
        {
            var psi = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false };
            foreach (var a in args) psi.ArgumentList.Add(a);
            psi.ArgumentList.Add("--child");
            psi.Environment[ENV_HW] = args.Contains("--hw") ? "1" : "0";
            if (args.Contains("--debug"))
            {
                psi.Environment["OPENCV_LOG_LEVEL"] = "DEBUG";
                psi.Environment["OPENCV_VIDEOIO_DEBUG"] = "1";
            }
            using var p = Process.Start(psi)!;
            p.WaitForExit();
            return p.ExitCode;
        }

        static void PrintVideoIoBuildInfo()
        {
            // есть ли MSMF в сборке OpenCV, которую принёс Emgu
            try
            {
                var info = CvInvoke.BuildInformation;
                int i = info.IndexOf("Video I/O", StringComparison.Ordinal);
                if (i >= 0)
                {
                    int j = info.IndexOf("\n\n", i, StringComparison.Ordinal);
                    if (j < 0) j = Math.Min(info.Length, i + 600);
                    Console.WriteLine(info.Substring(i, j - i).TrimEnd());
                }
            }
            catch (Exception e) { Console.WriteLine("(сведения о сборке OpenCV недоступны: " + e.Message + ")"); }
        }

        static string Fourcc(VideoCapture cap)
        {
            int f = (int)cap.Get(CapProp.FourCC);
            if (f <= 0) return "?";
            var s = "" + (char)(f & 255) + (char)((f >> 8) & 255) + (char)((f >> 16) & 255) + (char)((f >> 24) & 255);
            return s.Any(c => c < 32 || c > 126) ? "0x" + f.ToString("X8") : s;
        }

        static string State(VideoCapture cap) =>
            cap.Get(CapProp.FrameWidth) + "x" + cap.Get(CapProp.FrameHeight) + " " +
            cap.Get(CapProp.Fps).ToString("0.##") + " к/с, формат " + Fourcc(cap);

        static long Step(Stopwatch sw, string what, Func<bool> action)
        {
            long t0 = sw.ElapsedMilliseconds;
            bool ok = action();
            long dt = sw.ElapsedMilliseconds - t0;
            Console.WriteLine("  " + what.PadRight(34) + (ok ? "ok" : "НЕ ПРИНЯТО").PadRight(12) + dt + " мс");
            return dt;
        }

        static void TestCamera(int cam, int w, int h, int fps)
        {
            Console.WriteLine();
            Console.WriteLine("--- Камера " + cam + " ---");
            var sw = Stopwatch.StartNew();

            long tOpen = sw.ElapsedMilliseconds;
            var cap = new VideoCapture(cam, VideoCapture.API.Msmf);
            tOpen = sw.ElapsedMilliseconds - tOpen;
            Console.WriteLine("  открытие MSMF".PadRight(36) + (cap.IsOpened ? "ok" : "НЕ ОТКРЫЛАСЬ").PadRight(12) + tOpen + " мс");
            if (!cap.IsOpened)
            {
                Console.WriteLine("  Подсказки: закрыт ли шлюз (камеру держит один процесс); существует ли камера " + cam +
                                  " в MSMF (номера могут отличаться от DirectShow); запустите с --debug — OpenCV напишет причину.");
                cap.Dispose();
                return;
            }
            Console.WriteLine("  по умолчанию: " + State(cap));

            // MSMF выбирает «родной» формат камеры, ближайший к запрошенным размеру и частоте:
            // для 1920×1080 при 30 к/с это MJPG (YUY2 в FullHD — около 5 к/с).
            // ВАЖНО: FOURCC в MSMF НЕ задаёт формат камеры (это формат вывода) — его не трогаем.
            Step(sw, "размер " + w + "x" + h, () => cap.Set(CapProp.FrameWidth, w) & cap.Set(CapProp.FrameHeight, h));
            Step(sw, "частота " + fps + " к/с", () => cap.Set(CapProp.Fps, fps));
            Console.WriteLine("  выбран формат камеры: " + State(cap));

            // Без преобразования: Read() вернёт кадр как есть — строку байт JPEG (для MJPG)
            Step(sw, "ConvertRgb = 0 (кадр как есть)", () => cap.Set(CapProp.ConvertRgb, 0));
            Console.WriteLine("  после ConvertRgb=0: " + State(cap));

            var frame = new Mat();   // не using: при зависшем Read() кадр нельзя освобождать
            long t1 = sw.ElapsedMilliseconds;
            var first = Task.Run(() => cap.Read(frame));
            if (!first.Wait(10000))
            {
                Console.WriteLine("  первый кадр не пришёл за 10 с — камера не отдаёт видео в этом режиме");
                return;   // Read() висит в другом потоке: камеру не закрываем, просто идём дальше
            }
            Console.WriteLine("  первый кадр".PadRight(36) + (first.Result && !frame.IsEmpty ? "ok" : "ПУСТО").PadRight(12) +
                              (sw.ElapsedMilliseconds - t1) + " мс");
            if (frame.IsEmpty) { cap.Dispose(); frame.Dispose(); return; }

            byte[] data = MatBytes(frame);
            bool isJpeg = frame.Rows == 1 && data.Length > 4 && data[0] == 0xFF && data[1] == 0xD8;
            Console.WriteLine("  кадр: " + frame.Rows + "x" + frame.Cols + " каналов " + frame.NumberOfChannels + ", " +
                              (data.Length / 1024) + " КБ — " +
                              (isJpeg ? "ЭТО JPEG ОТ КАМЕРЫ (без перекодирования)"
                                      : frame.Rows == 1 ? "сырые байты, но не JPEG (камера не в MJPG)"
                                                        : "распакованная картинка (ConvertRgb=0 не сработал)"));
            if (isJpeg)
            {
                using var decoded = new Mat();
                CvInvoke.Imdecode(data, ImreadModes.ColorBgr, decoded);
                Console.WriteLine("  проверка: JPEG распаковывается в " + decoded.Width + "x" + decoded.Height);
                var file = Path.Combine(AppContext.BaseDirectory, "msmf_cam" + cam + ".jpg");
                File.WriteAllBytes(file, data);
                Console.WriteLine("  сохранён как есть: " + file);
            }

            // частота за 3 с
            int n = 0; long bytes = 0;
            var t = Stopwatch.StartNew();
            while (t.ElapsedMilliseconds < 3000)
            {
                if (cap.Read(frame) && !frame.IsEmpty)
                {
                    n++;
                    bytes += (long)frame.Rows * frame.Cols * frame.ElementSize;
                }
            }
            Console.WriteLine("  ЧАСТОТА: " + (n * 1000.0 / t.ElapsedMilliseconds).ToString("0.0") + " к/с, в среднем " +
                              (bytes / Math.Max(1, n) / 1024) + " КБ/кадр");

            long tc = sw.ElapsedMilliseconds;
            cap.Dispose();
            frame.Dispose();
            Console.WriteLine("  закрытие".PadRight(36) + "".PadRight(12) + (sw.ElapsedMilliseconds - tc) + " мс");
        }

        static byte[] MatBytes(Mat m)
        {
            int len = m.Rows * m.Cols * m.ElementSize;
            var data = new byte[len];
            Marshal.Copy(m.DataPointer, data, 0, len);
            return data;
        }
    }
}*/