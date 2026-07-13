using DirectShowLib;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Formats.Asn1;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using WinRT.Interop;
using System.Diagnostics;

using Encoder = System.Drawing.Imaging.Encoder;

namespace tcp_to_udp
{
     class Udp_to_tcp
     {

        private static bool[] _isStreaming = new bool[3];
        private static VideoCapture[] _cameras = new VideoCapture[3];

        TCPclient tcp_client_main;
        int device_numb = 0;
        int string_sec_remain = 0;
        int port_main = 6000;
        string ip_main = "192.168.1.200";

        long last_ms = 0;
        bool host_send = false;

        UdpClient udp_client1;
        IPEndPoint udp_addres_1;
        Thread udp_thread = null;

        UdpClient udp_client2;
        IPEndPoint udp_addres_2;
        Thread udp_thread_2 = null;

        Thread server_thread1 = null;
        TCPserver _TCPserver1 = null;

        Thread server_thread2 = null;
        TCPserver _TCPserver2 = null;

        Thread[] cams_thr = new Thread[3];

        long last_time_1 = DateTime.Now.Ticks;
        long last_time_2 = DateTime.Now.Ticks;

        long start_time = DateTime.Now.Ticks;

        bool initing1 = false;
        bool initing2 = false;


        volatile int[] ports_cam_orig = { 5000, 5001, 5002 };//bef, aft, pound
        volatile int[] ports_cam = { 5000, 5001, 5002 };

        volatile Mat[] last_frame = new Mat[3];

        //int[] ports_cam = { 5000, 5001, 5002 };
        public void connect_udp_all()
        {
            var settins_string = load_obj<SettingsString>("settings_string.json");
            //ports_cam = settins_string.ports_cam;
            //ListAllCamerasButton_Click();
            //var set_test = new SettingsString();
            //set_test.ports_cam = ports_cam;
            //save_obj("settings_string.json", set_test);
            device_numb = settins_string.device_num;
            tcp_client_main = null;

            udp_client1 = null;
            GC.Collect();
            udp_client1 = new UdpClient(50000);
            string ip1 = "192.168.10.212";
            var port_udp1 = 52000;
            udp_addres_1 = new IPEndPoint(IPAddress.Parse(ip1), port_udp1);
            udp_client1.Connect(udp_addres_1);

            udp_client2 = null;
            GC.Collect();
            udp_client2 = new UdpClient(50001);
            string ip2 = "192.168.10.211";
            var port_udp2 = 52100;
            udp_addres_2 = new IPEndPoint(IPAddress.Parse(ip2), port_udp2);
            udp_client2.Connect(udp_addres_2);


            udp_thread = new Thread(recieve_udp_all);
            udp_thread.Start();

            _TCPserver1 = new TCPserver(62000);
            server_thread1 = new Thread(_TCPserver1.startServer);
            server_thread1.Start();

            tcp_client_main = new TCPclient();
           // Console.WriteLine("start con");
            tcp_client_main.Connection(port_main, ip_main);

            Process.Start("String_line4.exe");


            //Console.WriteLine("start con done");
            for (int i = 0; i < 3; i++) cams_thr[i] = start_cam(i, ports_cam[i]);
            while(true)
            {
                string? input = Console.ReadLine();
                if (input != null)
                {
                    _TCPserver1.pushBuffer_in(input +"\n");
                    //Console.WriteLine(input);
                }              
            } 
        }

        List<string> coms1 = new List<string>();
        List<string> coms2 = new List<string>();

        List<Command> commands1 = new List<Command>();
        List<Command> commands2 = new List<Command>();
        long command_counter1 = 0;
        long command_counter2 = 0;

        volatile int string_is_ending = 0;
        volatile int pound_is_ending = 0;

