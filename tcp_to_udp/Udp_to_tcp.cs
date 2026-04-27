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
using Encoder = System.Drawing.Imaging.Encoder;

namespace tcp_to_udp
{
     class Udp_to_tcp
    {

        private static bool[] _isStreaming = new bool[3];
        private static VideoCapture[] _cameras = new VideoCapture[3];


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


        int[] ports_cam = { 5000, 5001, 5002 };
        public void connect_udp_all()
        {
            var settins_string = load_obj<SettingsString>("settings_string.json");
            ports_cam = settins_string.ports_cam;

            //var set_test = new SettingsString();
            //set_test.ports_cam = ports_cam;
            //save_obj("settings_string.json", set_test);
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
            for (int i = 0; i < 3; i++) cams_thr[i] = start_cam(i, ports_cam[i]);

        }


        List<string> coms1 = new List<string>();
        List<string> coms2 = new List<string>();

        List<Command> commands1 = new List<Command>();
        List<Command> commands2 = new List<Command>();
        long command_counter1 = 0;
        long command_counter2 = 0;

        void recieve_udp_all()
        {
            int count_ins = 0;

            long count_send1 = 0;
            long count_send2 = 0;
            while (udp_client1 != null && udp_client2 != null)
            {
                // Console.WriteLine("recive udp");
                int com_num = 0;
                bool parsed_val = false;


                //coms2 = new List<string>();

                if (_TCPserver1.connected)
                {
                    var data = _TCPserver1.getBuffer();
                    if (data.Length > 3)
                    {
                        data = data.Replace('\r', ' ');
                        var coms = data.Trim().Split('\n');
                        //Console.WriteLine("data: " + data);
                        foreach (var command in coms)
                        {
                            Console.WriteLine("command: " + command);
                            if (command.Length > 3)
                            {
                                if (command.Contains("M577") || command.Contains("M578") || command.Contains("M579") || command.Contains("M580") || command.Contains("M584") || command.Contains("M587"))
                                {
                                    Console.WriteLine("add com1: " + command);
                                    commands1.Add(new Command(command_counter1, command));
                                    command_counter1++;
                                }
                                else if (command.Contains("M585") || command.Contains("M581"))
                                {
                                    Console.WriteLine("add com2: " + command);
                                    commands2.Add(new Command(command_counter2, command));
                                    command_counter2++;
                                }
                                else if (command.Contains("M590"))
                                {

                                    //Console.WriteLine("add com3: " + command);
                                    var command_af = command.Replace("  ", " ");
                                    command_af = command_af.Replace("  ", " ");
                                    var vars = command_af.Trim().Split(' ');

                                    if (vars.Length > 2)
                                    {
                                        var ind_cam = Convert.ToInt32(vars[1]);
                                        var exp_cam = Convert.ToInt32(vars[2]);
                                        Console.WriteLine(ind_cam + " " + exp_cam);
                                        _cameras[ind_cam].Set(Emgu.CV.CvEnum.CapProp.Exposure, exp_cam);
                                        //coms2.Add(command);
                                    }

                                }
                            }
                                

                        }
                    }
                }
                if (udp_client1.Available > 0)
                {
                       

                    var res = udp_client1.Receive(ref udp_addres_1);

                    long dtime = DateTime.Now.Ticks - last_time_1;
                    last_time_1 = DateTime.Now.Ticks;
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
                            _TCPserver1.pushBuffer(mes);
                        }
                        //Console.WriteLine(mes);
                        // Console.WriteLine("len1: " + coms1.Count);
                        if (commands1.Count > 0)
                        {
                            var cur_num_board = (long)Convert.ToInt32(mes.Split(' ')[1]);
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
                                udp_client1.SendAsync(mes_out, mes_out.Length);
                                        
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
                    var mes = Encoding.ASCII.GetString(res) + "\n";
                    if (res != null)
                    {
                        if (_TCPserver1.connected)
                        {
                            _TCPserver1.pushBuffer(mes);
                        }
                        //Console.WriteLine(mes);
                        // Console.WriteLine("len1: " + coms1.Count);
                        if (commands2.Count > 0)
                        {
                            var cur_num_board = (long)Convert.ToInt32(mes.Split(' ')[1]);
                            //Console.WriteLine("send1 com: " + cur_num_board + "/" + count_send1 + " " + coms1[0]);
                            var cur_num_ins = commands2[0].num - count_send2;
                            if (!initing2)
                            {
                                initing2 = true;
                                count_send2 = commands2[0].num - cur_num_board - 1;
                                cur_num_ins = commands2[0].num - count_send2;
                                //Console.WriteLine("count_send1 " + count_send1 + ";cur_num_ins " + cur_num_ins + "; cur_num_board " + cur_num_board + "; commands1[0].num " + commands1[0].num);
                                Console.WriteLine("init 2 f:" + dtime + " " + (last_time_2 - start_time));
                            }

                            if (cur_num_ins - 1 == cur_num_board)
                            {
                                var com_cur = "N" + cur_num_ins + " " + commands2[0].com;
                                var mes_out = Encoding.ASCII.GetBytes(com_cur);
                                udp_client2.SendAsync(mes_out, mes_out.Length);

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



                // if (_TCPserver1.connected) _TCPserver1.handle();

                //if (com_num > 1) Console.WriteLine(com_num);

            }


        }

        Thread start_cam(int ind,int port)
        {
            // Параметры UDP

            int clientPort = port; // Порт клиента
            _cameras[ind] = new VideoCapture(ind, VideoCapture.API.DShow); // 0 - индекс камеры по умолчанию  //
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
            Thread streamThread = new Thread(() => StreamVideo(port,ind));
            streamThread.Start();

            _isStreaming[ind] = true;
            return streamThread;
        }

        void StreamVideo(int port,int ind)
        {
            using (UdpClient udpSender = new UdpClient())
            {
                //IPEndPoint clientEndpoint = new IPEndPoint(_TCPserver1.get_client().Address, port);
                Mat frame = new Mat();
                while (_isStreaming[ind])
                {
                    _cameras[ind].Read(frame);
                    //CvInvoke.Resize(frame, frame, new Size(640, 480));
                    if (!frame.IsEmpty)
                    {
                        byte[] jpegBytes = FrameToJpegBytesEmgu(frame);
                        //Console.WriteLine($"Отправлен кадр: {jpegBytes.Length} байт");
                        if(jpegBytes.Length<65000)
                        {
                            udpSender.Send(jpegBytes, jpegBytes.Length, new IPEndPoint(_TCPserver1.get_client().Address, port));
                        }
                       
                      
                       // udpSender.Send(jpegBytes, 65536, clientEndpoint);
                        
                    }

                    Thread.Sleep(15); // ~30 FPS
                }
            }
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

        public SettingsString()
        {
           // ports_cam = new int[3] { 5000, 5001, 5002 };
        }

    }


}

