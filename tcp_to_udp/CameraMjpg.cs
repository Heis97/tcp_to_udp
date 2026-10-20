// Камера через DirectShow без OpenCV: кадры MJPG от камеры как есть — без распаковки
// и повторного сжатия. Цепочка: камера → SampleGrabber (тип MJPG) → NullRenderer.
// Кадр — готовый JPEG, его можно сразу слать по UDP. Распакованная картинка (Mat)
// делается только по запросу (DecodeLast) — для калибровки по изображению.
//
// Номера камер — в том же порядке, что у OpenCV с API.DShow (категория VideoInputDevice).

using System.Runtime.InteropServices;
using DirectShowLib;
using Emgu.CV;
using Emgu.CV.CvEnum;

namespace tcp_to_udp
{
    public sealed class CameraMjpg : ISampleGrabberCB, IDisposable
    {
        static readonly Guid CLSID_SampleGrabber = new Guid("C1F400A0-3F08-11D3-9F0B-006008039E37");
        static readonly Guid CLSID_NullRenderer = new Guid("C1F400A4-3F08-11D3-9F0B-006008039E37");

        public int Index { get; private set; }
        public string Name { get; private set; } = "";
        public int Width { get; private set; }
        public int Height { get; private set; }
        public double Fps { get; private set; }
        /// <summary>Все режимы MJPG камеры: «1920x1080 30 к/с».</summary>
        public List<string> MjpgModes { get; } = new List<string>();
        /// <summary>Были ли в первом кадре таблицы Хаффмана (DHT). Если нет — подставляем стандартные.</summary>
        public bool? FirstFrameHadDht { get; private set; }

        /// <summary>Новый кадр JPEG. Вызывается в потоке DirectShow — обработчик должен быть быстрым.</summary>
        public event Action<CameraMjpg, byte[]> FrameReady;

        public byte[] LastJpeg => _last;
        public long FrameCount => Interlocked.Read(ref _frames);

        volatile byte[] _last;
        long _frames;
        IFilterGraph2 _graph;
        IMediaControl _control;
        IBaseFilter _source, _grabberFilter, _nullRenderer;
        ISampleGrabber _grabber;
        IAMCameraControl _cameraControl;

        CameraMjpg() { }

        public static string[] ListDevices()
        {
            return DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice).Select(d => d.Name).ToArray();
        }

        /// <summary>Открыть камеру в режиме MJPG width×height и запустить. null — ошибка (текст в error).</summary>
        public static CameraMjpg Open(int index, int width, int height, int fps, out string error)
        {
            var cam = new CameraMjpg { Index = index };
            try
            {
                error = cam.Build(width, height, fps);
                if (error == null) return cam;
            }
            catch (Exception e)
            {
                error = e.GetType().Name + ": " + e.Message;
            }
            cam.Dispose();
            return null;
        }

        string Build(int width, int height, int fps)
        {
            var devs = DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice);
            if (Index < 0 || Index >= devs.Length)
                return "нет камеры " + Index + " (всего камер: " + devs.Length + ")";
            Name = devs[Index].Name;

            _graph = (IFilterGraph2)new FilterGraph();
            int hr = _graph.AddSourceFilterForMoniker(devs[Index].Mon, null, Name, out _source);
            if (hr < 0) return "камера не открылась (занята другой программой?) hr=0x" + hr.ToString("X8");

            var builder = (ICaptureGraphBuilder2)new CaptureGraphBuilder2();
            try
            {
                DsError.ThrowExceptionForHR(builder.SetFiltergraph(_graph));

                // 1) формат выхода камеры: MJPG нужного размера
                hr = builder.FindInterface(PinCategory.Capture, MediaType.Video, _source, typeof(IAMStreamConfig).GUID, out object o);
                if (hr < 0 || !(o is IAMStreamConfig cfg)) return "камера не даёт настроить формат (IAMStreamConfig)";
                string err = SelectMjpg(cfg, width, height, fps);
                if (err != null) return err;

                // 2) SampleGrabber принимает только MJPG — декодер в цепочку не попадёт
                _grabberFilter = (IBaseFilter)Activator.CreateInstance(Type.GetTypeFromCLSID(CLSID_SampleGrabber));
                _grabber = (ISampleGrabber)_grabberFilter;
                var gmt = new AMMediaType { majorType = MediaType.Video, subType = MediaSubType.MJPG };
                DsError.ThrowExceptionForHR(_grabber.SetMediaType(gmt));
                DsUtils.FreeAMMediaType(gmt);
                DsError.ThrowExceptionForHR(_grabber.SetBufferSamples(false));
                DsError.ThrowExceptionForHR(_grabber.SetOneShot(false));
                DsError.ThrowExceptionForHR(_grabber.SetCallback(this, 1));      // 1 = BufferCB
                DsError.ThrowExceptionForHR(_graph.AddFilter(_grabberFilter, "MJPG grabber"));

                _nullRenderer = (IBaseFilter)Activator.CreateInstance(Type.GetTypeFromCLSID(CLSID_NullRenderer));
                DsError.ThrowExceptionForHR(_graph.AddFilter(_nullRenderer, "Null renderer"));

                hr = builder.RenderStream(PinCategory.Capture, MediaType.Video, _source, _grabberFilter, _nullRenderer);
                if (hr < 0) return "не удалось соединить камеру с приёмником MJPG hr=0x" + hr.ToString("X8");
            }
            finally
            {
                Marshal.ReleaseComObject(builder);
            }