        void recieve_udp_all()
        {
            int count_ins = 0;

            long count_send1 = 0;
            long count_send2 = 0;
            bool err_con_tcp = false;

            long prev_time1 = 0;
            long prev_time2 = 0;

            while (udp_client1 != null && udp_client2 != null)
            {
                // Console.WriteLine("recive udp");
                int com_num = 0;
                bool parsed_val = false;

                //Console.Clear();
                //coms2 = new List<string>();

                if (_TCPserver1.connected)
                {
                    err_con_tcp = true;
                    //data =;
                    if (_TCPserver1.getBufferLen() > 3)
                    {
                        var data = _TCPserver1.getBuffer();
                        //Console.WriteLine("data bef: " + data);
                        data = data.Replace('\r', ' ');
                        var coms = data.Trim().Split('\n');
                        //Console.WriteLine("data: " + data);
                        foreach (var command in coms)
                        {
                            Console.WriteLine("command: " + command);
                            if (command.Length > 3)
                            {
                                if (command.Contains("M585") || command.Contains("M577") || command.Contains("M578") || command.Contains("M579") || command.Contains("M580") || command.Contains("M584") || command.Contains("M587"))
                                {
                                    Console.WriteLine("add com1: " + command);
                                    commands1.Add(new Command(command_counter1, command));
                                    command_counter1++;
                                }
                                if (command.Contains("M585") || command.Contains("M581"))
                                {
                                    Console.WriteLine("add com2: " + command);
                                    commands2.Add(new Command(command_counter2, command));
                                    command_counter2++;
                                }
                                else if (command.Contains("M590")|| command.Contains("M591"))
                                {

                                    //Console.WriteLine("add com3: " + command);
                                    var command_af = command.Replace("  ", " ");
                                    command_af = command_af.Replace("  ", " ");
                                    var vars = command_af.Trim().Split(' ');

                                    if (vars.Length > 2)
                                    {
                                        var ind_cam = Convert.ToInt32(vars[1]);
                                        var val = Convert.ToInt32(vars[2]);
                                        //Console.WriteLine(ind_cam + " " + val);
                                        if (command.Contains("M590"))
                                        {
                                            _cameras[ind_cam].Set(Emgu.CV.CvEnum.CapProp.Exposure, val);
                                        }
                                        else if(command.Contains("M591"))
                                        {
                                            ports_cam[ind_cam] = val;
                                        }
                                    }
                                }
                                else if (command.Contains("M592"))
                                {
                                    var auto_set_cams = new Thread(auto_setup_cams);
                                    auto_set_cams.Start();
                                }

                                else if (command.Contains("M593"))
                                {
                                    
                                    tcp_client_main.Connection(port_main, ip_main);
                                }

                                else if (command.Contains("M594"))
                                {
                                    var val = val_from_command(command);
                                    string_is_ending = val;
                                    tcp_client_main.send_mes(device_numb + "" + string_is_ending+""+ pound_is_ending);
                                }
                                else if (command.Contains("M595"))
                                {
                                    var val = val_from_command(command);
                                    pound_is_ending = val;
                                    tcp_client_main.send_mes(device_numb + "" + string_is_ending + "" + pound_is_ending);
                                }
                            }
                        }
                    }
                }
                else
                {
                    if(err_con_tcp)
                    {
                        Console.WriteLine("not connected interface");
                        err_con_tcp = false;
                    }
                    
                }
                if (udp_client1.Available > 0)
                {
                       

                    var res = udp_client1.Receive(ref udp_addres_1);

                    long dtime = DateTime.Now.Ticks - last_time_1;
                    last_time_1 = DateTime.Now.Ticks;

                    var cur_time_ms = DateTime.Now.Millisecond;

                    var dtime_ms = cur_time_ms - last_ms;
                    //Console.WriteLine(dtime_ms);
                    if(DateTime.Now.Millisecond > 500)
                    {
                        if(!host_send)
                        {
                            //Console.WriteLine(dtime_ms);
                            var mes_serv = device_numb + "" + string_is_ending + "" + pound_is_ending;
                            //Console.WriteLine("send server " + mes_serv);
                            tcp_client_main.send_mes(mes_serv);
                            host_send = true;

                        }

                    }
                    else
                    {
                        host_send = false;
                    }

                    if (dtime > 5000000)
                    {
                        initing1 = false;
                        Console.WriteLine("init 1 re:" + dtime+" "+ (last_time_1- start_time));
                    }


                    //Console.WriteLine(DateTime.Now.Ticks);
                    var mes = Encoding.ASCII.GetString(res) + "\n";
                    if (res != null)
                    {
                        if (_TCPserver1.connected)
                        {
                            _TCPserver1.send_mes(mes);
                            //_TCPserver1.pushBuffer(mes);
                        }
                        //Console.WriteLine(mes);
                        // Console.WriteLine("len1: " + coms1.Count);
                        var vars_from_mes = mes.Split(' ');
                        var cur_num_board = -1L;
                        //Console.WriteLine(vars_from_mes.Length);
                        if (vars_from_mes.Length >= 7)
                        {
                            try
                            {
                                var cur_st = (long)Convert.ToDouble(vars_from_mes[2]);
                                cur_num_board = (long)Convert.ToInt32(vars_from_mes[1]);
                                if (cur_st == 8)
                                {
                                    
                                    var secs_rem = (int)Convert.ToDouble(vars_from_mes[6]);
                                    var mins_rem = (int)(secs_rem / (double)60);
                                    //Console.WriteLine(mes+" "+ secs_rem+" "+ mins_rem);
                                    if (mins_rem > 0 && mins_rem < 10)
                                    {
                                        string_is_ending = mins_rem;
                                    }

                                }

                                if (cur_st == 6)
                                {

                                    var time_mesure_cur_1 = Convert.ToInt64(vars_from_mes[7]);
                                    var dt_time_mesure_1 = time_mesure_cur_1 - prev_time1;
                                    
                                    if (dt_time_mesure_1!=45)
                                    {
                                        Console.WriteLine(dt_time_mesure_1+" "+ time_mesure_cur_1+" "+ prev_time1);
                                    }
                                    //Console.Clear();

                                    prev_time1 = time_mesure_cur_1;
                                }
                            }
                            catch
                            {
                                Console.WriteLine(Console.Error);
                            }
                        }
                        if (commands1.Count > 0 && cur_num_board>=0)
                        {

                           // var cur_num_board = (long)Convert.ToInt32(vars_from_mes[1]);
                            //Console.WriteLine("send1 com: " + cur_num_board + "/" + count_send1 + " " + coms1[0]);
                            var cur_num_ins = commands1[0].num - count_send1;
                            if (!initing1)
                            {
                                initing1 = true;
                                count_send1 = commands1[0].num - cur_num_board -1 ;
                                cur_num_ins = commands1[0].num - count_send1 ;
                                Console.WriteLine("init 1 f:" + dtime + " " + (last_time_1 - start_time));
                                //Console.WriteLine("count_send1 " + count_send1 + ";cur_num_ins " + cur_num_ins + "; cur_num_board " + cur_num_board + "; commands1[0].num " + commands1[0].num);
                            }

                            if (cur_num_ins - 1 == cur_num_board)
                            {
                                var com_cur = "N" + cur_num_ins + " " + commands1[0].com;
                                var mes_out = Encoding.ASCII.GetBytes(com_cur); 
                                udp_client1.Send(mes_out, mes_out.Length);
                                
                                //Console.WriteLine("send1 com: " + cur_num_board + "/" + cur_num_ins + " " + com_cur);
                            }
                            else if (cur_num_ins == cur_num_board)
                            {
                                commands1.RemoveAt(0);

                                // count_send1++;
                                //Console.WriteLine("send1 else if: " + cur_num_board + "/" + cur_num_ins);
                            }
                            else 
                            {
                                        
                                //Console.WriteLine("send1 else: " + cur_num_board + "/" + cur_num_ins);
                            }                                
                        }                            
                    }
                }

                if (udp_client2.Available > 0)
                {


                    var res = udp_client2.Receive(ref udp_addres_2);

                    long dtime = DateTime.Now.Ticks - last_time_2;
                    last_time_2 = DateTime.Now.Ticks;
                    if (dtime > 5000000)
                    {
                        initing2 = false;
                        Console.WriteLine("init 2 re:" + dtime + " " + (last_time_2 - start_time));
                    }
                    //Console.WriteLine(DateTime.Now.Ticks);
                    var get_res = Encoding.ASCII.GetString(res);
                    if(get_res!=null)
                    {
                        var mes =get_res +"\n";

                       
                            if (_TCPserver1.connected)
                            {
                                _TCPserver1.send_mes(mes);
                                //_TCPserver1.pushBuffer(mes);
                            }
                            //Console.WriteLine(mes);
                            // Console.WriteLine("len1: " + coms1.Count);
                            var vars_from_mes = mes.Split(' ');
                        // var cur_num_board = (long)Convert.ToInt32(vars_from_mes[1]);
                        //Console.WriteLine(vars_from_mes.Length);
                            var cur_num_board = -1L;
                            if (vars_from_mes.Length >= 7)
                            {
                                try
                                {
                                    cur_num_board = (long)Convert.ToInt32(vars_from_mes[1]);
                                    if ((long)Convert.ToDouble(vars_from_mes[2]) == 0)
                                    {

                                        var time_mesure_cur_2 = (int)Convert.ToDouble(vars_from_mes[4]);
                                        var dt_time_mesure_2 = time_mesure_cur_2 - prev_time2;
                                        
                                        if (dt_time_mesure_2 != 20)
                                        {
                                             Console.WriteLine(dt_time_mesure_2 + " " + time_mesure_cur_2 + " " + prev_time2);
                                        }
                                        prev_time2 = time_mesure_cur_2;
                                    //Console.Clear();
                                    //Console.WriteLine("2: " + mes);
                                }


                                }
                                catch
                                {
                                    Console.WriteLine(Console.Error);
                                }
                            }


                            if (commands2.Count > 0 && cur_num_board >= 0)
                            {
                                //Console.WriteLine("send2 com: " + cur_num_board + "/" + count_send2 + " " + coms2[0]);
                                var cur_num_ins = commands2[0].num - count_send2;
                                if (!initing2)
                                {
                                    initing2 = true;
                                    count_send2 = commands2[0].num - cur_num_board - 1;
                                    cur_num_ins = commands2[0].num - count_send2;
                                    //Console.WriteLine("count_send1 " + count_send1 + ";cur_num_ins " + cur_num_ins + "; cur_num_board " + cur_num_board + "; commands1[0].num " + commands1[0].num);
                                    //Console.WriteLine("init 2 f:" + dtime + " " + (last_time_2 - start_time));
                                }

                                if (cur_num_ins - 1 == cur_num_board)
                                {
                                    var com_cur = "N" + cur_num_ins + " " + commands2[0].com;
                                    var mes_out = Encoding.ASCII.GetBytes(com_cur);
                                    udp_client2.Send(mes_out, mes_out.Length);

                                    //Console.WriteLine("send1 com: " + cur_num_board + "/" + cur_num_ins + " " + com_cur);
                                }
                                else if (cur_num_ins == cur_num_board)
                                {
                                    commands2.RemoveAt(0);

                                    // count_send1++;
                                    //Console.WriteLine("send1 else if: " + cur_num_board + "/" + cur_num_ins);
                                }
                                else
                                {

                                    //Console.WriteLine("send1 else: " + cur_num_board + "/" + cur_num_ins);
                                }
                            }

                        
                    }
                    
                    
                }

                
                //Console.
                // if (_TCPserver1.connected) _TCPserver1.handle();

                //if (com_num > 1) Console.WriteLine(com_num);

            }

            Console.ReadKey();
        }
        static int val_from_command(string cmd)
        {
            var command_af = cmd.Replace("  ", " ");
            var vars = command_af.Trim().Split(' ');
            var var = Convert.ToInt32(vars[1]);
            return var;
        }
        Thread start_cam(int ind,int port)
        {
            // Параметры UDP

            int clientPort = port; // Порт клиента
            //_cameras[ind] = new VideoCapture("@device:pnp:\\\\?\\usb#USB#VID_09DA&PID_2695&MI_00#9&26DAA0E0&1&0000#{65E8773D-8F56-11D0-A3B9-00A0C9223196", VideoCapture.API.DShow);
            _cameras[ind] = new VideoCapture(ind, VideoCapture.API.DShow);// 0 - индекс камеры по умолчанию  //
            _cameras[ind].Set(Emgu.CV.CvEnum.CapProp.FrameWidth,640);
            _cameras[ind].Set(Emgu.CV.CvEnum.CapProp.FrameHeight, 480);
            _cameras[ind].Set(Emgu.CV.CvEnum.CapProp.Fps, 30);

            _cameras[ind].Set(Emgu.CV.CvEnum.CapProp.Exposure, -7);



            Console.WriteLine(_cameras[ind].Get(Emgu.CV.CvEnum.CapProp.FrameWidth) + " " + _cameras[ind].Get(Emgu.CV.CvEnum.CapProp.FrameHeight) + " " + _cameras[ind].Get(Emgu.CV.CvEnum.CapProp.Fps));
            if (!_cameras[ind].IsOpened)
            {
                Console.WriteLine("Ошибка: не удалось открыть камеру!");
                return null;
            }

            Console.WriteLine("Начало видеопотока через UDP...  "+ind);
            Thread streamThread = new Thread(() => StreamVideo(ind));
            streamThread.Start();

            _isStreaming[ind] = true;
            return streamThread;
        }

