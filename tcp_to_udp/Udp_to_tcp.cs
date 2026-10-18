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

            }
            else if (command.Contains("M595"))
            {
                var val = val_from_command(com_board);

            }

            else if (command.Contains("M596")) // prog load
            {
                var com_re = com_board.Replace("M596 ", "").Trim();
                printer.prog_orig_commands.Add(com_re);

            }

            else if (command.Contains("M597"))// prog control
            {
                var val = val_from_command(com_board);
                Console.WriteLine("M597 val: " + val);
                if (val == 0)
                {
                    var frames_xyz_list = printer.convert_g_code_to_stepperframes(printer.prog_orig_commands.ToArray(), printer).ToList();
                    if (frames_xyz_list != null)
                    {
                        printer.start_main_alternate_prog(frames_xyz_list.ToArray());
                    }


                }
                else if (val == 1)
                {
                    printer.prog_state = StepperPrinter.programm_state.PAUSE;
                }
                else
                {
                    //load_commands(new string[] { "num1 M588 A0" });
                    load_commands(new string[] { "num1 M589 S" });
                    printer.prog_state = StepperPrinter.programm_state.STOP;
                }
            }
            else if (command.Contains("M598")) // prog clear
            {
                printer.prog_commands = new List<StepperFrame>();
                printer.prog_orig_commands = new List<string>();
            }
            else if (command.Contains("M610"))//set jog vel
            {
                var val = val_from_command_d(com_board);
                printer.jog_xyz_vel = val;
            }

            else if (command.Contains("M611") && printer.all_steps_kinem == 0)//jog 
            {

                var val = val_from_command(com_board);
                var jog_orig = new List<StepperFrame>();
                var fr_cur = new StepperFrame(new Point3d_GL(0, 0, 0), 0, 0);
                fr_cur.vel = printer.jog_xyz_vel;
                jog_orig.Add(fr_cur);
                var fr_jog = fr_cur.clone();
                fr_jog.p_xyz = fr_jog.p_xyz.add_mask(val, printer.jog_len);
                jog_orig.Add(fr_jog);

                printer.cur_coordinate_system = StepperPrinter.coordinate_system.CURRENT;
                //offset_frame.p_xyz = new Point3d_GL(0, 0, 0);
                printer.start_main_alternate_prog(jog_orig.ToArray());


            }
            else if (command.Contains("M612"))//set_zero
            {
                var val = vals_from_command_d(com_board,8);
                //printer.offset_zero_pos[printer.tool_active] = 

                var cur_fr = printer.cur_frame.clone();
                var x = val[0];
                var y = val[1];
                var z = val[2];
                var e = val[3];

                var x_en = val[4];
                var y_en = val[5];
                var z_en = val[6];
                var e_en = val[7];

                if (x_en == 1) printer.offset_zero_pos[printer.tool_active].p_xyz.x = cur_fr.p_xyz.x - x; 
                if (y_en == 1) printer.offset_zero_pos[printer.tool_active].p_xyz.y = cur_fr.p_xyz.y - y;
                if (z_en == 1) printer.offset_zero_pos[printer.tool_active].p_xyz.z = cur_fr.p_xyz.z - z;

                printer.tool_calibrated_manual[printer.tool_active] = true;

            }
            else if (command.Contains("M613"))//
            {

                var val = val_from_command(com_board);
                //
                if (val <= 4) { val = 4; printer.delta_init_calibr(StepperPrinter.delta_calibr_ps_count.ps4); }
                else { val = 18; printer.delta_init_calibr(StepperPrinter.delta_calibr_ps_count.ps18); }

                load_commands(new string[] { "main M589 X80" });
               

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
                                        new StepperFrame(printer.cur_frame.p_xyz-printer.offset_frame.p_xyz,0,printer.jog_xyz_vel),
                                        new StepperFrame(new Point3d_GL(),0,printer.jog_xyz_vel),
                };
                printer.start_main_alternate_prog(frames_xyz_list.ToArray());

            }
            else if (command.Contains("M616"))//remember_p 
            {
                var val = val_from_command(com_board);
                if (val >= 0 && val < printer.bed_calib_ps.Length)
                {
                    printer.bed_calib_ps[val] = printer.cur_frame;
                }

                if (val < 0)
                {
                    printer.bed_calib_vec = new Point3d_GL(0, 0, 1);
                }

                if (val > printer.bed_calib_ps.Length)
                {

                    var p1 = printer.bed_calib_ps[0].p_xyz;
                    var p2 = printer.bed_calib_ps[1].p_xyz;
                    var p3 = printer.bed_calib_ps[2].p_xyz;
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
                printer.cur_coordinate_system = StepperPrinter.coordinate_system.INTERNAL; 
                printer.offset_frame = printer.comp_offset(printer.cur_coordinate_system);
                StepperFrame[] prog_cur = null;
                prog_cur = StepperPrinter.gen_change_prog(val, printer.settins_string, printer.cur_frame.clone(),printer.safe_z_internal);
                
                if(prog_cur!=null) printer.start_alternate_prog(prog_cur);
            }

            else if (command.Contains("M620")) // take i tool
            {
                printer.cur_coordinate_system = StepperPrinter.coordinate_system.INTERNAL;
                printer.offset_frame = printer.comp_offset(printer.cur_coordinate_system);
                var prog_cur = new List<StepperFrame>();

                var val = val_from_command(com_board);
                var tool_dest = val;
                
                var manip_left = printer.tool_inds[0];
                var manip_right = printer.tool_inds[1];

                var last_frame = printer.cur_frame.clone();

                if (manip_left != tool_dest && manip_right != tool_dest && printer.tool_active != tool_dest) Console.WriteLine("Вставте экструдер: " + tool_dest);
                if (printer.tool_active != 0)
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
                        Console.WriteLine("Нет свободного места: Текущий экструдер: " + printer.tool_active + "; Манипулятор левый: " + manip_left + "; Манипулятор правый: " + manip_right);
                        printer.main_alternately_commands_exec = false;
                        printer.prog_state = StepperPrinter.programm_state.STOP;
                          
                        return;

                    }
                    else
                    {
                        prog_cur.AddRange(StepperPrinter.gen_change_prog(prog_num_give, printer.settins_string, last_frame.clone(), printer.safe_z_internal));
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
                if (prog_cur != null)  prog_cur.AddRange(StepperPrinter.gen_change_prog(prog_num_take, printer.settins_string, last_frame.clone(), printer.safe_z_internal));

                if (prog_cur != null)
                {
                    printer.tool_active = tool_dest;
                    printer.start_alternate_prog(prog_cur.ToArray());
                }
                
            }

            else if (command.Contains("M621")) //drop active tool
            {
                printer.cur_coordinate_system = StepperPrinter.coordinate_system.INTERNAL;
                printer.offset_frame = printer.comp_offset(printer.cur_coordinate_system);
                var prog_cur = new List<StepperFrame>();
                var last_frame = printer.cur_frame.clone();
                int prog_num_give = -1;
                var manip_left = printer.tool_inds[0];
                var manip_right = printer.tool_inds[1];
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
                    Console.WriteLine("Нет свободного места: Текущий экструдер: " + printer.tool_active + "; Манипулятор левый: " + manip_left + "; Манипулятор правый: " + manip_right);
                    printer.main_alternately_commands_exec = false;
                    printer.prog_state = StepperPrinter.programm_state.STOP;
                    return;
                }
                else
                {
                    prog_cur.AddRange(StepperPrinter.gen_change_prog(prog_num_give, printer.settins_string, last_frame.clone(), printer.safe_z_internal) );
                    if (prog_cur != null) printer.start_alternate_prog(prog_cur.ToArray());
                }
            }

            else if (command.Contains("M622"))
            {



            }

            else if (command.Contains("M623"))//set bring tablet
            {

                var prog_cur = new List<StepperFrame>();
                prog_cur.Add(new StepperFrame(2, 577, "I0 V" + printer.settins_string.servo_open_val, false));
                prog_cur.Add(new StepperFrame(2, 577, "I1 V" + (180 - printer.settins_string.servo_open_val), false));
                prog_cur.Add(new StepperFrame(2, 587, "I0 P" + printer.settins_string.table_change_pos + " L", false));
                prog_cur.Add(new StepperFrame(2, 587, "I2 P" + printer.settins_string.lift_up_val + " L", false));
                prog_cur.Add(new StepperFrame(2, 577, "I0 V" + printer.settins_string.servo_close_val, false));
                prog_cur.Add(new StepperFrame(2, 577, "I1 V" + (180 - printer.settins_string.servo_close_val), false));
                prog_cur.Add(new StepperFrame(2, 587, "I2 H"));
                prog_cur.Add(new StepperFrame(2, 587, "I0 P" + printer.settins_string.table_work_pos + " L", false));
                printer.start_alternate_prog(prog_cur.ToArray());
            }

            else if (command.Contains("M624"))//set give tablet
            {

                var prog_cur = new List<StepperFrame>();
                prog_cur.Add(new StepperFrame(2, 587, "I0 P" + printer.settins_string.table_change_pos + " L", false));
                prog_cur.Add(new StepperFrame(2, 587, "I2 P" + printer.settins_string.lift_up_val + " L", false));
                prog_cur.Add(new StepperFrame(2, 577, "I0 V" + printer.settins_string.servo_open_val, false));
                prog_cur.Add(new StepperFrame(2, 577, "I1 V" + (180 - printer.settins_string.servo_open_val), false));
                prog_cur.Add(new StepperFrame(2, 587, "I2 H", false));
                prog_cur.Add(new StepperFrame(2, 587, "I0 P" + printer.settins_string.table_open_pos + " L", false));
                printer.start_alternate_prog(prog_cur.ToArray());
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

                prog_cur.Add(new StepperFrame(2, 587, "I4 P" + printer.settins_string.take_left_manip_rot[4] + " L", false, 0));
                prog_cur.Add(new StepperFrame(2, 587, "I3 P100 L", false, 0));
                prog_cur.Add(new StepperFrame(2, 587, "I6 P" + printer.settins_string.take_right_manip_rot[4] + " L", false, 0));
                prog_cur.Add(new StepperFrame(2, 587, "I5 P100 L", false));

                printer.start_alternate_prog(prog_cur.ToArray());

            }
            else if (command.Contains("M631"))//set xy_calibrate
            {
                if (!printer.tool_calibrated_auto[printer.tool_active])
                {
                    printer.cur_coordinate_system = StepperPrinter.coordinate_system.INTERNAL;
                    //printer.offset_frame.p_xyz = new Point3d_GL(0, 0, 0);
                    double save_dist_nossle = 10;
                    printer.calibrate_nossle_stage_counter = 0;

                    printer.calibrating_nossle = true;
                    var prog_cur = new List<StepperFrame>();
                    var cur_fr_dest = printer.cur_frame.clone();
                    cur_fr_dest.vel = printer.jog_xyz_vel;
                    prog_cur.Add(cur_fr_dest.clone());
                    if (cur_fr_dest.p_xyz.z < printer.settins_string.calibrate_nossle_z[0] + save_dist_nossle)
                    {
                        cur_fr_dest.p_xyz.z += save_dist_nossle;
                    }
                    //add offset for different nossle    [cur_nossle]
                    prog_cur.Add(cur_fr_dest.clone());
                    prog_cur.Add(new StepperFrame(new Point3d_GL(printer.settins_string.calibrate_nossle_x[0], printer.settins_string.calibrate_nossle_y[0], printer.settins_string.calibrate_nossle_z[0] + save_dist_nossle), 0, printer.jog_xyz_vel));
                    prog_cur.Add(new StepperFrame(new Point3d_GL(printer.settins_string.calibrate_nossle_x[0], printer.settins_string.calibrate_nossle_y[0], printer.settins_string.calibrate_nossle_z[0]), 0, printer.jog_xyz_vel));
                    printer.start_alternate_prog(prog_cur.ToArray());
                    printer.calibrate_nossle_stage_counter = 1;
                    printer.calibrate_nossle_frames = new List<StepperFrame>();
                    Console.WriteLine("calibrate_nossle_stage_counter: " + printer.calibrate_nossle_stage_counter);
                }
            }
            else if (command.Contains("M632"))
            {
                var val = val_from_command(com_board);
                

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
                prog_cur.Add(new StepperFrame(1, 587, "I4 P"+ printer.settins_string.osc_rot_start_pos1+" L", false, 0));
                prog_cur.Add(new StepperFrame(1, 587, "I5 P-50 L", false, 0));
                prog_cur.Add(new StepperFrame(1, 587, "I6 P"+ printer.settins_string.osc_rot_start_pos2+" L", false));

                printer.start_alternate_prog(prog_cur.ToArray());

            }

            else if (command.Contains("M635"))//home osc
            {

                var prog_cur = new List<StepperFrame>();

                //prog_cur.Add(new StepperFrame(1, 589, "X10"));
                prog_cur.Add(new StepperFrame(0, 631, ""));

                prog_cur.Add(new StepperFrame(0, 637, "1"));
                //prog_cur.Add(new StepperFrame(0, 638, "-41.8732 67.1863 -394.5207"));
                //prog_cur.Add(new StepperFrame(0, 632, "1"));
                prog_cur.Add(new StepperFrame(new Point3d_GL(0,0,50),0, printer.jog_xyz_vel));
                prog_cur.Add(new StepperFrame(new Point3d_GL(0, 10, 50), 0, printer.jog_xyz_vel));
                prog_cur.Add(new StepperFrame(new Point3d_GL(10, 10, 50), 0, printer.jog_xyz_vel));
                prog_cur.Add(new StepperFrame(new Point3d_GL(10, 0, 50), 0, printer.jog_xyz_vel));
                prog_cur.Add(new StepperFrame(new Point3d_GL(0, 0, 50), 0, printer.jog_xyz_vel));
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

                printer.start_main_alternate_prog(prog_cur.ToArray());

            }


            else if (command.Contains("M636"))
            {
                var val = val_from_command(com_board);
                printer.cur_nossle_type = val;

            }

            else if (command.Contains("M637"))
            {
                var val = val_from_command(com_board);
                printer.cur_plate_type = val;

            }


            else if (command.Contains("M638"))
            {
                var val = vals_from_command_d(com_board, 3);
                printer.offset_nossle[printer.tool_active].p_xyz = new Point3d_GL(val[0], val[1], val[2]);

            }
            else if (command.Contains("M639"))
            {
                var val = val_from_command(com_board);
                printer.tool_active = val;

            }
            else if (command.Contains("M640"))
            {
                var val = val_from_command(com_board);
                switch(val)
                {
                    case 0: printer.cur_coordinate_system = StepperPrinter.coordinate_system.MANUAL; break;
                    case 1: printer.cur_coordinate_system = StepperPrinter.coordinate_system.AUTO; break;
                    case 2: printer.cur_coordinate_system = StepperPrinter.coordinate_system.CURRENT; break;
                    case 3: printer.cur_coordinate_system = StepperPrinter.coordinate_system.INTERNAL; break;
                }
            }
            else if (command.Contains("M641"))
            {
                var val = val_from_command(com_board);
                switch (val)
                {
                    case 0: printer.dest_coordinate_system = StepperPrinter.coordinate_system.MANUAL; printer.cur_coordinate_system = StepperPrinter.coordinate_system.MANUAL; Console.WriteLine(printer.cur_coordinate_system); break;
                    case 1: printer.dest_coordinate_system = StepperPrinter.coordinate_system.AUTO; printer.cur_coordinate_system = StepperPrinter.coordinate_system.AUTO; break;
                }
            }
            else if (command.Contains("M642"))
            {
                var val = val_from_command(com_board);
                switch (val)
                {
                    case 0: printer.table_out = false; break;
                    case 1: printer.table_out = true; break;
                }
            }
            else if (command.Contains("M643"))
            {
                var val = val_from_command(com_board);
                switch (val)
                {
                    case 0: printer.cover_up = false; break;
                    case 1: printer.cover_up = true; break;
                }
            }




            else if (command.Contains("M700"))//set all stop
            {
                printer.prog_state =  StepperPrinter.programm_state.STOP;
                _TCPserver1.pushBuffer_in("num1 M589 S" + "\n");
                _TCPserver1.pushBuffer_in("num2 M589 S" + "\n");
                printer.calibrating_nossle = false;
                printer.main_alternately_commands_counter = printer.main_alternately_commands.Count + 1;
            }

            else if (command.Contains("M701"))//stop condition
            {
                var vals = vals_from_command(com_board, 6);

                printer.consider_wait_all_steps_kinem = vals[0];
                printer.consider_all_steps1 = vals[1];
                printer.consider_all_steps2 = vals[2];
                printer.consider_prog_done = vals[3];
                printer.consider_wait_en = vals[4];
                printer.consider_wait_term = vals[5];


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

            /*var prog_orig_commands = new List<string>()
            {
                "G92 X0 Y0 Z0 E0",
                "G1 X0 Y0 F600",
                "G1 X10 E0.1",
                "G1 X10 Y10 E0.2",
                "G1 X0 Y10 E0.3",
                "G1 X0 Y0 E0.4 F600",
                "G91",
                "G1 Z-10",
                "G90",
                "G92 X0 Y0 Z0 E0",
                "G1 X0 Y0 F600",
                "G1 X10 E0.1",
                "G1 X10 Y10 E0.2",
                "G1 X0 Y10 E0.3",
                "G1 X0 Y0 E0.4 F600",
            };

            var frames_xyz_test_2 = StepperFrame.convert_g_code_to_stepperframes(prog_orig_commands.ToArray(), printer);*/
            //prog_commands = StepperFrame.convert_g_code(frames_xyz_test_2, ref printer, offset_frame).ToList();

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
                            //var mes_serv = device_numb + "" + string_is_ending + "" + pound_is_ending;
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
                            var frame_out = printer.cur_frame.p_xyz.Clone();
                            //frame_out.z -= printer.comp_off_bed(frame_out);
                            
                            var tools = "00000";
                            if (printer.tool_inds.Length == 4) tools = printer.tool_inds[0] + "" + printer.tool_inds[1] + "" + printer.tool_inds[2] + "" + printer.tool_inds[3] + "" + printer.tool_active;

                            var offs_intern = printer.comp_offset(StepperPrinter.coordinate_system.INTERNAL).p_xyz;

                            //Console.WriteLine(offs_intern);

                            var frame_out_cur = frame_out.Clone() - printer.offset_frame.p_xyz;
                            var frame_out_mash = frame_out.Clone() - offs_intern;
                            var frame_out_tool = frame_out.Clone() - printer.comp_offset(printer.dest_coordinate_system).p_xyz;

                            _TCPserver1.pushBuffer(mes + " " + tools + " " + frame_out_cur.ToString() + " " + frame_out_mash.ToString() + " " + frame_out_tool.ToString() +" "+printer.cur_coordinate_system+ "\n");
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
                                    printer.all_steps1 = Convert.ToInt32(vars_from_mes[4][0]) - 48;
                                    printer.all_steps_kinem = Convert.ToInt32(vars_from_mes[4][1]) - 48;
                                    printer.ring_buf_en = Convert.ToInt32(vars_from_mes[4][2]) - 48;
                                    printer.wait_en = Convert.ToInt32(vars_from_mes[4][3]) - 48;

                                    //Console.WriteLine("1:" + vars_from_mes[4]);
                                }

                                if (cur_send == 0)
                                {

                                    //ring_en = Convert.ToInt32(vars_from_mes[6]);

                                    

                                    //ring_buf_en = Convert.ToInt32(vars_from_mes[6]);


                                    var nossle_calib_vals = vars_from_mes[10];
                                    printer.calibr_x = Convert.ToInt32(nossle_calib_vals[0]) - 48;
                                    printer.calibr_y = Convert.ToInt32(nossle_calib_vals[1]) - 48;
                                    printer.calibr_z = Convert.ToInt32(nossle_calib_vals[2]) - 48;

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

                                        printer.tool_inds = new int[] { 0, 0, 0, 0 };

                                        if (tool0_0 == 0) printer.tool_inds[0] = 1;
                                        if (tool0_1 == 0) printer.tool_inds[0] = 2;
                                        if (tool0_2 == 0) printer.tool_inds[0] = 3;

                                        if (tool1_0 == 0) printer.tool_inds[1] = 1;
                                        if (tool1_1 == 0) printer.tool_inds[1] = 2;
                                        if (tool1_2 == 0) printer.tool_inds[1] = 3;

                                        if (tool2_0 == 0) printer.tool_inds[2] = 1;
                                        if (tool3_0 == 0) printer.tool_inds[3] = 1;


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


                                    printer.cur_pos = new long[] { cur_poses[0], cur_poses[1], cur_poses[2], cur_poses[7] };

                                    printer.cur_frame = printer.solve_fk(printer.cur_pos);



                                    //Console.WriteLine(cur_frame.e);
                                    printer.cur_frame.p_xyz.z -= printer.comp_off_bed(printer.cur_frame.p_xyz);
                                    /**/
                                }


                                var commands_load = (commands1.Count == 0 && commands2.Count == 0 && _TCPserver1.buffer_out.Length == 0);
                                var printer_work =
                                    (printer.consider_all_steps1 == 1 && printer.all_steps1 == 1) ||
                                    (printer.consider_all_steps2 == 1 && printer.all_steps2 == 1) ||
                                    (printer.consider_wait_all_steps_kinem == 1 && printer.all_steps_kinem == 1) ||
                                    (printer.consider_prog_done == 1 && printer.ring_buf_en == 1) ||
                                    (printer.consider_wait_en == 1 && printer.wait_en == 1) ||
                                    (printer.consider_wait_term == 1 && printer.wait_term == 1);


                                //prog_work-----------------------------------------------------------------
                                /*if (prog_state == programm_state.MOVE && (cur_prog_line + safe_len_send_val < cur_prog_line_board || cur_prog_line < lookup_buf - safe_len_send_val))
                                {
                                    //Console.WriteLine(mes);
                                    if (cur_prog_line < prog_commands?.Count)
                                    {
                                        _TCPserver1.pushBuffer_in(prog_commands[cur_prog_line].get_command(printer) + "\n");
                                        cur_prog_line++;



                                    }
                                }

                                //jog work-----------------------------------------------------------------
                                if ((prog_state == programm_state.JOG && (cur_jog_line + safe_len_send_val < cur_prog_line_board || cur_jog_line < lookup_buf - safe_len_send_val))&& commands_load)
                                {
                                    if (cur_jog_line < jog_commands?.Count)
                                    {

                                        var com = jog_commands[cur_jog_line].get_command(printer);
                                        load_commands(new string[] { com });
                                        //Console.WriteLine("cur_prog_line_board: " + cur_prog_line_board + "; cur_jog_line: " + cur_jog_line+"/"+ jog_commands?.Count+"; "+com);

                                        cur_jog_line++;
                                    }
                                }*/

                                //alternately work-----------------------------------------------------------------
                                if (printer.prog_state ==  StepperPrinter.programm_state.ALTERNATELY)
                                {
                                    
                                    //Console.WriteLine("cur: "+all_steps_kinem + " " + all_steps1 + " " + all_steps2 + " " + ring_buf_en + " " + wait_en + " " + wait_term);
                                    //Console.WriteLine("con: " + consider_wait_all_steps_kinem + " " + consider_all_steps1 + " " + consider_all_steps2 + " " + consider_prog_done + " " + consider_wait_en + " " + consider_wait_term);
                                    if (commands_load && !printer_work)
                                    {
                                        if (printer.cur_alternately_line < printer.alternately_commands.Count)
                                        {

                                            var cur_alternately_line_internal = printer.alternately_commands[printer.cur_alternately_line].line_number;
                                            var printer_ready_kinem = ((cur_alternately_line_internal + printer.safe_len_send_val < cur_prog_line_board || cur_alternately_line_internal < printer.lookup_buf - printer.safe_len_send_val));// && cur_alternately_line_internal < stop_len

                                            if (printer_ready_kinem || !printer.alternately_commands[printer.cur_alternately_line].kinematic)
                                            {
                                                if (printer.alternately_commands[printer.cur_alternately_line].reset_extr) printer.abs_pos_extr = 0;
                                                var com = printer.alternately_commands[printer.cur_alternately_line].get_command(printer);

                                                //Console.WriteLine("alt: "+cur_alternately_line+"/" + alternately_commands.Count + " " + com);
                                                //Console.WriteLine("cur: "+all_steps_kinem + " " + all_steps1 + " " + all_steps2 + " " + ring_buf_en + " " + wait_en + " " + wait_term);
                                                //Console.WriteLine("con: " + consider_wait_all_steps_kinem + " " + consider_all_steps1 + " " + consider_all_steps2 + " " + consider_prog_done + " " + consider_wait_en + " " + consider_wait_term);

                                               
                                                load_commands(new string[] { com });


                                                printer.consider_all_steps1 = printer.alternately_commands[printer.cur_alternately_line].consider_all_steps1;
                                                printer.consider_all_steps2 = printer.alternately_commands[printer.cur_alternately_line].consider_all_steps2;
                                                printer.consider_wait_all_steps_kinem = printer.alternately_commands[printer.cur_alternately_line].consider_wait_all_steps_kinem;
                                                printer.consider_wait_en = printer.alternately_commands[printer.cur_alternately_line].consider_wait_en;
                                                printer.consider_wait_term = printer.alternately_commands[printer.cur_alternately_line].consider_wait_term;
                                                printer.consider_prog_done = printer.alternately_commands[printer.cur_alternately_line].consider_prog_done;
                                                //Console.WriteLine("set: " + consider_wait_all_steps_kinem + " " + consider_all_steps1 + " " + consider_all_steps2 + " " + consider_prog_done + " " + consider_wait_en + " " + consider_wait_term);
                                                printer.cur_alternately_line++;

                                                if(printer.consider_prog_done == 1)
                                                {
                                                    //Console.WriteLine("set onsider_prog_done==1");
                                                }
                                                else
                                                {
                                                   // Console.WriteLine("set onsider_prog_done==0");
                                                }
                                            }
                                        }
                                        else
                                        {
                                            /*Console.WriteLine("consider_prog_done == 1 && ring_buf_en == 1 " + (consider_prog_done == 1 && ring_buf_en == 1));
                                            Console.WriteLine("printer_work: " + printer_work);
                                            Console.WriteLine("STOP; ring_buf_en:" + ring_buf_en+" "+ alternately_commands[cur_alternately_line-1].consider_prog_done+" "+(cur_alternately_line - 1));*/
                                            printer.prog_state = StepperPrinter.programm_state.STOP;
                                        }
                                    }
                                        
                                    

                                }

                                //main alternately work-----------------------------------------------------------------



                                //------
                                if (printer.main_alternately_commands_exec && printer.prog_state == StepperPrinter.programm_state.STOP && commands_load && !printer_work && printer.main_alternately_commands_counter < printer.main_alternately_commands.Count && !printer.calibrating_nossle)
                                {
                                    {
                                        if (printer.main_alternately_commands[printer.main_alternately_commands_counter][0].plate_num == 0)
                                        {

                                            var com = printer.main_alternately_commands[printer.main_alternately_commands_counter][0].get_command(printer);
                                            Console.WriteLine("main:"+ printer.main_alternately_commands_counter + " " + printer.main_alternately_commands.Count + " " + com);
                                            exec_main_prog(com);
                                        }
                                        else
                                        {
                                            printer.cur_frame.e = printer.abs_pos_extr;
                                            printer.start_alternate_prog(printer.main_alternately_commands[printer.main_alternately_commands_counter].ToArray());
                                        }

                                        printer.main_alternately_commands_counter++;
                                    }

                                }

                                //delta calib_handler-----------------------------------------------------------------

                                var cur_delta_calib = Convert.ToInt32(vars_from_mes[8]);//!!!!!!!cur_send ==0
                                var cur_ring_buf_en = Convert.ToInt32(vars_from_mes[9]);
                                var cur_homing = Convert.ToInt32(vars_from_mes[10]);
                                if (printer.delta_calibr_en)
                                {



                                    if (printer.prev_homing - cur_homing == 1)
                                    {
                                        _TCPserver1.pushBuffer_in("num1 M589 Y80" + "\n");
                                        printer.delta_calibr_counter = 0;

                                        Console.WriteLine("prev_homing - cur_homing == 1");
                                    }

                                    if (printer.prev_delta_calib - cur_delta_calib == 1 && !printer.delta_calib_go_next_p)
                                    {
                                        Console.WriteLine("cur_delta_calib - prev_delta_calib == 1");
                                        if (printer.delta_calibr_counter == 0)
                                        {
                                            printer.offset_frame = printer.cur_frame;
                                        }


                                        printer.points_res_calibrate_abc[printer.delta_calibr_counter] = (long[])printer.cur_pos.Clone();
                                        printer.delta_calibr_counter++;
                                        Console.WriteLine("calibr_counter: " + printer.delta_calibr_counter + "/ " + printer.delta_calibr_counter_max);
                                        if (printer.delta_calibr_counter >= printer.delta_calibr_counter_max)
                                        {
                                            printer.delta_calibr_en = false;
                                            printer.delta_comp_prop_calibr();
                                            _TCPserver1.pushBuffer_in("num1 M589 X180" + "\n");
                                        }
                                        var delta_orig_commands = new List<string>();
                                        delta_orig_commands.Add("G1 X" + printer.cur_frame.p_xyz.x + " Y" + printer.cur_frame.p_xyz.y + " Z" + printer.cur_frame.p_xyz.z + " F1200");
                                        //delta_orig_commands.Add("G1 Z"+2*printer.points_for_calibrate_xy[printer.delta_calibr_counter].z);
                                        delta_orig_commands.Add("G1 Z" + printer.points_for_calibrate_xy[printer.delta_calibr_counter].z);
                                        delta_orig_commands.Add("G1 X" + printer.points_for_calibrate_xy[printer.delta_calibr_counter].x + " Y" + printer.points_for_calibrate_xy[printer.delta_calibr_counter].y);
                                        //prog_orig_commands g code to next point
                                        var frames_xyz_test = printer.convert_g_code_to_stepperframes(delta_orig_commands.ToArray(), printer);



                                        /*prog_commands = StepperFrame.convert_g_code(frames_xyz_test, ref printer, offset_frame).ToList();
                                        if (prog_commands != null)
                                        {
                                            prog_commands = StepperFrame.prepare_g_code_to_load(prog_commands.ToArray(), ref printer).ToList();
                                            max_count_cur_prog = prog_commands.Count;
                                            cur_prog_line = 0;
                                            prog_state = programm_state.MOVE;

                                            delta_calib_go_next_p = true;
                                            Console.WriteLine(delta_calib_go_next_p + " " + cur_ring_buf_en + " " + cur_prog_line + " ");
                                        }*/

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
                                    if (printer.delta_calib_go_next_p && cur_ring_buf_en == 0) Console.WriteLine(printer.cur_prog_line + "/" + printer.max_count_cur_prog);


                                    if (printer.delta_calib_go_next_p && cur_ring_buf_en == 0 && printer.cur_prog_line > printer.max_count_cur_prog - 5)
                                    {
                                        Console.WriteLine(printer.delta_calib_go_next_p + " " + cur_ring_buf_en + " " + printer.cur_prog_line + " " + printer.max_count_cur_prog / 2);
                                        Console.WriteLine("delta_calib_go_next_p && cur_ring_buf_en == 0");
                                        _TCPserver1.pushBuffer_in("num1 M589 Y80" + "\n");
                                        printer.delta_calib_go_next_p = false;
                                    }

                                }

                                printer.prev_delta_calib = cur_delta_calib;
                                printer.prev_homing = cur_homing;
                                // Console.WriteLine(mes);
                                printer.prev_pos = (long[])printer.cur_pos.Clone();


                                //nossle calib_handler-----------------------------------------------------------------
                                if (printer.calibrating_nossle)
                                {
                                    //var commands_load = (commands1.Count == 0 && commands2.Count == 0 && _TCPserver1.buffer_out.Length == 0 && ring_buf_en == 0);
                                    //var printer_work = (all_steps1 == 1 || all_steps2 == 1);

                                    if(commands_load && !printer_work)
                                    { //----CALIBR_X------------------------------------------------------------------------------------
                                        if (printer.calibrate_nossle_stage_counter == 1  && printer.prog_state == StepperPrinter.programm_state.STOP)
                                        {
                                            var prog_cur = new List<StepperFrame>();
                                            prog_cur.Add(new StepperFrame(1, 589, "A1", false));
                                            prog_cur.Add(printer.cur_frame.clone());
                                            prog_cur.Add(new StepperFrame(new Point3d_GL(printer.settins_string.calibrate_nossle_x[1], printer.settins_string.calibrate_nossle_y[1], printer.settins_string.calibrate_nossle_z[1]), 0, printer.calibr_vel));
                                            printer.start_alternate_prog(prog_cur.ToArray());
                                            printer.calibrate_nossle_stage_counter = 2;

                                            Console.WriteLine("calibrate_nossle_stage_counter: " + printer.calibrate_nossle_stage_counter);

                                        }

                                        if (printer.calibrate_nossle_stage_counter == 2 && printer.calibr_x == 2)
                                        {
                                            printer.calibrate_nossle_frames.Add(printer.cur_frame.clone());
                                            var prog_cur = new List<StepperFrame>();
                                            prog_cur.Add(new StepperFrame(1, 589, "A0", false));
                                            prog_cur.Add(printer.cur_frame.clone());
                                            prog_cur.Add(new StepperFrame(new Point3d_GL(printer.settins_string.calibrate_nossle_x[1], printer.settins_string.calibrate_nossle_y[1], printer.settins_string.calibrate_nossle_z[1]), 0, printer.jog_xyz_vel));
                                            printer.start_alternate_prog(prog_cur.ToArray());
                                            printer.calibrate_nossle_stage_counter = 3;
                                            Console.WriteLine("calibrate_nossle_stage_counter: " + printer.calibrate_nossle_stage_counter);
                                        }


                                        if (printer.calibrate_nossle_stage_counter == 3 && printer.prog_state == StepperPrinter.programm_state.STOP)// &&prog_state == programm_state.STOP && prog_done == 1
                                        {
                                            //calibrate_nossle_frames.Add(cur_frame.clone());
                                            var prog_cur = new List<StepperFrame>();
                                            prog_cur.Add(new StepperFrame(1, 589, "A1", false));
                                            prog_cur.Add(printer.cur_frame.clone());
                                            prog_cur.Add(new StepperFrame(new Point3d_GL(printer.settins_string.calibrate_nossle_x[0], printer.settins_string.calibrate_nossle_y[0], printer.settins_string.calibrate_nossle_z[0]), 0, printer.calibr_vel));
                                            printer.start_alternate_prog(prog_cur.ToArray());
                                            printer.calibrate_nossle_stage_counter = 4;
                                            Console.WriteLine("calibrate_nossle_stage_counter: " + printer.calibrate_nossle_stage_counter);
                                        }

                                        if (printer.calibrate_nossle_stage_counter == 4 && printer.calibr_x == 2)
                                        {
                                            printer.calibrate_nossle_frames.Add(printer.cur_frame.clone());
                                            var prog_cur = new List<StepperFrame>();
                                            prog_cur.Add(new StepperFrame(1, 589, "A0", false));
                                            prog_cur.Add(printer.cur_frame.clone());
                                            prog_cur.Add(new StepperFrame(new Point3d_GL(printer.settins_string.calibrate_nossle_x[0], printer.settins_string.calibrate_nossle_y[0], printer.settins_string.calibrate_nossle_z[0]), 0, printer.jog_xyz_vel));
                                            //prog_cur.Add(new StepperFrame((calibrate_nossle_frames[0].p_xyz + calibrate_nossle_frames[1].p_xyz)/2, 0, calibr_vel));
                                            printer.start_alternate_prog(prog_cur.ToArray());
                                            printer.calibrate_nossle_stage_counter = 5;
                                            Console.WriteLine("calibrate_nossle_stage_counter: " + printer.calibrate_nossle_stage_counter);
                                        }



                                        //----CALIBR_Y------------------------------------------------------------------------------------

                                        if (printer.calibrate_nossle_stage_counter == 5 && printer.prog_state == StepperPrinter.programm_state.STOP)
                                        {
                                            var prog_cur = new List<StepperFrame>();
                                            prog_cur.Add(new StepperFrame(1, 589, "B1", false));
                                            prog_cur.Add(printer.cur_frame.clone());
                                            prog_cur.Add(new StepperFrame(new Point3d_GL(printer.settins_string.calibrate_nossle_x[2], printer.settins_string.calibrate_nossle_y[2], printer.settins_string.calibrate_nossle_z[2]), 0, printer.calibr_vel));
                                            printer.start_alternate_prog(prog_cur.ToArray());
                                            printer.calibrate_nossle_stage_counter = 6;
                                            Console.WriteLine("calibrate_nossle_stage_counter: " + printer.calibrate_nossle_stage_counter);

                                        }

                                        if (printer.calibrate_nossle_stage_counter == 6 && printer.calibr_y == 2)
                                        {
                                            printer.calibrate_nossle_frames.Add(printer.cur_frame.clone());
                                            var prog_cur = new List<StepperFrame>();
                                            prog_cur.Add(new StepperFrame(1, 589, "B0", false));
                                            prog_cur.Add(printer.cur_frame.clone());
                                            prog_cur.Add(new StepperFrame(new Point3d_GL(printer.settins_string.calibrate_nossle_x[2], printer.settins_string.calibrate_nossle_y[2], printer.settins_string.calibrate_nossle_z[2]), 0, printer.jog_xyz_vel));
                                            printer.start_alternate_prog(prog_cur.ToArray());
                                            printer.calibrate_nossle_stage_counter = 7;
                                            Console.WriteLine("calibrate_nossle_stage_counter: " + printer.calibrate_nossle_stage_counter);
                                        }


                                        if (printer.calibrate_nossle_stage_counter == 7 && printer.prog_state == StepperPrinter.programm_state.STOP)
                                        {
                                            //calibrate_nossle_frames.Add(cur_frame.clone());
                                            var prog_cur = new List<StepperFrame>();
                                            prog_cur.Add(new StepperFrame(1, 589, "B1", false));
                                            prog_cur.Add(printer.cur_frame.clone());
                                            prog_cur.Add(new StepperFrame(new Point3d_GL(printer.settins_string.calibrate_nossle_x[0], printer.settins_string.calibrate_nossle_y[0], printer.settins_string.calibrate_nossle_z[0]), 0, printer.calibr_vel));
                                            printer.start_alternate_prog(prog_cur.ToArray());
                                            printer.calibrate_nossle_stage_counter = 8;
                                            Console.WriteLine("calibrate_nossle_stage_counter: " + printer.calibrate_nossle_stage_counter);
                                        }

                                        if (printer.calibrate_nossle_stage_counter == 8 && printer.calibr_y == 2)
                                        {
                                            printer.calibrate_nossle_frames.Add(printer.cur_frame.clone());
                                            var prog_cur = new List<StepperFrame>();
                                            prog_cur.Add(new StepperFrame(1, 589, "B0", false));
                                            prog_cur.Add(printer.cur_frame.clone());
                                            var fr_z_cal = new StepperFrame((printer.calibrate_nossle_frames[2].p_xyz + printer.calibrate_nossle_frames[3].p_xyz) / 2, 0, printer.jog_xyz_vel);
                                            printer.calibrate_nossle_frames.Add(fr_z_cal.clone());
                                            fr_z_cal.p_xyz.z += 15;
                                            //prog_cur.Add(new StepperFrame(new Point3d_GL(settins_string.calibrate_nossle_x[0], settins_string.calibrate_nossle_y[0], settins_string.calibrate_nossle_z[0]), 0, jog_xyz_vel));
                                            prog_cur.Add(fr_z_cal.clone());
                                            printer.start_alternate_prog(prog_cur.ToArray());


                                            printer.calibrate_nossle_stage_counter = 9;
                                            Console.WriteLine("calibrate_nossle_stage_counter: " + printer.calibrate_nossle_stage_counter);
                                        }


                                        //----CALIBR_Z------------------------------------------------------------------------------------

                                        if (printer.calibrate_nossle_stage_counter == 9 && printer.prog_state == StepperPrinter.programm_state.STOP)
                                        {
                                            var prog_cur = new List<StepperFrame>();
                                            prog_cur.Add(new StepperFrame(1, 589, "C1", false));
                                            prog_cur.Add(printer.cur_frame.clone());
                                            prog_cur.Add(new StepperFrame(printer.calibrate_nossle_frames[printer.calibrate_nossle_frames.Count - 1].p_xyz, 0, printer.calibr_vel));
                                            printer.start_alternate_prog(prog_cur.ToArray());
                                            printer.calibrate_nossle_stage_counter = 10;
                                            Console.WriteLine("calibrate_nossle_stage_counter: " + printer.calibrate_nossle_stage_counter);

                                        }

                                        if (printer.calibrate_nossle_stage_counter == 10 && printer.calibr_z == 2)
                                        {
                                            printer.calibrate_nossle_frames.Add(printer.cur_frame.clone());
                                            var prog_cur = new List<StepperFrame>();
                                            prog_cur.Add(new StepperFrame(1, 589, "C0", false));
                                            var cur_fr_dz = printer.cur_frame.p_xyz.Clone();
                                            cur_fr_dz.z += 30;
                                            prog_cur.Add(printer.cur_frame.clone());
                                            prog_cur.Add(new StepperFrame(cur_fr_dz, 0, printer.jog_xyz_vel));

                                            var fr_x_cal = (printer.calibrate_nossle_frames[0].p_xyz + printer.calibrate_nossle_frames[1].p_xyz) / 2;
                                            var fr_y_cal = (printer.calibrate_nossle_frames[2].p_xyz + printer.calibrate_nossle_frames[3].p_xyz) / 2;
                                            printer.offset_nossle[printer.tool_active].p_xyz.x = fr_x_cal.x;
                                            printer.offset_nossle[printer.tool_active].p_xyz.y = fr_y_cal.y;
                                            printer.offset_nossle[printer.tool_active].p_xyz.z = printer.cur_frame.p_xyz.z;
                                            printer.tool_calibrated_auto[printer.tool_active] = true;
                                            printer.start_alternate_prog(prog_cur.ToArray());
                                            printer.calibrating_nossle = false;
                                            printer.offset_frame = printer.comp_offset(printer.cur_coordinate_system);
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
                                        printer.wait_term = Convert.ToInt32(vars_from_mes[4][4]) - 48;
                                        printer.all_steps2 = Convert.ToInt32(vars_from_mes[4][0]) - 48;
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
            printer.settins_string = load_obj<SettingsString>("settings_printer.json");


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


            for (int i = 0; i<printer.settins_string.motors_count1;i++)
            {
                
                _TCPserver1.pushBuffer_in("num1 M587" + 
                    " I" + i + 
                    " A" + Math.Round(printer.settins_string.a_max1[i],3) +
                    " V" + Math.Round(printer.settins_string.v_def1[i], 3) +
                    " E" + printer.settins_string.end_inv1[i] +
                    " D" + printer.settins_string.motor_dir1[i] +
                    " R" + printer.settins_string.steps_per_mm1[i] +
                    " K" + printer.settins_string.home_dir1[i] +
                    " B" + printer.settins_string.home_pos1[i] +
                    "\n");
            }

            for (int i = 0; i < printer.settins_string.motors_count2; i++)
            {
                _TCPserver1.pushBuffer_in("num2 M587" +
                    " I" + i +
                    " A" + Math.Round(printer.settins_string.a_max2[i], 3) +
                    " V" + Math.Round(printer.settins_string.v_def2[i], 3) +
                    " E" + printer.settins_string.end_inv2[i] +
                    " D" + printer.settins_string.motor_dir2[i] +
                    " R" + printer.settins_string.steps_per_mm2[i] +
                    " K" + printer.settins_string.home_dir2[i] +
                    " B" + printer.settins_string.home_pos2[i] +
                    "\n");
            }


            _TCPserver1.pushBuffer_in("num1 M581 I2 A0");
            save_obj("settings_printer.json", printer.settins_string);

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

