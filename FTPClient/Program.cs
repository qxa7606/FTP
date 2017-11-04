using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FTPClient
{
    class Program
    {
        /// <summary>
        /// The entry point for the program. Creates an ftp client object
        /// and initiates it.
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            ftp_client cl = new ftp_client();
            try
            {
                cl.handle(args);
            } catch (Exception e)
            {
                Console.WriteLine("This exception was thrown: ");
                Console.WriteLine(e.ToString());
                Console.WriteLine("Press any key to exit");
                Console.Read();
                Environment.Exit(1);
            }
        }
    }
}