            // 3) проверка: что реально соединилось
            var cmt = new AMMediaType();
            if (_grabber.GetConnectedMediaType(cmt) >= 0)
            {
                bool mjpg = cmt.subType == MediaSubType.MJPG;
                DsUtils.FreeAMMediaType(cmt);
                if (!mjpg) return "соединение не в MJPG";
            }

            _cameraControl = _source as IAMCameraControl;
            _control = (IMediaControl)_graph;
            hr = _control.Run();
            if (hr < 0) return "камера не запустилась hr=0x" + hr.ToString("X8");
            return null;
        }

        string SelectMjpg(IAMStreamConfig cfg, int width, int height, int fps)
        {
            DsError.ThrowExceptionForHR(cfg.GetNumberOfCapabilities(out int count, out int size));
            IntPtr caps = Marshal.AllocCoTaskMem(Math.Max(size, 256));
            AMMediaType chosen = null;
            try
            {
                for (int i = 0; i < count; i++)
                {
                    if (cfg.GetStreamCaps(i, out AMMediaType mt, caps) < 0 || mt == null) continue;
                    bool keep = false;
                    if (mt.majorType == MediaType.Video && mt.subType == MediaSubType.MJPG && mt.formatPtr != IntPtr.Zero)
                    {
                        int w, h; long frameTime;
                        if (mt.formatType == FormatType.VideoInfo)
                        {
                            var v = (VideoInfoHeader)Marshal.PtrToStructure(mt.formatPtr, typeof(VideoInfoHeader));
                            w = v.BmiHeader.Width; h = Math.Abs(v.BmiHeader.Height); frameTime = v.AvgTimePerFrame;
                        }
                        else if (mt.formatType == FormatType.VideoInfo2)
                        {
                            var v = (VideoInfoHeader2)Marshal.PtrToStructure(mt.formatPtr, typeof(VideoInfoHeader2));
                            w = v.BmiHeader.Width; h = Math.Abs(v.BmiHeader.Height); frameTime = v.AvgTimePerFrame;
                        }
                        else { DsUtils.FreeAMMediaType(mt); continue; }

                        double modeFps = frameTime > 0 ? 1e7 / frameTime : 0;
                        MjpgModes.Add(w + "x" + h + " " + modeFps.ToString("0.#") + " к/с");
                        if (chosen == null && w == width && h == height)
                        {
                            chosen = mt; keep = true;
                            Width = w; Height = h; Fps = modeFps;
                        }
                    }
                    if (!keep) DsUtils.FreeAMMediaType(mt);
                }
                if (chosen == null)
                    return "у камеры нет режима MJPG " + width + "x" + height +
                           (MjpgModes.Count > 0 ? ". Есть: " + string.Join(", ", MjpgModes.Distinct()) : ". MJPG нет совсем");

                // желаемая частота
                long want = fps > 0 ? 10_000_000L / fps : 0;
                if (want > 0)
                {
                    if (chosen.formatType == FormatType.VideoInfo)
                    {
                        var v = (VideoInfoHeader)Marshal.PtrToStructure(chosen.formatPtr, typeof(VideoInfoHeader));
                        v.AvgTimePerFrame = want;
                        Marshal.StructureToPtr(v, chosen.formatPtr, false);
                    }
                    else
                    {
                        var v = (VideoInfoHeader2)Marshal.PtrToStructure(chosen.formatPtr, typeof(VideoInfoHeader2));
                        v.AvgTimePerFrame = want;
                        Marshal.StructureToPtr(v, chosen.formatPtr, false);
                    }
                    Fps = fps;
                }
                int hr = cfg.SetFormat(chosen);
                if (hr < 0) return "камера не приняла MJPG " + width + "x" + height + " hr=0x" + hr.ToString("X8");
                return null;
            }
            finally
            {
                if (chosen != null) DsUtils.FreeAMMediaType(chosen);
                Marshal.FreeCoTaskMem(caps);
            }
        }

        // ── кадры ──────────────────────────────────────────────────────────────
        int ISampleGrabberCB.SampleCB(double sampleTime, IMediaSample sample)
        {
            Marshal.ReleaseComObject(sample);
            return 0;
        }

        int ISampleGrabberCB.BufferCB(double sampleTime, IntPtr buffer, int length)
        {
            try
            {
                if (buffer == IntPtr.Zero || length < 4) return 0;
                var data = new byte[length];
                Marshal.Copy(buffer, data, 0, length);
                if (FirstFrameHadDht == null) FirstFrameHadDht = HasDht(data);
                data = EnsureDht(data);
                _last = data;
                Interlocked.Increment(ref _frames);
                FrameReady?.Invoke(this, data);
            }
            catch (Exception e)
            {
                Console.WriteLine("cam" + Index + ": " + e.Message);
            }
            return 0;
        }

        /// <summary>Распаковать последний кадр (для калибровки по изображению). null — кадров ещё не было.</summary>
        public Mat DecodeLast()
        {
            var j = _last;
            if (j == null) return null;
            var m = new Mat();
            CvInvoke.Imdecode(j, ImreadModes.ColorBgr, m);
            return m.IsEmpty ? null : m;
        }

        // ── экспозиция ─────────────────────────────────────────────────────────
        /// <summary>Экспозиция в единицах камеры (как CapProp.Exposure в OpenCV DShow: −13…0).</summary>
        public bool SetExposure(int value)
        {
            if (_cameraControl == null) return false;
            return _cameraControl.Set(CameraControlProperty.Exposure, value, CameraControlFlags.Manual) >= 0;
        }

        public string ExposureRange()
        {
            if (_cameraControl == null) return "нет управления камерой";
            int hr = _cameraControl.GetRange(CameraControlProperty.Exposure, out int min, out int max, out int step, out int def, out CameraControlFlags flags);
            return hr < 0 ? "недоступна" : min + "…" + max + " (шаг " + step + ", по умолчанию " + def + ")";
        }

        // ── таблицы Хаффмана ───────────────────────────────────────────────────
        // Многие USB-камеры шлют MJPG без сегмента DHT (стандарт Motion-JPEG): декодеры,
        // которые не подставляют таблицы сами, такой кадр не откроют. Вставляем стандартные
        // таблицы JPEG (Annex K) перед началом данных (SOS). 420 байт; проверено на libjpeg.
        static readonly byte[] StdDht =
        {
            0xFF, 0xC4, 0x01, 0xA2, 0x00, 0x00, 0x01, 0x05, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A,
            0x0B, 0x10, 0x00, 0x02, 0x01, 0x03, 0x03, 0x02, 0x04, 0x03, 0x05, 0x05, 0x04, 0x04, 0x00, 0x00,
            0x01, 0x7D, 0x01, 0x02, 0x03, 0x00, 0x04, 0x11, 0x05, 0x12, 0x21, 0x31, 0x41, 0x06, 0x13, 0x51,
            0x61, 0x07, 0x22, 0x71, 0x14, 0x32, 0x81, 0x91, 0xA1, 0x08, 0x23, 0x42, 0xB1, 0xC1, 0x15, 0x52,
            0xD1, 0xF0, 0x24, 0x33, 0x62, 0x72, 0x82, 0x09, 0x0A, 0x16, 0x17, 0x18, 0x19, 0x1A, 0x25, 0x26,
            0x27, 0x28, 0x29, 0x2A, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x3A, 0x43, 0x44, 0x45, 0x46, 0x47,
            0x48, 0x49, 0x4A, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58, 0x59, 0x5A, 0x63, 0x64, 0x65, 0x66, 0x67,
            0x68, 0x69, 0x6A, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78, 0x79, 0x7A, 0x83, 0x84, 0x85, 0x86, 0x87,
            0x88, 0x89, 0x8A, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98, 0x99, 0x9A, 0xA2, 0xA3, 0xA4, 0xA5,
            0xA6, 0xA7, 0xA8, 0xA9, 0xAA, 0xB2, 0xB3, 0xB4, 0xB5, 0xB6, 0xB7, 0xB8, 0xB9, 0xBA, 0xC2, 0xC3,
            0xC4, 0xC5, 0xC6, 0xC7, 0xC8, 0xC9, 0xCA, 0xD2, 0xD3, 0xD4, 0xD5, 0xD6, 0xD7, 0xD8, 0xD9, 0xDA,
            0xE1, 0xE2, 0xE3, 0xE4, 0xE5, 0xE6, 0xE7, 0xE8, 0xE9, 0xEA, 0xF1, 0xF2, 0xF3, 0xF4, 0xF5, 0xF6,
            0xF7, 0xF8, 0xF9, 0xFA, 0x01, 0x00, 0x03, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A,
            0x0B, 0x11, 0x00, 0x02, 0x01, 0x02, 0x04, 0x04, 0x03, 0x04, 0x07, 0x05, 0x04, 0x04, 0x00, 0x01,
            0x02, 0x77, 0x00, 0x01, 0x02, 0x03, 0x11, 0x04, 0x05, 0x21, 0x31, 0x06, 0x12, 0x41, 0x51, 0x07,
            0x61, 0x71, 0x13, 0x22, 0x32, 0x81, 0x08, 0x14, 0x42, 0x91, 0xA1, 0xB1, 0xC1, 0x09, 0x23, 0x33,
            0x52, 0xF0, 0x15, 0x62, 0x72, 0xD1, 0x0A, 0x16, 0x24, 0x34, 0xE1, 0x25, 0xF1, 0x17, 0x18, 0x19,
            0x1A, 0x26, 0x27, 0x28, 0x29, 0x2A, 0x35, 0x36, 0x37, 0x38, 0x39, 0x3A, 0x43, 0x44, 0x45, 0x46,
            0x47, 0x48, 0x49, 0x4A, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58, 0x59, 0x5A, 0x63, 0x64, 0x65, 0x66,
            0x67, 0x68, 0x69, 0x6A, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78, 0x79, 0x7A, 0x82, 0x83, 0x84, 0x85,
            0x86, 0x87, 0x88, 0x89, 0x8A, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98, 0x99, 0x9A, 0xA2, 0xA3,
            0xA4, 0xA5, 0xA6, 0xA7, 0xA8, 0xA9, 0xAA, 0xB2, 0xB3, 0xB4, 0xB5, 0xB6, 0xB7, 0xB8, 0xB9, 0xBA,
            0xC2, 0xC3, 0xC4, 0xC5, 0xC6, 0xC7, 0xC8, 0xC9, 0xCA, 0xD2, 0xD3, 0xD4, 0xD5, 0xD6, 0xD7, 0xD8,
            0xD9, 0xDA, 0xE2, 0xE3, 0xE4, 0xE5, 0xE6, 0xE7, 0xE8, 0xE9, 0xEA, 0xF2, 0xF3, 0xF4, 0xF5, 0xF6,
            0xF7, 0xF8, 0xF9, 0xFA
        };

        public static bool HasDht(byte[] j) => FindSos(j, out bool dht) >= 0 && dht;

        /// <summary>Позиция маркера SOS (FF DA) и были ли до него таблицы DHT (FF C4). −1 — не JPEG.</summary>
        static int FindSos(byte[] j, out bool dht)
        {
            dht = false;
            if (j.Length < 4 || j[0] != 0xFF || j[1] != 0xD8) return -1;
            int i = 2;
            while (i + 4 <= j.Length)
            {
                if (j[i] != 0xFF) return -1;
                byte m = j[i + 1];
                if (m == 0xFF) { i++; continue; }                 // байты-заполнители
                if (m == 0xDA) return i;
                if (m == 0xC4) dht = true;
                if (m == 0x01 || (m >= 0xD0 && m <= 0xD7)) { i += 2; continue; }   // маркеры без длины
                i += 2 + ((j[i + 2] << 8) | j[i + 3]);
            }
            return -1;
        }

        public static byte[] EnsureDht(byte[] j)
        {
            int sos = FindSos(j, out bool dht);
            if (sos < 0 || dht) return j;
            var r = new byte[j.Length + StdDht.Length];
            Buffer.BlockCopy(j, 0, r, 0, sos);
            Buffer.BlockCopy(StdDht, 0, r, sos, StdDht.Length);
            Buffer.BlockCopy(j, sos, r, sos + StdDht.Length, j.Length - sos);
            return r;
        }

        // ── закрытие ───────────────────────────────────────────────────────────
        public void Dispose()
        {
            try { _control?.Stop(); } catch { }
            try { _grabber?.SetCallback(null, 1); } catch { }
            FrameReady = null;
            foreach (var o in new object[] { _nullRenderer, _grabberFilter, _source, _graph })
            {
                try { if (o != null && Marshal.IsComObject(o)) Marshal.ReleaseComObject(o); } catch { }
            }
            _control = null; _grabber = null; _cameraControl = null;
            _nullRenderer = _grabberFilter = _source = null; _graph = null;
        }
    }
}
