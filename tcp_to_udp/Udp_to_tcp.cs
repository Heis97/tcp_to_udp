using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using WinRT.Interop;
using Encoder = System.Drawing.Imaging.Encoder;

namespace tcp_to_udp
{
     class Udp_to_tcp
    {

        private static bool[] _isStreaming = new bool[3];
        private static VideoCapture[] _cameras = new VideoCapture[3];


        UdpClient udp_client;
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
        public void connect_udp_all()
        {
            udp_client = null;
            GC.Collect();

            udp_client = new UdpClient(50000);
            string ip1 = "192.168.10.212";
            var port_udp1 = 52000;
            udp_addres_1 = new IPEndPoint(IPAddress.Parse(ip1), port_udp1);
            udp_client.Connect(udp_addres_1);



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

            cams_thr[0]= start_cam(0,5000);
            //Thread.Sleep(2000);
            cams_thr[1] = start_cam(1, 5001);
            //Thread.Sleep(2000);
            cams_thr[2] = start_cam(2, 5002);
        }


        List<string> coms1 = new List<string>();
        List<string> coms2 = new List<string>();


        void recieve_udp_all()
        {
            int count_ins = 0;
            while (udp_client != null && udp_client2 != null)
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
                            if (command.Length > 3)
                                if (command.Contains("M577") || command.Contains("M578") || command.Contains("M579") || command.Contains("M580") || command.Contains("M584") || command.Contains("M587"))
                                {
                                    //Console.WriteLine("add com1: "+ command);
                                    coms1.Add(command);
                                }
                                else if (command.Contains("M585") || command.Contains("M581"))
                                {
                                   // Console.WriteLine("add com2: " + command);
                                    coms2.Add(command);
                                }
                                else if (command.Contains("M590") && command.Contains("*"))
                                {
                                    var com = command.Split("*")[0];
                                     //Console.WriteLine("add com3: " + command);
                                    var command_af = com.Replace("  ", " ");
                                    command_af = command_af.Replace("  ", " ");
                                    var vars = command_af.Trim().Split(' ');

                                    if(vars.Length > 3)
                                    {
                                        var ind_cam = Convert.ToInt32(vars[2]);
                                        var exp_cam = Convert.ToInt32(vars[3]);
                                        Console.WriteLine(ind_cam+" "+exp_cam);
                                        _cameras[ind_cam].Set(Emgu.CV.CvEnum.CapProp.Exposure, exp_cam);
                                        //coms2.Add(command);
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
                                    //Console.WriteLine("send2: " + coms2[0]);
                                    var mes_out = Encoding.ASCII.GetBytes(coms2[0]); count_ins++;
                                    udp_client2.SendAsync(mes_out, mes_out.Length);
                                    com_num++;
                                    coms2.RemoveAt(0);
                                }                               
                                //coms2 = new List<string>();
                            }

                        }
                    }


                    while (udp_client.Available > 0)
                    {
                        var res = udp_client.Receive(ref udp_addres_1);

                        var mes = Encoding.ASCII.GetString(res) + "\n";
                        if (res != null)
                        {
                            // Console.WriteLine("udp res: " + mes);
                            //label_udp_state_1.BeginInvoke((MethodInvoker)(() => label_udp_state_1.Text = mes));
                            if (_TCPserver1.connected)
                            {
                                _TCPserver1.pushBuffer(mes);
                                // Console.WriteLine("len1: " + coms1.Count);
                                if (coms1.Count > 0)
                                {
                                    //Console.WriteLine("send1: " + coms1[0]);
                                    var mes_out = Encoding.ASCII.GetBytes(coms1[0]); count_ins++;
                                    udp_client.SendAsync(mes_out, mes_out.Length);
                                    com_num++;
                                    coms1.RemoveAt(0);
                                }
                                

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

            _cameras[ind].Set(Emgu.CV.CvEnum.CapProp.Exposure, -9);



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


        static byte[] FrameToJpegBytesEmgu(Mat frame, int quality = 90)
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

    }
}