        void StreamVideo(int ind)
        {
            using (UdpClient udpSender = new UdpClient())
            {
                //IPEndPoint clientEndpoint = new IPEndPoint(_TCPserver1.get_client().Address, port);
                Mat frame = new Mat();
                while (_isStreaming[ind])
                {
                    _cameras[ind].Read(frame);
                    last_frame[ind] = frame.Clone();
                    //CvInvoke.Resize(frame, frame, new Size(640, 480));
                    if (!frame.IsEmpty)
                    {

                        byte[] jpegBytes = FrameToJpegBytesEmgu(frame);
                        //Console.WriteLine($"Отправлен кадр: {jpegBytes.Length} байт");
                        if(jpegBytes.Length<65000)
                        {
                            udpSender.Send(jpegBytes, jpegBytes.Length, new IPEndPoint(_TCPserver1.get_client().Address, ports_cam[ind]));
                        }
                       
                      
                       // udpSender.Send(jpegBytes, 65536, clientEndpoint);
                        
                    }

                    Thread.Sleep(15); // ~30 FPS
                }
            }
        }
        public void auto_setup_cams()
        {
            var frms_st =(Mat[]) last_frame.Clone();
            CvInvoke.Imshow("st1", frms_st[1]);
            _TCPserver1.pushBuffer_in("M585 A R0\n");
            _TCPserver1.pushBuffer_in("M585 C R0\n");
            Thread.Sleep(500);
            _TCPserver1.pushBuffer_in("M585 A R255\n");
            Thread.Sleep(500);
            var frms_bef = (Mat[])last_frame.Clone();
            CvInvoke.Imshow("bef1", frms_bef[1]);
            Thread.Sleep(500);
            _TCPserver1.pushBuffer_in("M585 A R0\n");
            Thread.Sleep(500);
            _TCPserver1.pushBuffer_in("M585 C R255\n");
            Thread.Sleep(500);
            _TCPserver1.pushBuffer_in("M585 C R0\n");
            var frms_aft = (Mat[])last_frame.Clone();

            var delt_bef = comp_delt_mats(frms_st, frms_bef);
            var delt_aft = comp_delt_mats(frms_st, frms_aft);

            int maxIndex_bef = Array.IndexOf(delt_bef, delt_bef.Max());
            int maxIndex_aft = Array.IndexOf(delt_aft, delt_aft.Max());

            ports_cam[maxIndex_bef] = 5000;
            ports_cam[maxIndex_aft] = 5001;
            var vals_used = new bool[] { false, false, false };
            vals_used[maxIndex_bef] = true;
            vals_used[maxIndex_aft] = true;
            for(int i=0; i<vals_used.Length;i++)
            {
                if (!vals_used[i])
                {
                    ports_cam[i] = 5002;
                }
            }
        }

