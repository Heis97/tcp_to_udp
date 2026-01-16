using System.ComponentModel.Design;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;

namespace tcp_to_udp
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var serv = new Udp_to_tcp();

            serv.connect_udp_all();
        }



    }

}
