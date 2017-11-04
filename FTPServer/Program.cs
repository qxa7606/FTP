using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FTPServer
{

    class Program
    {
        /// <summary>
        /// This is the entry point for the program. It creates a server
        /// object and initiates it.
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            ftp_server serv = new ftp_server();
            serv.Start();

            Console.WriteLine("Press any key to stop...");
            Console.ReadKey(true);
        }
    }
}
