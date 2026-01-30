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
            try
            {
                var serv = new Udp_to_tcp();
                serv.connect_udp_all();
            }
            catch (Exception ex)
            {
                // Выводим ошибку красным цветом для наглядности
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Критическая ошибка: {ex.Message}");
                Console.ResetColor();
                Console.WriteLine("\nНажмите любую клавишу для выхода...");
                Console.ReadKey();
            }
            Console.ReadKey();
        }



    }

}
