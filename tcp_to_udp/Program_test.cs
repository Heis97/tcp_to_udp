// Тест: кадры MJPG от камер через DirectShow как есть — без распаковки и повторного сжатия.
// Использует класс CameraMjpg (тот же файл потом переносится в шлюз).
//
// Шлюз tcp_to_udp должен быть ЗАКРЫТ — камеру держит один процесс.
//   DshowRawTest.exe                         как в шлюзе: 0 и 1 — 1920x1080, 2 — 640x480
//   DshowRawTest.exe 1:1920x1080             только камера 1
//   DshowRawTest.exe 0:1280x720 2:640x480    свои размеры

using System.Diagnostics;
using tcp_to_udp;

/*namespace tcp_to_udp
{
    internal static class Program
    {
        static int Main(string[] args)
        {
            var plan = new List<(int cam, int w, int h)>();
            foreach (var a in args)
            {
                var p = a.Split(':', 'x');
                plan.Add((int.Parse(p[0]), int.Parse(p[1]), int.Parse(p[2])));
            }
            if (plan.Count == 0) plan.AddRange(new[] { (0, 1920, 1080), (1, 1920, 1080), (2, 640, 480) });

            Console.WriteLine("=== Камеры DirectShow (номера как у OpenCV API.DShow):");
            var names = CameraMjpg.ListDevices();
            for (int i = 0; i < names.Length; i++) Console.WriteLine("  " + i + ": " + names[i]);

            // ── 1. открыть все камеры сразу (как в шлюзе) ──
            var cams = new List<CameraMjpg>();
            foreach (var (cam, w, h) in plan)
            {
                Console.WriteLine();
                Console.WriteLine("--- Камера " + cam + ": MJPG " + w + "x" + h + " 30 к/с");
                var sw = Stopwatch.StartNew();
                var c = CameraMjpg.Open(cam, w, h, 30, out string err);
                Console.WriteLine("  открытие: " + (c != null ? "ok" : "ОШИБКА: " + err) + ", " + sw.ElapsedMilliseconds + " мс");
                if (c == null) continue;
                Console.WriteLine("  имя: " + c.Name);
                Console.WriteLine("  режимы MJPG камеры: " + string.Join(", ", c.MjpgModes.Distinct()));
                Console.WriteLine("  экспозиция: " + c.ExposureRange());
                cams.Add(c);
            }
            if (cams.Count == 0) { Pause(); return 1; }

            // ── 2. первый кадр каждой камеры: JPEG ли, есть ли таблицы, распаковывается ли ──
            Thread.Sleep(1500);
            foreach (var c in cams)
            {
                Console.WriteLine();
                Console.WriteLine("--- Камера " + c.Index + ": первый кадр");
                var j = c.LastJpeg;
                if (j == null) { Console.WriteLine("  КАДРОВ НЕТ за 1,5 с"); continue; }
                bool jpeg = j.Length > 4 && j[0] == 0xFF && j[1] == 0xD8;
                Console.WriteLine("  " + (j.Length / 1024) + " КБ, " + (jpeg ? "JPEG от камеры (без перекодирования)" : "НЕ JPEG"));
                Console.WriteLine("  таблицы Хаффмана в кадре камеры: " +
                                  (c.FirstFrameHadDht == true ? "есть" : "нет — подставлены стандартные"));
                var t = Stopwatch.StartNew();
                using (var m = c.DecodeLast())
                    Console.WriteLine("  распаковка (по запросу, для калибровки): " +
                                      (m != null ? m.Width + "x" + m.Height + ", " + t.ElapsedMilliseconds + " мс" : "НЕ РАСПАКОВЫВАЕТСЯ"));
                var file = Path.Combine(AppContext.BaseDirectory, "dshow_cam" + c.Index + ".jpg");
                File.WriteAllBytes(file, j);
                Console.WriteLine("  сохранён: " + file);
            }

            // ── 3. частота всех камер одновременно, 5 с ──
            Console.WriteLine();
            Console.WriteLine("--- Частота, все камеры одновременно (5 с), процессор шлюза при этом почти не нужен:");
            var start = cams.Select(c => c.FrameCount).ToArray();
            var bytes = new long[cams.Count];
            for (int i = 0; i < cams.Count; i++)
            {
                int k = i;
                cams[i].FrameReady += (cam, data) => Interlocked.Add(ref bytes[k], data.Length);
            }
            var proc = Process.GetCurrentProcess();
            var cpu0 = proc.TotalProcessorTime;
            var sw5 = Stopwatch.StartNew();
            Thread.Sleep(5000);
            double sec = sw5.ElapsedMilliseconds / 1000.0;
            proc.Refresh();
            double cpu = (proc.TotalProcessorTime - cpu0).TotalMilliseconds / (sec * 1000) * 100 / Environment.ProcessorCount;
            for (int i = 0; i < cams.Count; i++)
            {
                long n = cams[i].FrameCount - start[i];
                Console.WriteLine("  камера " + cams[i].Index + ": " + (n / sec).ToString("0.0") + " к/с, " +
                                  (n > 0 ? bytes[i] / n / 1024 : 0) + " КБ/кадр");
            }
            Console.WriteLine("  загрузка процессора программой: " + cpu.ToString("0.0") + " %");

            // ── 4. экспозиция ──
            Console.WriteLine();
            foreach (var c in cams)
                Console.WriteLine("  камера " + c.Index + ": экспозиция −7 → " + (c.SetExposure(-7) ? "принята" : "НЕ принята"));

            // ── 5. закрытие ──
            Console.WriteLine();
            foreach (var c in cams)
            {
                var t = Stopwatch.StartNew();
                c.Dispose();
                Console.WriteLine("  камера " + c.Index + " закрыта за " + t.ElapsedMilliseconds + " мс");
            }
            Pause();
            return 0;
        }

        static void Pause()
        {
            Console.WriteLine();
            Console.WriteLine("Готово. Нажмите любую клавишу...");
            Console.ReadKey();
        }
    }
}*/