        public static double[] comp_delt_mats(Mat[] frames_st, Mat[] frames_past)
        {
            var delts = new double[frames_st.Length];
            for(int i=0; i<frames_st.Length;i++)
            {
                if(frames_past[i]!=null && frames_st[i]!=null)
                {
                    Mat delt_mat = frames_past[i] - frames_st[i];
                    CvInvoke.CvtColor(delt_mat, delt_mat, ColorConversion.Rgb2Gray);
                    delts[i] = delt_mat.ToImage<Gray, byte>().GetAverage().Intensity;
                }
                
            }
            return delts;
        }

        static byte[] FrameToJpegBytesEmgu(Mat frame, int quality = 70)
        {
            KeyValuePair<ImwriteFlags, int>[] encodeParams = new KeyValuePair<ImwriteFlags, int>[]
            {
            new KeyValuePair<ImwriteFlags, int>(ImwriteFlags.JpegQuality, quality)
            };
            byte[] buffer;
            using (VectorOfByte vector = new VectorOfByte())
            {
                CvInvoke.Imencode(".jpg", frame, vector, encodeParams);
                buffer = vector.ToArray();
            }
            return buffer;

        }

        static public void save_obj(string path, object obj)
        {
            JsonSerializer serializer = new JsonSerializer();
            serializer.NullValueHandling = NullValueHandling.Ignore;
            serializer.Formatting = Newtonsoft.Json.Formatting.Indented;
            using (StreamWriter sw = new StreamWriter(path))
            using (JsonWriter writer = new JsonTextWriter(sw))
            {
                serializer.Serialize(writer, obj);
            }
        }
        static public T load_obj<T>(string path, string text = null)
        {
            string jsontext = "";

            try
            {
                if (text != null)
                {
                    jsontext = text;
                }
                else
                {
                    using (StreamReader file = File.OpenText(path))
                    {
                        jsontext = file.ReadToEnd();
                    }
                    // Console.WriteLine(path + "__________________________");
                    //Console.WriteLine(jsontext);
                }
                return JsonConvert.DeserializeObject<T>(jsontext);
            }
            catch
            {
                return default(T);
            }

        }

        public List<DsDevice> GetVideoInputDevices()
        {
            // Возвращает список устройств категории VideoInputDevice
            return DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice).ToList();
        }

        // Пример использования
        private void ListAllCamerasButton_Click()
        {
            var devices = GetVideoInputDevices();
            for (int i = 0; i < devices.Count; i++)
            {
                Console.WriteLine($"Camera Index: {i}, Name: {devices[i].DevicePath}");
            }
        }

        // Как открыть камеру по имени
        private VideoCapture OpenCameraByName(string cameraName)
        {
            var devices = GetVideoInputDevices();
            for (int i = 0; i < devices.Count; i++)
            {
                if (devices[i].Name.Contains(cameraName))
                {
                    return new VideoCapture(i); // Индекс в DirectShowLib соответствует индексу в Emgu CV
                }
            }
            return null; // Устройство не найдено
        }

    }
    class Command
    {

        public long num;
        public string com;
        public Command(long num, string com)
        {
            this.com = com;
            this.num = num;
        }

    }

    class SettingsString
    {

        public int[] ports_cam;
        public int device_num;//0...9
        public SettingsString()
        {
            ports_cam = new int[3] { 5000, 5001, 5002 };
            device_num = 0;
        }

    }


}

