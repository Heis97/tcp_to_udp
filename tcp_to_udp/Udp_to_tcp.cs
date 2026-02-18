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


        int[] ports_cam = { 5000, 5001, 5002 };
        public void connect_udp_all()
        {
            var settins_string = load_obj<SettingsString>("settings_string.json");
            ports_cam = settins_string.ports_cam;
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


        void recieve_udp_all()
        {
            int count_ins = 0;

            int count_send1 = 0;
            int count_send2 = 0;
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
                                if (command.Contains("M577") || command.Contains("M578") || command.Contains("M579") || command.Contains("M580") || command.Contains("M584") || command.Contains("M587"))
                                {
                                    Console.WriteLine("add com1: "+ command);
                                    coms1.Add(command);
                                }
                                else if (command.Contains("M585") || command.Contains("M581"))
                                {
                                    Console.WriteLine("add com2: " + command);
                                    coms2.Add(command);
                                }
                                else if (command.Contains("M590"))
                                {
 
                                     //Console.WriteLine("add com3: " + command);
                                    var command_af = command.Replace("  ", " ");
                                    command_af = command_af.Replace("  ", " ");
                                    var vars = command_af.Trim().Split(' ');

                                    if(vars.Length > 2)
                                    {
                                        var ind_cam = Convert.ToInt32(vars[1]);
                                        var exp_cam = Convert.ToInt32(vars[2]);
                                        Console.WriteLine(ind_cam+" "+exp_cam);
                                        _cameras[ind_cam].Set(Emgu.CV.CvEnum.CapProp.Exposure, exp_cam);
                                        //coms2.Add(command);
                                    }
                                    
                                }

                        }
                    }

                    while (udp_client1.Available > 0)
                    {
                        var res = udp_client1.Receive(ref udp_addres_1);



                        var mes = Encoding.ASCII.GetString(res) + "\n";
                        if (res != null)
                        {
                            // Console.WriteLine("udp res: " + mes);
                            //label_udp_state_1.BeginInvoke((MethodInvoker)(() => label_udp_state_1.Text = mes));
                            if (_TCPserver1.connected)
                            {
                                _TCPserver1.pushBuffer(mes);

                                //Console.WriteLine(mes);
                                // Console.WriteLine("len1: " + coms1.Count);
                                if (coms1.Count > 0)
                                {
                                    var cur_num_board = Convert.ToInt32(mes.Split(' ')[1]);
                                    Console.WriteLine("send1 com: " + cur_num_board + "/" + count_send1 + " " + coms1[0]);
                                    if (count_send1 - 1 == cur_num_board)
                                    {
                                        var mes_out = Encoding.ASCII.GetBytes(coms1[0]); count_ins++;
                                        udp_client1.SendAsync(mes_out, mes_out.Length);
                                        //Console.WriteLine("send1 com: " + cur_num_board + "/" + count_send1 + " " + coms1[0]);
                                    }
                                    else if (cur_num_board == count_send1)
                                    {
                                        coms1.RemoveAt(0);
                                        com_num++;
                                        count_send1++;
                                        //Console.WriteLine("send1 plus: " + cur_num_board + "/" + count_send1);
                                    }
                                    else
                                    {
                                        count_send1 = cur_num_board;
                                        //Console.WriteLine("send1 else: " + cur_num_board + "/" + count_send1);
                                    }                                
                                }
                            }
                        }
                    }

                    while (udp_client2.Available > 0)
                    {


                        var res = udp_client2.Receive(ref udp_addres_2);

                        var mes = Encoding.ASCII.GetString(res) + "\n";


                        if (res != null)
                        {
                            // Console.WriteLine("udp res: " + mes);
                            // label_udp_state_2.BeginInvoke((MethodInvoker)(() => label_udp_state_2.Text = mes));
                            if (_TCPserver1.connected)
                            {
                                _TCPserver1.pushBuffer(mes);
                                
                                if(coms2.Count>0)
                                {

                                    var cur_num_board = Convert.ToInt32(mes.Split(' ')[1]);
                                    Console.WriteLine("send2 com: " + cur_num_board + "/" + count_send2 + " " + coms2[0]);
                                    if (count_send2 - 1 == cur_num_board)
                                    {
                                        var mes_out = Encoding.ASCII.GetBytes(coms2[0]); count_ins++;
                                        udp_client2.SendAsync(mes_out, mes_out.Length);
                                        //Console.WriteLine("send2 com: " + cur_num_board + "/" + count_send2 + " " + coms2[0]);
                                    }
                                    else if (cur_num_board == count_send2)
                                    {
                                        coms2.RemoveAt(0);
                                        com_num++;
                                        count_send2++;
                                        //Console.WriteLine("send2 plus: " + cur_num_board + "/" + count_send2);
                                    }
                                    else
                                    {
                                        count_send2 = cur_num_board;
                                        //Console.WriteLine("send2 else: " + cur_num_board + "/" + count_send2);
                                    }

                                }
                                //coms2 = new List<string>();
                            }

                        }
                    }


                   
                    // if (_TCPserver1.connected) _TCPserver1.handle();

                    if (com_num > 1) Console.WriteLine(com_num);
                }
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


    class SettingsString
    {

        public int[] ports_cam;

        public SettingsString()
        {
           // ports_cam = new int[3] { 5000, 5001, 5002 };
        }

    }


}

