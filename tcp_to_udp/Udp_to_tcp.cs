using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace tcp_to_udp
{
     class Udp_to_tcp
    {

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
        public void connect_udp_all()
        {
            udp_client = null;
            GC.Collect();

            udp_client = new UdpClient(50000);
            string ip1 = "192.168.10.212";
            var port_udp1 = 52000;
            udp_addres_1 = new IPEndPoint(parse_ip(ip1), port_udp1);
            udp_client.Connect(udp_addres_1);



            udp_client2 = null;
            GC.Collect();

            udp_client2 = new UdpClient(50001);
            string ip2 = "192.168.10.211";
            var port_udp2 = 52100;
            udp_addres_2 = new IPEndPoint(parse_ip(ip2), port_udp2);
            udp_client2.Connect(udp_addres_2);


            udp_thread = new Thread(recieve_udp_all);
            udp_thread.Start();

            _TCPserver1 = new TCPserver(62000);
            server_thread1 = new Thread(_TCPserver1.startServer);
            server_thread1.Start();

        }


        public static IPAddress parse_ip(string ip)
        {
            if (ip == null) return null;
            if (ip.Length < 3) return null;
            if (ip.Count(c => c == '.') != 3) return null;

            var cells = ip.Split('.');
            var bytes = new byte[] { 0, 0, 0, 0 };
            for (int i = 0; i < cells.Length; i++)
            {
                int cur_cell = -1;
                if (int.TryParse(cells[i], out cur_cell))
                {
                    if (!(cur_cell >= 0 && cur_cell < 256))
                    {
                        return null;
                    }
                    else
                    {
                        bytes[i] = (byte)cur_cell;
                    }
                }
            }
            return new IPAddress(bytes);
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
                                // Console.WriteLine("len2: " + coms2.Count);
                                foreach (var command in coms2)
                                {

                                    //Console.WriteLine("com2: " + command);
                                    var mes_out = Encoding.ASCII.GetBytes(command); count_ins++;
                                    udp_client2.SendAsync(mes_out, mes_out.Length);
                                    com_num++;

                                }
                                coms2 = new List<string>();


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
                                foreach (var command in coms1)
                                {

                                    //Console.WriteLine("com1: " + command);
                                    var mes_out = Encoding.ASCII.GetBytes(command);
                                    udp_client.SendAsync(mes_out, mes_out.Length);
                                    com_num++;

                                }
                                coms1 = new List<string>();

                            }



                        }
                    }
                    // if (_TCPserver1.connected) _TCPserver1.handle();

                    if (com_num > 1) Console.WriteLine(com_num);
                }
            }


        }

    }
}

