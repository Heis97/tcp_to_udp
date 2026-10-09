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
using System.Reflection;
using Emgu.CV.Stitching;

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

        List<string> coms1 = new List<string>();
        List<string> coms2 = new List<string>();

        List<Command> commands1 = new List<Command>();
        List<Command> commands2 = new List<Command>();
        long command_counter1 = 0;
        long command_counter2 = 0;

        volatile int string_is_ending = 0;
        volatile int pound_is_ending = 0;


        SettingsString settins_string = null;

        List<string> prog_orig_commands = new List<string>();

        List<List<StepperFrame>> main_alternately_commands = new List<List<StepperFrame>>();
        int main_alternately_commands_counter = 0;


        List<StepperFrame> alternately_commands = new List<StepperFrame>();
        List<StepperFrame> prog_commands = new List<StepperFrame>();
        List<StepperFrame> jog_commands = new List<StepperFrame>();

        StepperFrame[] bed_calib_ps = new StepperFrame[3];
        int cur_jog_line = 0;
        int cur_alternately_line = 0;
        int cur_alternately_line_internal = 0;
        programm_state prog_state = programm_state.STOP;
        int cur_prog_line = 0;
        int pause_prog_line = 0;


        long[] cur_pos = new long[] { 0, 0, 0, 0 };
        long[] prev_pos = new long[] { 0, 0, 0, 0 };
        StepperFrame cur_frame = new StepperFrame(new Point3d_GL(0, 0, 0), 0, 0);
        StepperFrame offset_frame = new StepperFrame(new Point3d_GL(0, 0, 0), 0, 0);

        StepperFrame offset_plate = new StepperFrame(new Point3d_GL(0, 0, 0), 0, 0);
        StepperFrame offset_nossle = new StepperFrame(new Point3d_GL(0, 0, 0), 0, 0);



        StepperPrinter printer = new StepperPrinter();

        public void connect_udp_all()
        {
            //var settins_string = load_obj<SettingsString>("settings_printer.json");
            //ports_cam = settins_string.ports_cam;
            //ListAllCamerasButton_Click();
            //var set_test = new SettingsString();
            //set_test.ports_cam = ports_cam;
            //save_obj("settings_printer2.json", set_test);
            device_numb = 0;
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


            


            //commands1.Add(new Command(coms_str.Length + 1, "M588 A1"));

            /*for (int i = 0; i < 100; i++) commands1.Add(new Command( i, "M588 X"+i*5+" Y0 Z100 E100 W"+100*i));// J-" + ( Math.Round( i * 0.01,3)).ToString()));
            commands1.Add(new Command(100, "M588 A1"));
            commands1.Add(new Command(101, "M588 A1"));*/

            udp_thread = new Thread(recieve_udp_all);
            udp_thread.Start();



            _TCPserver1 = new TCPserver(62000);
            server_thread1 = new Thread(_TCPserver1.startServer);
            server_thread1.Start();

            /*var coms_str = StepperFrame.test();
            for (int i = 0; i < coms_str.Length; i++)
            {
                if(i==20) _TCPserver1.pushBuffer_in("M596 G1 A1 D0 C" + coms_str.Length + "\n");
                _TCPserver1.pushBuffer_in(coms_str[i] + '\n');
            }*/


            //tcp_client_main = new TCPclient();
            // Console.WriteLine("start con");
            // tcp_client_main.Connection(port_main, ip_main);

            //Process.Start("String_line4.exe");
            load_settings();


            Console.WriteLine("1 " + commands1.Count);

            //Console.WriteLine("start con done");
            //for (int i = 0; i < 3; i++) cams_thr[i] = start_cam(i, ports_cam[i]);
            while (true)
            {
                string? input = Console.ReadLine();
                if (input != null)
                {
                    _TCPserver1.pushBuffer_in(input +"\n");
                    //Console.WriteLine(input);
                }              
            } 
        }

        

        public static void show_delta_table(double[,,] table)
        {
            var im = new Image<Gray,byte>(table.GetLength(0), table.GetLength(1));
            for (int i = 0; i < im.Width; i++)
            {
                for (int j = 0; j < im.Height; j++)
                {
                    if(table[i, j, 0]!=0)
                    {
                        im.Data[i, j, 0] = 255;
                    }
                    
                }
            }
            CvInvoke.Imshow("table", im);
            CvInvoke.WaitKey();
        }

        double jog_xyz_vel = 10;
        int ring_en = 0;
        int lookup_buf = 100;
        int safe_len_send_val = 40;



        int ring_buf_en = 0;
        int all_steps_kinem = 0;
        int all_steps1 = 0;        
        int prog_done = 0;
        int wait_en = 0;
        int wait_term = 0;

        int all_steps2 = 0;

        int consider_wait_all_steps_kinem = 0;
        int consider_all_steps1 = 0;
        int consider_prog_done = 0;
        int consider_wait_en =0;
        int consider_wait_term = 0;

        int consider_all_steps2 = 0;


        //------------------------------------------
        int prev_delta_calib = 0;
        int prev_homing = 0;
        bool delta_calib_go_next_p = false;
        bool delta_calib_nozzle_up = false;

        int calibr_x = -1;
        int calibr_y = -1;
        int calibr_z = -1;
        //----------------------------------------
        int max_count_cur_prog = 0;
        double jog_len = 100;
        int stop_len = 21;
        bool stop_len_setted = false;
        //================================
        double calibr_vel = 3d;
        bool calibrating_nossle = false;
        int calibrate_nossle_stage_counter = 0;
        List< StepperFrame> calibrate_nossle_frames = new List< StepperFrame>();

        int[] tool_inds = new int[4];
        bool main_alternately_commands_exec = false;
        enum programm_state { MOVE, STOP, PAUSE, JOG , ALTERNATELY , CALIBRATE}

        bool auto_calibr = false;
        int cur_nossle_type = 0;
        int cur_plate_type = 0;

        int tool_active = 0;

        public void comp_offset()
        {
            if(auto_calibr)
            {
                
                
                offset_plate.p_xyz = new Point3d_GL(settins_string.offset_plate_x[cur_plate_type], settins_string.offset_plate_y[cur_plate_type], settins_string.offset_plate_z[cur_plate_type]);
                Console.WriteLine("offset_nossle" + offset_nossle.p_xyz);
                Console.WriteLine("offset_plate:" + offset_plate.p_xyz + " num:" + cur_plate_type);
                offset_frame = new StepperFrame(offset_nossle.p_xyz + offset_plate.p_xyz, 0, jog_xyz_vel);

                Console.WriteLine("offset_frame:" + offset_frame.p_xyz );
            }
        }

        public void start_alternate_prog(StepperFrame[] prog_cur)
        {
            comp_offset();
           
            var offs = cur_frame.clone();
            offs.p_xyz -= offset_frame.p_xyz;
            alternately_commands = StepperFrame.prepare_alternate_g_code_to_load(prog_cur,ref printer, offset_frame, offs).ToList();
            cur_alternately_line = 0;

            prog_state = programm_state.ALTERNATELY;

            consider_wait_all_steps_kinem = 0;
            consider_all_steps1 = 0;
            consider_prog_done = 0;
            consider_wait_en = 0;
            consider_wait_term = 0;

            consider_all_steps2 = 0;
        }

        public void start_main_alternate_prog(StepperFrame[] prog_cur)
        {
            main_alternately_commands = StepperFrame.prepare_main_alternate_g_code_to_load(prog_cur);
            main_alternately_commands_counter = 0;
            main_alternately_commands_exec = true;

            
        }


        public void load_commands(string[] coms)
        {
            foreach (var command in coms)
            {
                //Console.WriteLine("command: " + command);
                if (command.Length > 3)
                {
                    if (command.Contains("num1"))
                    {
                        //Console.WriteLine("add com1: " + command);
                        var com_board = command.Replace("num1", "").Trim();
                        commands1.Add(new Command(command_counter1, com_board));
                        command_counter1++;
                    }
                    else if (command.Contains("num2"))
                    {
                        //Console.WriteLine("add com2: " + command);
                        var com_board = command.Replace("num2", "").Trim();
                        commands2.Add(new Command(command_counter2, com_board));
                        command_counter2++;
                    }
                    else if (command.Contains("main"))
                    {
                        exec_main_prog(command);
                    }
                }
            }
        }

        public void exec_main_prog(string command)
        {


            var com_board = command.Replace("main", "").Trim();
            if (command.Contains("M590") || command.Contains("M591"))
            {

                //Console.WriteLine("add com3: " + command);
                var command_af = com_board.Replace("  ", " ");
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
                    else if (command.Contains("M591"))
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
                var val = val_from_command(com_board);
                string_is_ending = val;
                tcp_client_main.send_mes(device_numb + "" + string_is_ending + "" + pound_is_ending);
            }
            else if (command.Contains("M595"))
            {
                var val = val_from_command(com_board);
                pound_is_ending = val;
                tcp_client_main.send_mes(device_numb + "" + string_is_ending + "" + pound_is_ending);
            }

            else if (command.Contains("M596")) // prog load
            {
                var com_re = com_board.Replace("M596 ", "").Trim();
                prog_orig_commands.Add(com_re);

            }

            else if (command.Contains("M597"))// prog control
            {
                var val = val_from_command(com_board);
                Console.WriteLine("M597 val: " + val);
                if (val == 0)
                {
                    var frames_xyz_list = StepperFrame.convert_g_code_to_stepperframes(prog_orig_commands.ToArray(), printer).ToList();
                    if (frames_xyz_list != null)
                    {
                        frames_xyz_list.Insert(0, new StepperFrame(cur_frame.p_xyz - offset_frame.p_xyz, 0, jog_xyz_vel));
                        prog_commands = StepperFrame.convert_g_code(frames_xyz_list.ToArray(), ref printer, offset_frame)?.ToList();


                        if (prog_commands != null)
                        {
                            prog_commands = StepperFrame.prepare_g_code_to_load(prog_commands.ToArray(), ref printer).ToList();
                            cur_prog_line = 0;
                            prog_state = programm_state.MOVE;
                        }

                        //Console.WriteLine("move");
                    }


                }
                else if (val == 1)
                {
                    prog_state = programm_state.PAUSE;
                }
                else
                {
                    _TCPserver1.pushBuffer_in("num1 M588 A0" + "\n");
                    prog_state = programm_state.STOP;
                }
            }
            else if (command.Contains("M598")) // prog clear
            {
                prog_commands = new List<StepperFrame>();
                prog_orig_commands = new List<string>();
            }
            else if (command.Contains("M610"))//set jog vel
            {
                var val = val_from_command_d(com_board);
                jog_xyz_vel = val;
            }

            else if (command.Contains("M611" ) && all_steps_kinem == 0)//jog 
            {
                //Console.WriteLine("all_steps_kinem: " + all_steps_kinem);
                var val = val_from_command(com_board);
                var jog_orig = new List<StepperFrame>();
                var fr_cur = cur_frame.clone();
                fr_cur.vel = jog_xyz_vel;
                jog_orig.Add(fr_cur);
                var fr_jog = fr_cur.clone();
                fr_jog.p_xyz = fr_jog.p_xyz.add_mask(val, jog_len);
                jog_orig.Add(fr_jog);
                cur_jog_line = 0;
                jog_commands = StepperFrame.convert_g_code(jog_orig.ToArray(), ref printer, new StepperFrame(new Point3d_GL(0, 0, 0), 0, 0)).ToList();
                if (jog_commands != null)
                {
                    jog_commands = StepperFrame.prepare_g_code_to_load(jog_commands.ToArray(), ref printer).ToList();
                    prog_state = programm_state.JOG;
                }


            }
            else if (command.Contains("M612"))//set_zero
            {
                offset_frame = cur_frame.clone();
            }
            else if (command.Contains("M613"))//set_zero
            {

                var val = val_from_command(com_board);
                //
                if (val <= 4) { val = 4; printer.delta_init_calibr(StepperPrinter.delta_calibr_ps_count.ps4); }
                else { val = 18; printer.delta_init_calibr(StepperPrinter.delta_calibr_ps_count.ps18); }

                _TCPserver1.pushBuffer_in("main M589 X80" + "\n");

                printer.delta_calibr_en = true;

                // Console.WriteLine("printer.delta_calibr_en = true;");
            }
            else if (command.Contains("M614"))//settings load
            {

                load_settings();
            }
            else if (command.Contains("M615"))//move zero p
            {

                var frames_xyz_list = new StepperFrame[]
                {
                                        new StepperFrame(cur_frame.p_xyz-offset_frame.p_xyz,0,jog_xyz_vel),
                                        new StepperFrame(new Point3d_GL(),0,jog_xyz_vel),
                };
                prog_commands = StepperFrame.convert_g_code(frames_xyz_list, ref printer, offset_frame).ToList();
                if (prog_commands != null)
                {
                    prog_commands = StepperFrame.prepare_g_code_to_load(prog_commands.ToArray(), ref printer).ToList();
                    cur_prog_line = 0;
                    prog_state = programm_state.MOVE;
                    Console.WriteLine("move");
                }

            }
            else if (command.Contains("M616"))//remember_p 
            {
                var val = val_from_command(com_board);
                if (val >= 0 && val < bed_calib_ps.Length)
                {
                    bed_calib_ps[val] = cur_frame;
                }

                if (val < 0)
                {
                    printer.bed_calib_vec = new Point3d_GL(0, 0, 1);
                }

                if (val > bed_calib_ps.Length)
                {

                    var p1 = bed_calib_ps[0].p_xyz;
                    var p2 = bed_calib_ps[1].p_xyz;
                    var p3 = bed_calib_ps[2].p_xyz;
                    var vecn = new Flat3d_GL(p1, p2, p3).n;
                    if (Math.Abs(vecn.z) > 0.5)
                    {
                        if (vecn.z < 0)
                        {
                            vecn.x *= -1;
                            vecn.y *= -1;
                            vecn.z *= -1;
                        }
                        printer.bed_calib_vec = new Point3d_GL(vecn.x, vecn.y, vecn.z);
                        Console.WriteLine("printer.bed_calib_vec: " + printer.bed_calib_vec);
                    }
                    else
                    {
                        Console.WriteLine("vecn.z < 0.5");
                    }

                }
            }

            else if (command.Contains("M617"))//set jog vel
            {
                var val = val_from_command(com_board);
                printer.koef_extrus = val / 100d;
            }

            else if (command.Contains("M618"))//set jog vel
            {
                var val = val_from_command(com_board);
                printer.koef_vel = val / 100d;
            }


            else if (command.Contains("M619"))
            {
                var val = val_from_command(com_board);


                //i3 vert, i4 rot, 
                offset_frame.p_xyz = new Point3d_GL(0, 0, 0);
                auto_calibr = false;
                StepperFrame[] prog_cur = null;
                prog_cur = StepperPrinter.gen_change_prog(val, settins_string, cur_frame.clone());
                
                if(prog_cur!=null)  start_alternate_prog(prog_cur);
            }

            else if (command.Contains("M620")) // take i tool
            {
                var prog_cur = new List<StepperFrame>();

                var val = val_from_command(com_board);
                var tool_dest = val;
                
                var manip_left = tool_inds[0];
                var manip_right = tool_inds[1];

                var last_frame = cur_frame.clone();

                if (manip_left != tool_dest && manip_right != tool_dest && tool_active != tool_dest) Console.WriteLine("Вставте экструдер: " + tool_dest);
                if (tool_active != 0)
                {
                    int prog_num_give = -1;
                    if (manip_left == 0)
                    {
                        prog_num_give = 1;
                    }
                    if (manip_right == 0)
                    {
                        prog_num_give = 3;
                    }
                    if(prog_num_give<=0)
                    {
                        Console.WriteLine("Нет свободного места: Текущий экструдер: " + tool_active + "; Манипулятор левый: " + manip_left + "; Манипулятор правый: " + manip_right);
                        main_alternately_commands_exec = false;
                        prog_state = programm_state.STOP;
                          
                        return;

                    }
                    else
                    {
                        prog_cur.AddRange(StepperPrinter.gen_change_prog(prog_num_give, settins_string, last_frame.clone()));
                        last_frame = StepperFrame.find_last_kinematic_frame(prog_cur.ToArray());
                        if (last_frame == null) return;
                    }
                }

                int prog_num_take = -1;
                if(manip_left == tool_dest)
                {
                    prog_num_take = 0;
                }
                if (manip_right == tool_dest)
                {
                    prog_num_take = 2;
                }
                
                prog_cur.AddRange(StepperPrinter.gen_change_prog(prog_num_take, settins_string, last_frame.clone()));

                if (prog_cur != null)
                {
                    tool_active = tool_dest;
                    start_alternate_prog(prog_cur.ToArray());
                }
                
            }

            else if (command.Contains("M621")) //drop active tool
            {
                var prog_cur = new List<StepperFrame>();
                var last_frame = cur_frame.clone();
                int prog_num_give = -1;
                var manip_left = tool_inds[0];
                var manip_right = tool_inds[1];
                if (manip_left == 0)
                {
                    prog_num_give = 1;
                }
                if (manip_right == 0)
                {
                    prog_num_give = 3;
                }
                if (prog_num_give <= 0)
                {
                    Console.WriteLine("Нет свободного места: Текущий экструдер: " + tool_active + "; Манипулятор левый: " + manip_left + "; Манипулятор правый: " + manip_right);
                    main_alternately_commands_exec = false;
                    prog_state = programm_state.STOP;
                    return;
                }
                else
                {
                    prog_cur.AddRange(StepperPrinter.gen_change_prog(prog_num_give, settins_string, last_frame.clone()));
                    if (prog_cur != null) start_alternate_prog(prog_cur.ToArray());
                }
            }

            else if (command.Contains("M622"))
            {



            }

            else if (command.Contains("M623"))//set bring tablet
            {

                var prog_cur = new List<StepperFrame>();
                prog_cur.Add(new StepperFrame(2, 577, "I0 V" + settins_string.servo_open_val, false));
                prog_cur.Add(new StepperFrame(2, 577, "I1 V" + (180 - settins_string.servo_open_val), false));
                prog_cur.Add(new StepperFrame(2, 587, "I0 P" + settins_string.table_change_pos + " L", false));
                prog_cur.Add(new StepperFrame(2, 587, "I2 P" + settins_string.lift_up_val + " L", false));
                prog_cur.Add(new StepperFrame(2, 577, "I0 V" + settins_string.servo_close_val, false));
                prog_cur.Add(new StepperFrame(2, 577, "I1 V" + (180 - settins_string.servo_close_val), false));
                prog_cur.Add(new StepperFrame(2, 587, "I2 H"));
                prog_cur.Add(new StepperFrame(2, 587, "I0 P" + settins_string.table_work_pos + " L", false));
                start_alternate_prog(prog_cur.ToArray());
            }

            else if (command.Contains("M624"))//set give tablet
            {

                var prog_cur = new List<StepperFrame>();
                prog_cur.Add(new StepperFrame(2, 587, "I0 P" + settins_string.table_change_pos + " L", false));
                prog_cur.Add(new StepperFrame(2, 587, "I2 P" + settins_string.lift_up_val + " L", false));
                prog_cur.Add(new StepperFrame(2, 577, "I0 V" + settins_string.servo_open_val, false));
                prog_cur.Add(new StepperFrame(2, 577, "I1 V" + (180 - settins_string.servo_open_val), false));
                prog_cur.Add(new StepperFrame(2, 587, "I2 H", false));
                prog_cur.Add(new StepperFrame(2, 587, "I0 P" + settins_string.table_open_pos + " L", false));
                start_alternate_prog(prog_cur.ToArray());
            }
            else if (command.Contains("M630"))//home manipulators
            {

                var prog_cur = new List<StepperFrame>();
                prog_cur.Add(new StepperFrame(2, 587, "I3 S50 L", false, 0));
                prog_cur.Add(new StepperFrame(2, 587, "I4 S50 L", false, 0));
                prog_cur.Add(new StepperFrame(2, 587, "I5 S50 L", false, 0));
                prog_cur.Add(new StepperFrame(2, 587, "I6 S50 L", false));

                prog_cur.Add(new StepperFrame(2, 587, "I3 H", false, 0));
                prog_cur.Add(new StepperFrame(2, 587, "I4 H", false, 0));
                prog_cur.Add(new StepperFrame(2, 587, "I5 H", false, 0));
                prog_cur.Add(new StepperFrame(2, 587, "I6 H", false));

                prog_cur.Add(new StepperFrame(2, 587, "I4 P" + settins_string.take_left_manip_rot[4] + " L", false, 0));
                prog_cur.Add(new StepperFrame(2, 587, "I3 P100 L", false, 0));
                prog_cur.Add(new StepperFrame(2, 587, "I6 P" + settins_string.take_right_manip_rot[4] + " L", false, 0));
                prog_cur.Add(new StepperFrame(2, 587, "I5 P100 L", false));

                start_alternate_prog(prog_cur.ToArray());

            }
            else if (command.Contains("M631"))//set xy_calibrate
            {
                offset_frame.p_xyz = new Point3d_GL(0, 0, 0);
                auto_calibr = false;
                double save_dist_nossle = 10;
                calibrate_nossle_stage_counter = 0;

                calibrating_nossle = true;
                var prog_cur = new List<StepperFrame>();
                var cur_fr_dest = cur_frame.clone();
                cur_fr_dest.vel = jog_xyz_vel;
                prog_cur.Add(cur_fr_dest.clone());
                if (cur_fr_dest.p_xyz.z < settins_string.calibrate_nossle_z[0] + save_dist_nossle)
                {
                    cur_fr_dest.p_xyz.z += save_dist_nossle;
                }
                //add offset for different nossle    [cur_nossle]
                prog_cur.Add(cur_fr_dest.clone());
                prog_cur.Add(new StepperFrame(new Point3d_GL(settins_string.calibrate_nossle_x[0], settins_string.calibrate_nossle_y[0], settins_string.calibrate_nossle_z[0] + save_dist_nossle), 0, jog_xyz_vel));
                prog_cur.Add(new StepperFrame(new Point3d_GL(settins_string.calibrate_nossle_x[0], settins_string.calibrate_nossle_y[0], settins_string.calibrate_nossle_z[0]), 0, jog_xyz_vel));
                start_alternate_prog(prog_cur.ToArray());
                calibrate_nossle_stage_counter = 1;
                calibrate_nossle_frames = new List<StepperFrame>();
                Console.WriteLine("calibrate_nossle_stage_counter: " + calibrate_nossle_stage_counter);


            }
            else if (command.Contains("M632"))
            {
                var val = val_from_command(com_board);
                if (val == 1) auto_calibr = true;
                if (val == 0) auto_calibr = false;

            }


            else if (command.Contains("M634"))//home osc
            {

                var prog_cur = new List<StepperFrame>();
                prog_cur.Add(new StepperFrame(1, 587, "I3 S50 L", false,0));
                prog_cur.Add(new StepperFrame(1, 587, "I4 S50 L", false, 0));
                prog_cur.Add(new StepperFrame(1, 587, "I5 S50 L", false, 0));
                prog_cur.Add(new StepperFrame(1, 587, "I6 S50 L", false));

                prog_cur.Add(new StepperFrame(1, 587, "I3 H", false, 0));
                prog_cur.Add(new StepperFrame(1, 587, "I4 H", false, 0));
                prog_cur.Add(new StepperFrame(1, 587, "I5 H", false, 0));
                prog_cur.Add(new StepperFrame(1, 587, "I6 H", false));

                prog_cur.Add(new StepperFrame(1, 587, "I3 P-50 L", false, 0));
                prog_cur.Add(new StepperFrame(1, 587, "I4 P"+settins_string.osc_rot_start_pos1+" L", false, 0));
                prog_cur.Add(new StepperFrame(1, 587, "I5 P-50 L", false, 0));
                prog_cur.Add(new StepperFrame(1, 587, "I6 P"+settins_string.osc_rot_start_pos2+" L", false));

                start_alternate_prog(prog_cur.ToArray());

            }

            else if (command.Contains("M635"))//home osc
            {

                var prog_cur = new List<StepperFrame>();

                //prog_cur.Add(new StepperFrame(1, 589, "X10"));
                prog_cur.Add(new StepperFrame(0, 631, ""));

                prog_cur.Add(new StepperFrame(0, 637, "1"));
                //prog_cur.Add(new StepperFrame(0, 638, "-41.8732 67.1863 -394.5207"));
                //prog_cur.Add(new StepperFrame(0, 632, "1"));
                prog_cur.Add(new StepperFrame(new Point3d_GL(0,0,50),0,jog_xyz_vel));
                prog_cur.Add(new StepperFrame(new Point3d_GL(0, 10, 50), 0, jog_xyz_vel));
                prog_cur.Add(new StepperFrame(new Point3d_GL(10, 10, 50), 0, jog_xyz_vel));
                prog_cur.Add(new StepperFrame(new Point3d_GL(10, 0, 50), 0, jog_xyz_vel));
                prog_cur.Add(new StepperFrame(new Point3d_GL(0, 0, 50), 0, jog_xyz_vel));
                prog_cur.Add(new StepperFrame(0, 619, "1"));


                /*var prog_cur = new List<StepperFrame>();

                //prog_cur.Add(new StepperFrame(1, 589, "X10"));
                //prog_cur.Add(new StepperFrame(0, 631, ""));

                //prog_cur.Add(new StepperFrame(0, 637, "1"));
                //prog_cur.Add(new StepperFrame(0, 638, "-41.8732 67.1863 -394.5207"));
                //prog_cur.Add(new StepperFrame(0, 632, "1"));
                prog_cur.Add(new StepperFrame(new Point3d_GL(0, 0, -292), 0, 2));
                prog_cur.Add(new StepperFrame(1, 589, "P500000"));
                //prog_cur.Add(new StepperFrame(1, 589, "P500000"));
                prog_cur.Add(new StepperFrame(new Point3d_GL(0, 0, -293.4), 0, 2));
                prog_cur.Add(new StepperFrame(new Point3d_GL(0, 0, -294.4), 0, 2));
                prog_cur.Add(new StepperFrame(1, 589, "P500000"));
                prog_cur.Add(new StepperFrame(1, 587, "I3 S300 L", false));
                prog_cur.Add(new StepperFrame(0, 0, 0, 0, 1, 0));
                prog_cur.Add(new StepperFrame(new Point3d_GL(0, 0, -292), 0, 2));
                prog_cur.Add(new StepperFrame(1, 589, "P500000"));
                prog_cur.Add(new StepperFrame(0, 0, 0, 0, 1, 0));
                prog_cur.Add(new StepperFrame(new Point3d_GL(0, 0, -293.4), 0, 2));
                prog_cur.Add(new StepperFrame(1, 589, "P200000"));
                prog_cur.Add(new StepperFrame(0, 0, 0, 0, 1, 0));
                prog_cur.Add(new StepperFrame(new Point3d_GL(0, 0, -292), 0, 2));
                prog_cur.Add(new StepperFrame(1, 589, "P200000"));
                prog_cur.Add(new StepperFrame(0, 0, 0, 0, 1, 0));
                prog_cur.Add(new StepperFrame(new Point3d_GL(0, 0, -293.4), 0, 2));
                prog_cur.Add(new StepperFrame(1, 589, "P200000"));
                prog_cur.Add(new StepperFrame(new Point3d_GL(0, 0, -292.0), 0, 2));
                prog_cur.Add(new StepperFrame(0, 0, 0, 0, 1, 0));*/

                start_main_alternate_prog(prog_cur.ToArray());

            }


            else if (command.Contains("M636"))
            {
                var val = val_from_command(com_board);
                cur_nossle_type = val;

            }

            else if (command.Contains("M637"))
            {
                var val = val_from_command(com_board);
                cur_plate_type = val;

            }


            else if (command.Contains("M638"))
            {
                var val = vals_from_command_d(com_board, 3);
                offset_nossle.p_xyz = new Point3d_GL(val[0], val[1], val[2]);

            }
            else if (command.Contains("M639"))
            {
                var val = val_from_command(com_board);
                tool_active = val;

            }

            else if (command.Contains("M700"))//set all stop
            {
                prog_state = programm_state.STOP;
                _TCPserver1.pushBuffer_in("num1 M589 S" + "\n");
                _TCPserver1.pushBuffer_in("num2 M589 S" + "\n");
                calibrating_nossle = false;
                main_alternately_commands_counter = main_alternately_commands.Count + 1;
            }

            else if (command.Contains("M701"))//set all stop
            {
                var vals = vals_from_command(com_board, 6);



                consider_wait_all_steps_kinem = vals[0];
                consider_all_steps1 = vals[1];
                consider_all_steps2 = vals[2];
                consider_prog_done = vals[3];
                consider_wait_en = vals[4];
                consider_wait_term = vals[5];


            }
        }

        void recieve_udp_all()
        {
            int count_ins = 0;

            long count_send1 = 0;
            long count_send2 = 0;
            bool err_con_tcp = false;




            var max_print_r = printer.max_printing_radius();
            Console.WriteLine("max_print_r: " + max_print_r);
            printer.bed_calib_vec = printer.bed_calib_vec.normalize();
            printer.comp_delta_table(max_print_r);

            /*prog_orig_commands = new List<string>()
            {
                "G1 X-28.996218019813778 Y67.18429135365389 Z-364.5370826206767 F600",
                "G1 X-0.19606700283726042 Y-6.8165616215548965 Z-360.3370826206767 F600",
                "G1 X-0.19606700283726042 Y3.1834383784451035 Z-360.42008262067674 E0.1",
                "G1 X9.80393299716274 Y3.1834383784451035 Z-360.4690826206767 E0.2",
                "G1 X9.80393299716274 Y-6.8165616215548965 Z-360.3860826206767 E0.3",
                "G1 X-0.19606700283726042 Y-6.8165616215548965 Z-360.3370826206767 E0.5",
            };

            var frames_xyz_test_2 = StepperFrame.convert_g_code_to_stepperframes(prog_orig_commands.ToArray(), printer);
            prog_commands = StepperFrame.convert_g_code(frames_xyz_test_2, ref printer, offset_frame).ToList();*/

            /*prog_orig_commands = new List<string>()
            {
                "G1 X0 Y0 F600",
                "G1  X10",
                "G1 X10 Y10",
                "G1  X0 Y10 E1",
                "G1X0 Y0 Z0 E1",
                "G1 X0 Y0 F600",
                "G1  X10",
                "G1 X10 Y10",
                "G1  X0 Y10 E0",
                "G1X0 Y0 Z0 E1",

            };

            var frames_xyz_test_2 = StepperFrame.convert_g_code_to_stepperframes(prog_orig_commands.ToArray(), printer);
            prog_commands = StepperFrame.convert_g_code(frames_xyz_test_2, printer, offset_frame).ToList();*/
            //printer.delta_comp_test();

            //show_delta_table(printer.delta_fk_table_ps);
            //var p_abc_ik = printer.delta_ik(new Point3d_GL(0, 0, -174.72));
            //Console.WriteLine("ik: "+p_abc_ik);
            // printer.solve_fk(new long[] { 1000, 1000, 1000 });

            while (udp_client1 != null)
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
                        load_commands(coms);

                    }
                }
                else
                {
                    if (err_con_tcp)
                    {
                        Console.WriteLine("not connected interface");
                        err_con_tcp = false;
                    }

                }

                if (udp_client1.Available > 0)
                {
                    //Console.WriteLine("udp_client1.Available > 0: " );
                    var res = udp_client1.Receive(ref udp_addres_1);

                    long dtime = DateTime.Now.Ticks - last_time_1;
                    last_time_1 = DateTime.Now.Ticks;

                    var cur_time_ms = DateTime.Now.Millisecond;

                    var dtime_ms = cur_time_ms - last_ms;

                    if (DateTime.Now.Millisecond > 500)
                    {
                        if (!host_send)
                        {
                            //Console.WriteLine(dtime_ms);
                            var mes_serv = device_numb + "" + string_is_ending + "" + pound_is_ending;
                            //Console.WriteLine("send server " + mes_serv);
                            // tcp_client_main.send_mes(mes_serv);
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
                        Console.WriteLine("init 1 re:" + dtime + " " + (last_time_1 - start_time));
                    }


                    //Console.WriteLine(DateTime.Now.Ticks);
                    var mes = Encoding.ASCII.GetString(res);
                    //Console.WriteLine("res1: " + mes);
                    if (res != null)
                    {
                        if (_TCPserver1.connected)
                        {
                            //_TCPserver1.send_mes(mes);
                            var frame_out = cur_frame.p_xyz.Clone();
                            //frame_out.z -= printer.comp_off_bed(frame_out);
                            frame_out -= offset_frame.p_xyz;
                            var tools = "00000";
                            if (tool_inds.Length == 4) tools = tool_inds[0] + "" + tool_inds[1] + "" + tool_inds[2] + "" + tool_inds[3] + "" + tool_active;
                            _TCPserver1.pushBuffer(mes + " " + frame_out.ToString() +" "+ tools + "\n");
                        }
                        //Console.WriteLine(mes);
                        // Console.WriteLine("len1: " + coms1.Count);
                        var vars_from_mes = mes.Split(' ');
                        var cur_num_board = (long)Convert.ToInt32(vars_from_mes[1]);

                        //Console.WriteLine(vars_from_mes.Length);
                        if (vars_from_mes.Length >= 9)
                        {
                            //try
                            {
                                var ring_counter = (long)Convert.ToInt32(vars_from_mes[2]);
                                var cur_send = (long)Convert.ToInt32(vars_from_mes[3]);


                                if (vars_from_mes[4].Length == 5)
                                {
                                    wait_en = Convert.ToInt32(vars_from_mes[4][3]) - 48;
                                    all_steps1 = Convert.ToInt32(vars_from_mes[4][0]) - 48;
                                    all_steps_kinem = Convert.ToInt32(vars_from_mes[4][1]) - 48;

                                    //Console.WriteLine("1:" + vars_from_mes[4]);
                                }

                                if (cur_send == 0)
                                {

                                    //ring_en = Convert.ToInt32(vars_from_mes[6]);

                                    

                                    ring_buf_en = Convert.ToInt32(vars_from_mes[6]);


                                    var nossle_calib_vals = vars_from_mes[10];
                                    calibr_x = Convert.ToInt32(nossle_calib_vals[0]) - 48;
                                    calibr_y = Convert.ToInt32(nossle_calib_vals[1]) - 48;
                                    calibr_z = Convert.ToInt32(nossle_calib_vals[2]) - 48;

                                    var tool_recogn_vals = vars_from_mes[11];
                                    if (tool_recogn_vals.Length == 8)
                                    {
                                        var tool0_0 = Convert.ToInt32(tool_recogn_vals[0]) - 48;
                                        var tool0_1 = Convert.ToInt32(tool_recogn_vals[1]) - 48;
                                        var tool0_2 = Convert.ToInt32(tool_recogn_vals[2]) - 48;

                                        var tool1_0 = Convert.ToInt32(tool_recogn_vals[3]) - 48;
                                        var tool1_1 = Convert.ToInt32(tool_recogn_vals[4]) - 48;
                                        var tool1_2 = Convert.ToInt32(tool_recogn_vals[5]) - 48;

                                        var tool2_0 = Convert.ToInt32(tool_recogn_vals[6]) - 48;
                                        var tool3_0 = Convert.ToInt32(tool_recogn_vals[7]) - 48;

                                        tool_inds = new int[] { 0, 0, 0, 0 };

                                        if (tool0_0 == 0) tool_inds[0] = 1;
                                        if (tool0_1 == 0) tool_inds[0] = 2;
                                        if (tool0_2 == 0) tool_inds[0] = 3;

                                        if (tool1_0 == 0) tool_inds[1] = 1;
                                        if (tool1_1 == 0) tool_inds[1] = 2;
                                        if (tool1_2 == 0) tool_inds[1] = 3;

                                        if (tool2_0 == 0) tool_inds[2] = 1;
                                        if (tool3_0 == 0) tool_inds[3] = 1;


                                    }

                                }
                                var cur_prog_line_board = Convert.ToInt64(vars_from_mes[2]);
                                //cur position-----------------------------------------------------------------
                                if (cur_send == 1)
                                {
                                    var cur_poses = new long[8];
                                    for (int i = 0; i < 8; i++)
                                    {
                                        cur_poses[i] = Convert.ToInt64(vars_from_mes[5 + i]);
                                    }


                                    cur_pos = new long[] { cur_poses[0], cur_poses[1], cur_poses[2], cur_poses[7] };

                                    cur_frame = printer.solve_fk(cur_pos);
                                    cur_frame.p_xyz.z -= printer.comp_off_bed(cur_frame.p_xyz);
                                    /**/
                                }



                                //prog_work-----------------------------------------------------------------
                                if (prog_state == programm_state.MOVE && (cur_prog_line + safe_len_send_val < cur_prog_line_board || cur_prog_line < lookup_buf - safe_len_send_val))
                                {
                                    //Console.WriteLine(mes);
                                    if (cur_prog_line < prog_commands?.Count)
                                    {
                                        _TCPserver1.pushBuffer_in(prog_commands[cur_prog_line].get_command(printer) + "\n");
                                        cur_prog_line++;



                                    }
                                }

                                //jog work-----------------------------------------------------------------
                                if (prog_state == programm_state.JOG && (cur_jog_line + safe_len_send_val < cur_prog_line_board || cur_jog_line < lookup_buf - safe_len_send_val))
                                {
                                    if (cur_jog_line < jog_commands?.Count)
                                    {

                                        var com = jog_commands[cur_jog_line].get_command(printer);
                                        _TCPserver1.pushBuffer_in(com + "\n");
                                        //Console.WriteLine("cur_prog_line_board: " + cur_prog_line_board + "; cur_jog_line: " + cur_jog_line+"/"+ jog_commands?.Count+"; "+com);

                                        cur_jog_line++;
                                    }
                                }
                                var commands_load = (commands1.Count == 0 && commands2.Count == 0 && _TCPserver1.buffer_out.Length == 0);
                                var printer_work =
                                    (consider_all_steps1 == 1 && all_steps1 == 1) ||
                                    (consider_all_steps2 == 1 && all_steps2 == 1) ||
                                    (consider_wait_all_steps_kinem == 1 && all_steps_kinem == 1) ||
                                    (consider_prog_done == 1 && ring_buf_en == 1) ||
                                    (consider_wait_en == 1 && wait_en == 1) ||
                                    (consider_wait_term == 1 && wait_term == 1);

                                if (prog_state == programm_state.ALTERNATELY)
                                {
                                    
                                    //Console.WriteLine("cur: "+all_steps_kinem + " " + all_steps1 + " " + all_steps2 + " " + ring_buf_en + " " + wait_en + " " + wait_term);
                                    //Console.WriteLine("con: " + consider_wait_all_steps_kinem + " " + consider_all_steps1 + " " + consider_all_steps2 + " " + consider_prog_done + " " + consider_wait_en + " " + consider_wait_term);
                                    if (commands_load && !printer_work)
                                    {
                                        if (cur_alternately_line < alternately_commands.Count)
                                        {

                                            var cur_alternately_line_internal = alternately_commands[cur_alternately_line].line_number;
                                            var printer_ready_kinem = ((cur_alternately_line_internal + safe_len_send_val < cur_prog_line_board || cur_alternately_line_internal < lookup_buf - safe_len_send_val));// && cur_alternately_line_internal < stop_len

                                            if (printer_ready_kinem || !alternately_commands[cur_alternately_line].kinematic)
                                            {
                                                var com = alternately_commands[cur_alternately_line].get_command(printer);
                                                Console.WriteLine("alt: "+cur_alternately_line+"/" + alternately_commands.Count + " " + com);
                                                Console.WriteLine("cur: "+all_steps_kinem + " " + all_steps1 + " " + all_steps2 + " " + ring_buf_en + " " + wait_en + " " + wait_term);
                                                Console.WriteLine("con: " + consider_wait_all_steps_kinem + " " + consider_all_steps1 + " " + consider_all_steps2 + " " + consider_prog_done + " " + consider_wait_en + " " + consider_wait_term);


                                                load_commands(new string[] { com });

                                                
                                                consider_all_steps1 = alternately_commands[cur_alternately_line].consider_all_steps1;
                                                consider_all_steps2 = alternately_commands[cur_alternately_line].consider_all_steps2;
                                                consider_wait_all_steps_kinem = alternately_commands[cur_alternately_line].consider_wait_all_steps_kinem;
                                                consider_wait_en = alternately_commands[cur_alternately_line].consider_wait_en;
                                                consider_wait_term = alternately_commands[cur_alternately_line].consider_wait_term;
                                                consider_prog_done = alternately_commands[cur_alternately_line].consider_prog_done;
                                                //Console.WriteLine("set: " + consider_wait_all_steps_kinem + " " + consider_all_steps1 + " " + consider_all_steps2 + " " + consider_prog_done + " " + consider_wait_en + " " + consider_wait_term);
                                                cur_alternately_line++;

                                                if(consider_prog_done==1)
                                                {
                                                    Console.WriteLine("set onsider_prog_done==1");
                                                }
                                                else
                                                {
                                                    Console.WriteLine("set onsider_prog_done==0");
                                                }
                                            }
                                        }
                                        else
                                        {
                                            /*Console.WriteLine("consider_prog_done == 1 && ring_buf_en == 1 " + (consider_prog_done == 1 && ring_buf_en == 1));
                                            Console.WriteLine("printer_work: " + printer_work);
                                            Console.WriteLine("STOP; ring_buf_en:" + ring_buf_en+" "+ alternately_commands[cur_alternately_line-1].consider_prog_done+" "+(cur_alternately_line - 1));*/
                                            prog_state = programm_state.STOP;
                                        }
                                    }
                                        
                                    

                                }
                                //alternately work-----------------------------------------------------------------
                                /*if (prog_state == programm_state.ALTERNATELY)
                                {


                                    if (alternately_commands[cur_alternately_line].kinematic && printer.all_motors_stop2 && wait_en == 0 && commands1.Count == 0 && commands2.Count == 0)
                                    {
                                        //kinematic----------------------------------------------------------------------------

                                        //---------GO-----------------
                                        if ((cur_alternately_line_internal + safe_len_send_val<cur_prog_line_board  || cur_alternately_line_internal < lookup_buf - safe_len_send_val)&& cur_alternately_line_internal < stop_len)
                                        {
                                            if (alternately_commands[cur_alternately_line].len > 0)
                                            {
                                                stop_len = alternately_commands[cur_alternately_line].len;
                                            }
                                            var com = alternately_commands[cur_alternately_line].get_command(printer);
                                            Console.WriteLine(main_alternately_commands_counter + " " + main_alternately_commands.Count + " " + com+" "+ alternately_commands[cur_alternately_line].p_xyz);
                                            _TCPserver1.pushBuffer_in(com + "\n");
                                            if (alternately_commands[cur_alternately_line].movement && alternately_commands[cur_alternately_line].kinematic) cur_alternately_line_internal++;
                                            if (cur_alternately_line < alternately_commands.Count-1) cur_alternately_line++; //if (cur_alternately_line >= alternately_commands.Count) { prog_state = programm_state.STOP;}

                                        }

                                    }
                                    else
                                    {
                                        //--------direct--------------------
                                        if (wait_en==0 && printer.all_motors_stop1 && printer.all_motors_stop2 && cur_alternately_line_internal == 0 && commands1.Count == 0 && commands2.Count == 0)
                                        {
                                            //wait stop steppers
                                            var com = alternately_commands[cur_alternately_line].get_command(printer);
                                            _TCPserver1.pushBuffer_in(com + "\n");
                                            Console.WriteLine(main_alternately_commands_counter + " " + main_alternately_commands.Count + " " + com);
                                            if (alternately_commands[cur_alternately_line].wait_this_command)
                                            {
                                                printer.all_motors_stop1 = false;
                                                printer.all_motors_stop2 = false;

                                                wait_done_flag = false;
                                                all_steps_time_counter1 = 0;
                                                all_steps_time_counter2 = 0;
                                                Console.WriteLine("printer.all_motors_stop1 ");
                                            }
                                            cur_alternately_line++; if (cur_alternately_line >= alternately_commands.Count) { prog_state = programm_state.STOP; }
                                            //cur_alternately_line_internal = 0;
                                        }
                                    }
                                    //---------STOP-----------------      
                                    if (program_done_flag)
                                    {
                                        Console.WriteLine("program_done_flag cur_alternately_line: " + cur_alternately_line);
                                        //wait ring buf
                                        cur_alternately_line_internal = 0;
                                        stop_len = 21;
                                        program_done_flag = false;

                                        if (cur_alternately_line == alternately_commands.Count-1) { prog_state = programm_state.STOP; }
                                    }
                                }*/

                                //main alternately work-----------------------------------------------------------------



                                //------
                                if (main_alternately_commands_exec && prog_state == programm_state.STOP && commands_load && !printer_work && main_alternately_commands_counter < main_alternately_commands.Count && !calibrating_nossle)
                                {
                                    {
                                        if (main_alternately_commands[main_alternately_commands_counter][0].plate_num == 0)
                                        {

                                            var com = main_alternately_commands[main_alternately_commands_counter][0].get_command(printer);
                                            Console.WriteLine("main:"+main_alternately_commands_counter + " " + main_alternately_commands.Count + " " + com);
                                            exec_main_prog(com);
                                        }
                                        else
                                        {

                                            start_alternate_prog(main_alternately_commands[main_alternately_commands_counter].ToArray());
                                        }

                                        main_alternately_commands_counter++;
                                    }

                                }

                                //delta calib_handler-----------------------------------------------------------------

                                var cur_delta_calib = Convert.ToInt32(vars_from_mes[8]);//!!!!!!!cur_send ==0
                                var cur_ring_buf_en = Convert.ToInt32(vars_from_mes[9]);
                                var cur_homing = Convert.ToInt32(vars_from_mes[10]);
                                if (printer.delta_calibr_en)
                                {



                                    if (prev_homing - cur_homing == 1)
                                    {
                                        _TCPserver1.pushBuffer_in("num1 M589 Y80" + "\n");
                                        printer.delta_calibr_counter = 0;

                                        Console.WriteLine("prev_homing - cur_homing == 1");
                                    }

                                    if (prev_delta_calib - cur_delta_calib == 1 && !delta_calib_go_next_p)
                                    {
                                        Console.WriteLine("cur_delta_calib - prev_delta_calib == 1");
                                        if (printer.delta_calibr_counter == 0)
                                        {
                                            offset_frame = cur_frame;
                                        }


                                        printer.points_res_calibrate_abc[printer.delta_calibr_counter] = (long[])cur_pos.Clone();
                                        printer.delta_calibr_counter++;
                                        Console.WriteLine("calibr_counter: " + printer.delta_calibr_counter + "/ " + printer.delta_calibr_counter_max);
                                        if (printer.delta_calibr_counter >= printer.delta_calibr_counter_max)
                                        {
                                            printer.delta_calibr_en = false;
                                            printer.delta_comp_prop_calibr();
                                            _TCPserver1.pushBuffer_in("num1 M589 X180" + "\n");
                                        }
                                        var delta_orig_commands = new List<string>();
                                        delta_orig_commands.Add("G1 X" + cur_frame.p_xyz.x + " Y" + cur_frame.p_xyz.y + " Z" + cur_frame.p_xyz.z + " F1200");
                                        //delta_orig_commands.Add("G1 Z"+2*printer.points_for_calibrate_xy[printer.delta_calibr_counter].z);
                                        delta_orig_commands.Add("G1 Z" + printer.points_for_calibrate_xy[printer.delta_calibr_counter].z);
                                        delta_orig_commands.Add("G1 X" + printer.points_for_calibrate_xy[printer.delta_calibr_counter].x + " Y" + printer.points_for_calibrate_xy[printer.delta_calibr_counter].y);
                                        //prog_orig_commands g code to next point
                                        var frames_xyz_test = StepperFrame.convert_g_code_to_stepperframes(delta_orig_commands.ToArray(), printer);

                                        prog_commands = StepperFrame.convert_g_code(frames_xyz_test, ref printer, offset_frame).ToList();
                                        if (prog_commands != null)
                                        {
                                            prog_commands = StepperFrame.prepare_g_code_to_load(prog_commands.ToArray(), ref printer).ToList();
                                            max_count_cur_prog = prog_commands.Count;
                                            cur_prog_line = 0;
                                            prog_state = programm_state.MOVE;

                                            delta_calib_go_next_p = true;
                                            Console.WriteLine(delta_calib_go_next_p + " " + cur_ring_buf_en + " " + cur_prog_line + " ");
                                        }

                                    }

                                    /*if (prev_delta_calib - cur_delta_calib == 1 && !delta_calib_go_next_p)
                                    {
                                        var delta_orig_commands = new List<string>();
                                        delta_orig_commands.Add("G1 Z" + printer.points_for_calibrate_xy[printer.delta_calibr_counter].z + " F600");
                                        //prog_orig_commands g code to next point
                                        var frames_xyz = StepperFrame.convert_g_code_to_stepperframes(delta_orig_commands.ToArray(), printer);
                                        max_count_cur_prog = frames_xyz.Length;
                                        prog_commands = StepperFrame.convert_g_code(frames_xyz, printer, offset_frame).ToList();
                                        cur_prog_line = 0;
                                        prog_state = programm_state.MOVE;
                                        delta_calib_nozzle_up = false;

                                    }
                                    */
                                    if (delta_calib_go_next_p && cur_ring_buf_en == 0) Console.WriteLine(cur_prog_line + "/" + max_count_cur_prog);


                                    if (delta_calib_go_next_p && cur_ring_buf_en == 0 && cur_prog_line > max_count_cur_prog - 5)
                                    {
                                        Console.WriteLine(delta_calib_go_next_p + " " + cur_ring_buf_en + " " + cur_prog_line + " " + max_count_cur_prog / 2);
                                        Console.WriteLine("delta_calib_go_next_p && cur_ring_buf_en == 0");
                                        _TCPserver1.pushBuffer_in("num1 M589 Y80" + "\n");
                                        delta_calib_go_next_p = false;
                                    }

                                }

                                prev_delta_calib = cur_delta_calib;
                                prev_homing = cur_homing;
                                // Console.WriteLine(mes);
                                prev_pos = (long[])cur_pos.Clone();


                                //nossle calib_handler-----------------------------------------------------------------
                                if (calibrating_nossle)
                                {
                                    //var commands_load = (commands1.Count == 0 && commands2.Count == 0 && _TCPserver1.buffer_out.Length == 0 && ring_buf_en == 0);
                                    //var printer_work = (all_steps1 == 1 || all_steps2 == 1);

                                    if(commands_load && !printer_work)
                                    { //----CALIBR_X------------------------------------------------------------------------------------
                                        if (calibrate_nossle_stage_counter == 1  && prog_state == programm_state.STOP)
                                        {
                                            var prog_cur = new List<StepperFrame>();
                                            prog_cur.Add(new StepperFrame(1, 589, "A1", false));
                                            prog_cur.Add(cur_frame.clone());
                                            prog_cur.Add(new StepperFrame(new Point3d_GL(settins_string.calibrate_nossle_x[1], settins_string.calibrate_nossle_y[1], settins_string.calibrate_nossle_z[1]), 0, calibr_vel));
                                            start_alternate_prog(prog_cur.ToArray());
                                            calibrate_nossle_stage_counter = 2;

                                            Console.WriteLine("calibrate_nossle_stage_counter: " + calibrate_nossle_stage_counter);

                                        }

                                        if (calibrate_nossle_stage_counter == 2 && calibr_x == 2)
                                        {
                                            calibrate_nossle_frames.Add(cur_frame.clone());
                                            var prog_cur = new List<StepperFrame>();
                                            prog_cur.Add(new StepperFrame(1, 589, "A0", false));
                                            prog_cur.Add(cur_frame.clone());
                                            prog_cur.Add(new StepperFrame(new Point3d_GL(settins_string.calibrate_nossle_x[1], settins_string.calibrate_nossle_y[1], settins_string.calibrate_nossle_z[1]), 0, jog_xyz_vel));
                                            start_alternate_prog(prog_cur.ToArray());
                                            calibrate_nossle_stage_counter = 3;
                                            Console.WriteLine("calibrate_nossle_stage_counter: " + calibrate_nossle_stage_counter);
                                        }


                                        if (calibrate_nossle_stage_counter == 3 && prog_state == programm_state.STOP)// &&prog_state == programm_state.STOP && prog_done == 1
                                        {
                                            //calibrate_nossle_frames.Add(cur_frame.clone());
                                            var prog_cur = new List<StepperFrame>();
                                            prog_cur.Add(new StepperFrame(1, 589, "A1", false));
                                            prog_cur.Add(cur_frame.clone());
                                            prog_cur.Add(new StepperFrame(new Point3d_GL(settins_string.calibrate_nossle_x[0], settins_string.calibrate_nossle_y[0], settins_string.calibrate_nossle_z[0]), 0, calibr_vel));
                                            start_alternate_prog(prog_cur.ToArray());
                                            calibrate_nossle_stage_counter = 4;
                                            Console.WriteLine("calibrate_nossle_stage_counter: " + calibrate_nossle_stage_counter);
                                        }

                                        if (calibrate_nossle_stage_counter == 4 && calibr_x == 2)
                                        {
                                            calibrate_nossle_frames.Add(cur_frame.clone());
                                            var prog_cur = new List<StepperFrame>();
                                            prog_cur.Add(new StepperFrame(1, 589, "A0", false));
                                            prog_cur.Add(cur_frame.clone());
                                            prog_cur.Add(new StepperFrame(new Point3d_GL(settins_string.calibrate_nossle_x[0], settins_string.calibrate_nossle_y[0], settins_string.calibrate_nossle_z[0]), 0, jog_xyz_vel));
                                            //prog_cur.Add(new StepperFrame((calibrate_nossle_frames[0].p_xyz + calibrate_nossle_frames[1].p_xyz)/2, 0, calibr_vel));
                                            start_alternate_prog(prog_cur.ToArray());
                                            calibrate_nossle_stage_counter = 5;
                                            Console.WriteLine("calibrate_nossle_stage_counter: " + calibrate_nossle_stage_counter);
                                        }



                                        //----CALIBR_Y------------------------------------------------------------------------------------

                                        if (calibrate_nossle_stage_counter == 5 && prog_state == programm_state.STOP)
                                        {
                                            var prog_cur = new List<StepperFrame>();
                                            prog_cur.Add(new StepperFrame(1, 589, "B1", false));
                                            prog_cur.Add(cur_frame.clone());
                                            prog_cur.Add(new StepperFrame(new Point3d_GL(settins_string.calibrate_nossle_x[2], settins_string.calibrate_nossle_y[2], settins_string.calibrate_nossle_z[2]), 0, calibr_vel));
                                            start_alternate_prog(prog_cur.ToArray());
                                            calibrate_nossle_stage_counter = 6;
                                            Console.WriteLine("calibrate_nossle_stage_counter: " + calibrate_nossle_stage_counter);

                                        }

                                        if (calibrate_nossle_stage_counter == 6 && calibr_y == 2)
                                        {
                                            calibrate_nossle_frames.Add(cur_frame.clone());
                                            var prog_cur = new List<StepperFrame>();
                                            prog_cur.Add(new StepperFrame(1, 589, "B0", false));
                                            prog_cur.Add(cur_frame.clone());
                                            prog_cur.Add(new StepperFrame(new Point3d_GL(settins_string.calibrate_nossle_x[2], settins_string.calibrate_nossle_y[2], settins_string.calibrate_nossle_z[2]), 0, jog_xyz_vel));
                                            start_alternate_prog(prog_cur.ToArray());
                                            calibrate_nossle_stage_counter = 7;
                                            Console.WriteLine("calibrate_nossle_stage_counter: " + calibrate_nossle_stage_counter);
                                        }


                                        if (calibrate_nossle_stage_counter == 7 && prog_state == programm_state.STOP)
                                        {
                                            //calibrate_nossle_frames.Add(cur_frame.clone());
                                            var prog_cur = new List<StepperFrame>();
                                            prog_cur.Add(new StepperFrame(1, 589, "B1", false));
                                            prog_cur.Add(cur_frame.clone());
                                            prog_cur.Add(new StepperFrame(new Point3d_GL(settins_string.calibrate_nossle_x[0], settins_string.calibrate_nossle_y[0], settins_string.calibrate_nossle_z[0]), 0, calibr_vel));
                                            start_alternate_prog(prog_cur.ToArray());
                                            calibrate_nossle_stage_counter = 8;
                                            Console.WriteLine("calibrate_nossle_stage_counter: " + calibrate_nossle_stage_counter);
                                        }

                                        if (calibrate_nossle_stage_counter == 8 && calibr_y == 2)
                                        {
                                            calibrate_nossle_frames.Add(cur_frame.clone());
                                            var prog_cur = new List<StepperFrame>();
                                            prog_cur.Add(new StepperFrame(1, 589, "B0", false));
                                            prog_cur.Add(cur_frame.clone());
                                            var fr_z_cal = new StepperFrame((calibrate_nossle_frames[2].p_xyz + calibrate_nossle_frames[3].p_xyz) / 2, 0, jog_xyz_vel);
                                            calibrate_nossle_frames.Add(fr_z_cal.clone());
                                            fr_z_cal.p_xyz.z += 15;
                                            //prog_cur.Add(new StepperFrame(new Point3d_GL(settins_string.calibrate_nossle_x[0], settins_string.calibrate_nossle_y[0], settins_string.calibrate_nossle_z[0]), 0, jog_xyz_vel));
                                            prog_cur.Add(fr_z_cal.clone());
                                            start_alternate_prog(prog_cur.ToArray());


                                            calibrate_nossle_stage_counter = 9;
                                            Console.WriteLine("calibrate_nossle_stage_counter: " + calibrate_nossle_stage_counter);
                                        }


                                        //----CALIBR_Z------------------------------------------------------------------------------------

                                        if (calibrate_nossle_stage_counter == 9 && prog_state == programm_state.STOP)
                                        {
                                            var prog_cur = new List<StepperFrame>();
                                            prog_cur.Add(new StepperFrame(1, 589, "C1", false));
                                            prog_cur.Add(cur_frame.clone());
                                            prog_cur.Add(new StepperFrame(calibrate_nossle_frames[calibrate_nossle_frames.Count - 1].p_xyz, 0, calibr_vel));
                                            start_alternate_prog(prog_cur.ToArray());
                                            calibrate_nossle_stage_counter = 10;
                                            Console.WriteLine("calibrate_nossle_stage_counter: " + calibrate_nossle_stage_counter);

                                        }

                                        if (calibrate_nossle_stage_counter == 10 && calibr_z == 2)
                                        {
                                            calibrate_nossle_frames.Add(cur_frame.clone());
                                            var prog_cur = new List<StepperFrame>();
                                            prog_cur.Add(new StepperFrame(1, 589, "C0", false));
                                            var cur_fr_dz = cur_frame.p_xyz.Clone();
                                            cur_fr_dz.z += 30;
                                            prog_cur.Add(cur_frame.clone());
                                            prog_cur.Add(new StepperFrame(cur_fr_dz, 0, jog_xyz_vel));

                                            var fr_x_cal = (calibrate_nossle_frames[0].p_xyz + calibrate_nossle_frames[1].p_xyz) / 2;
                                            var fr_y_cal = (calibrate_nossle_frames[2].p_xyz + calibrate_nossle_frames[3].p_xyz) / 2;
                                            offset_nossle.p_xyz.x = fr_x_cal.x;
                                            offset_nossle.p_xyz.y = fr_y_cal.y;
                                            offset_nossle.p_xyz.z = cur_frame.p_xyz.z;
                                            start_alternate_prog(prog_cur.ToArray());
                                            calibrating_nossle = false;
                                            auto_calibr = true;
                                            comp_offset();
                                            Console.WriteLine("calibrate done");
                                        }






                                        /*if (calibrate_nossle_stage_counter >1 && prog_state == programm_state.STOP)
                                        {
                                            Console.WriteLine("calibrate failed stage_counter == "+ calibrate_nossle_stage_counter);

                                            calibrating_nossle = false;
                                        }*/

                                    }


                                }


                            }
                            //catch
                            {

                            }
                        }
                        // Console.WriteLine(commands1.Count);
                        if (commands1.Count > 0)
                        {

                            // var cur_num_board = (long)Convert.ToInt32(vars_from_mes[1]);
                            var cur_num_ins = commands1[0].num - count_send1;
                            //Console.WriteLine("send1 com pre: " + cur_num_board + "/" + cur_num_ins+" "+ count_send1 + " " + commands1[0].com);

                            if (!initing1)
                            {
                                initing1 = true;
                                count_send1 = commands1[0].num - cur_num_board - 1;
                                cur_num_ins = commands1[0].num - count_send1;
                                //Console.WriteLine("init 1 f:" + dtime + " " + (last_time_1 - start_time));
                                //Console.WriteLine("count_send1 " + count_send1 + ";cur_num_ins " + cur_num_ins + "; cur_num_board " + cur_num_board + "; commands1[0].num " + commands1[0].num);
                            }

                            if (cur_num_ins - 1 == cur_num_board)
                            {
                                var com_cur = "N" + cur_num_ins + " " + commands1[0].com;
                                var mes_out = Encoding.ASCII.GetBytes(com_cur);
                                udp_client1.Send(mes_out, mes_out.Length);

                                //Console.WriteLine("send1 com: " + cur_num_board + "/" + cur_num_ins + " " + com_cur+" "+mes);
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

                if (udp_client2 != null)
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
                        //Console.WriteLine("res2: " + mes);

                        if (res != null && !mes.Contains('M'))
                        {
                            var vars_from_mes = mes.Split(' ');
                            var cur_num_board = (long)Convert.ToInt32(vars_from_mes[1]);

                            //Console.WriteLine(vars_from_mes.Length);
                            if (vars_from_mes.Length >= 9)
                            {
                                //try
                                {

                                    var cur_send = (long)Convert.ToInt32(vars_from_mes[3]);
                                    if (vars_from_mes[4].Length == 5)
                                    {
                                        //Console.WriteLine("2:" + vars_from_mes[4]);
                                        //wait_term = Convert.ToInt32(vars_from_mes[4][3]) - 48;
                                        wait_term = Convert.ToInt32(vars_from_mes[4][4]) - 48;
                                        all_steps2 = Convert.ToInt32(vars_from_mes[4][0]) - 48;
                                    }
                                    

                                }
                                //catch
                                {

                                }
                            }
                            if (_TCPserver1.connected)
                            {
                                _TCPserver1.pushBuffer(mes);
                            }

                            if (commands2.Count > 0 && !mes.Contains('M') && !mes.Contains('N'))
                            {

                                //var cur_num_board = (long)Convert.ToInt32(mes.Split(' ')[1]);
                                //Console.WriteLine("send1 com: " + cur_num_board + "/" + count_send1 + " " + coms1[0]);
                                var cur_num_ins = commands2[0].num - count_send2;
                                //Console.WriteLine("send2 com pre: " + cur_num_board + "/" + cur_num_ins + " " + count_send2 + " " + commands2[0].com);
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

                                    //Console.WriteLine("send2 com: " + cur_num_board + "/" + cur_num_ins + " " + com_cur);
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


            }
        

        public void load_settings()
        {
            settins_string = load_obj<SettingsString>("settings_printer.json");


            // settins_string.soft_max_pos2 = new int[2];
            /*int ps_take = 10;

            settins_string.take_right_manip_rot = new int[ps_take];
            settins_string.take_right_manip_vert = new int[ps_take];

            settins_string.take_right_manip_x = new double[ps_take];
            settins_string.take_right_manip_y = new double[ps_take];
            settins_string.take_right_manip_z = new double[ps_take];*/

           /* int ps_take = 10;
            settins_string.offset_plate_x = new double[ps_take];
            settins_string.offset_plate_y = new double[ps_take];
            settins_string.offset_plate_z = new double[ps_take];

            settins_string.calibrate_nossles_offset_z = new double[ps_take];*/


            for (int i = 0; i<settins_string.motors_count1;i++)
            {
                
                _TCPserver1.pushBuffer_in("num1 M587" + 
                    " I" + i + 
                    " A" + Math.Round(settins_string.a_max1[i],3) +
                    " V" + Math.Round(settins_string.v_def1[i], 3) +
                    " E" + settins_string.end_inv1[i] +
                    " D" + settins_string.motor_dir1[i] +
                    " R" + settins_string.steps_per_mm1[i] +
                    " K" + settins_string.home_dir1[i] +
                    " B" + settins_string.home_pos1[i] +
                    "\n");
            }

            for (int i = 0; i < settins_string.motors_count2; i++)
            {
                _TCPserver1.pushBuffer_in("num2 M587" +
                    " I" + i +
                    " A" + Math.Round(settins_string.a_max2[i], 3) +
                    " V" + Math.Round(settins_string.v_def2[i], 3) +
                    " E" + settins_string.end_inv2[i] +
                    " D" + settins_string.motor_dir2[i] +
                    " R" + settins_string.steps_per_mm2[i] +
                    " K" + settins_string.home_dir2[i] +
                    " B" + settins_string.home_pos2[i] +
                    "\n");
            }


            _TCPserver1.pushBuffer_in("num1 M581 I2 A0");
            save_obj("settings_printer.json", settins_string);

        }
        static int val_from_command(string cmd)
        {
            var command_af = cmd.Replace("  ", " ");
            var vars = command_af.Trim().Split(' ');
            var var = Convert.ToInt32(vars[1]);
            return var;
        }
        static double val_from_command_d(string cmd)
        {
            var command_af = cmd.Replace("  ", " ");
            var vars = command_af.Trim().Split(' ');
            var var = Convert.ToDouble(vars[1]);
            return var;
        }

        static double[] vals_from_command_d(string cmd, int len)
        {
            var command_af = cmd.Replace("  ", " ");
            var vars = command_af.Trim().Split(' ');
            var vals = new double[len];
            for(int i=0;  i<len; i++)
            {
                vals[i] = Convert.ToDouble(vars[1+i]);
            }
            
            return vals;
        }


        static int[] vals_from_command(string cmd, int len)
        {
            var command_af = cmd.Replace("  ", " ");
            var vars = command_af.Trim().Split(' ');
            var vals = new int[len];
            for (int i = 0; i < len; i++)
            {
                vals[i] = Convert.ToInt32(vars[1 + i]);
            }

            return vals;
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

    public class SettingsString
    {
        //планировщик
        public double max_acs;
        public double max_r;
        public double min_dist;

        //настройки моторов
        public int motors_count1 = 8;

        public double[] a_max1;
        public double[] v_def1;
        public int[] end_inv1;
        public int[] motor_dir1;
        public int[] steps_per_mm1;
        public int[] home_dir1;
        public int[] home_pos1;
        public int[] soft_max_pos1;
        public int[] soft_min_pos1;

        public int motors_count2 = 8;

        public double[] a_max2;
        public double[] v_def2;
        public int[] end_inv2;
        public int[] motor_dir2;
        public int[] steps_per_mm2;
        public int[] home_dir2;
        public int[] home_pos2;
        public int[] soft_max_pos2;
        public int[] soft_min_pos2;
        //------------------------------------------
        public int[] take_left_manip_rot;
        public int[] take_left_manip_vert;

        public double[] take_left_manip_x;
        public double[] take_left_manip_y;
        public double[] take_left_manip_z;

        //------------------------------------------
        public int[] take_right_manip_rot;
        public int[] take_right_manip_vert;

        public double[] take_right_manip_x;
        public double[] take_right_manip_y;
        public double[] take_right_manip_z;


        //------------------------------------------

        public double[] calibrate_nossle_x;
        public double[] calibrate_nossle_y;
        public double[] calibrate_nossle_z;

        public double[] calibrate_nossles_offset_z;
        //------------------------------------------

        public double[] offset_plate_x;
        public double[] offset_plate_y;
        public double[] offset_plate_z;

        
        //------------------------------------------

        public int table_work_pos   = 0;
        public int table_change_pos = 0;
        public int table_open_pos = 0;
        public int lift_up_val      = 0;

        public int servo_open_val = 0;
        public int servo_close_val = 0;

        public int osc_rot_start_pos1 = 0;
        public int osc_rot_start_pos2 = 0;



        public SettingsString()
        {
            a_max1 =  new double[motors_count1];
            v_def1 =  new double[motors_count1];
            end_inv1 =  new int[motors_count1];
            motor_dir1 = new int[motors_count1];
            steps_per_mm1 = new int[motors_count1];
            home_dir1 =  new int[motors_count1];
            home_pos1 =  new int[motors_count1];

            a_max2 =  new double[motors_count2];
            v_def2 =  new double[motors_count2];
            end_inv2 =  new int[motors_count2];
            motor_dir2 = new int[motors_count2];
            steps_per_mm2 = new int[motors_count2];
            home_dir2 =  new int[motors_count2];
            home_pos2 =  new int[motors_count2];

            for (int i = 0; i < motors_count1; i++)
            {
                a_max1[i]     = 2;
                v_def1[i]     = 2;
                end_inv1[i]   = 0;
                motor_dir1[i] = 1;
                steps_per_mm1[i] = 100;
                home_dir1[i]  = 1;
                home_pos1[i]  = 0;
            }

            for (int i = 0; i < motors_count2; i++)
            {
                a_max2[i]     = 2;
                v_def2[i]     = 2;
                end_inv2[i]   = 0;
                motor_dir2[i] = 1;
                steps_per_mm2[i] = 100;
                home_dir2[i]  = 1;
                home_pos2[i]  = 0;
            }
        }

        public void apply_planner_settings()
        {
            StepperPrinter.min_dist = min_dist;
            StepperPrinter.printer_max_r = max_r;
            StepperPrinter.printer_max_acs = max_acs;
        }

    }


}

