using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

namespace FTPServer
{
    /// <summary>
    /// This class represents a client connected to the server. It 
    /// all the functions and user 
    /// </summary>
    class client_connection
    {
        string line = null;
        private TcpClient controlClient;
        private TcpClient dataClient;
        private TcpListener passive_listener;

        enum MODE { PASSIVE_MODE, ACTIVE_MODE };

        string username;
        string password;

        bool logged_in = false;

        const string welcome_msg = "========================= Welcome to this server! =======================\n";

        /// <summary>
        /// Constructor for the class
        /// </summary>
        /// <param name="client">Tcp client for the control connection</param>
        public client_connection(TcpClient client)
        {
            controlClient = client;
        }

        /// <summary>
        /// The main method that handles all the acitivity 
        /// in the class. Keeps listening and handling commands
        /// from the user.
        /// </summary>
        /// <param name="obj">unused</param>
        public void handleClient(object obj)
        {
            NetworkStream networkStream = controlClient.GetStream();
            StreamReader controlStreamReader = new StreamReader(networkStream);
            StreamWriter controlStreamWriter = new StreamWriter(networkStream);

            controlStreamWriter.WriteLine("Server ready. Please login in using USER and PASS.");
            controlStreamWriter.Flush();

            try
            {
                while ((line = controlStreamReader.ReadLine()) != null)
                {
                    string[] command = line.Split(' ');

                    string cmd = command[0].ToUpperInvariant();
                    string arguments = command.Length > 1 ? line.Substring(command[0].Length + 1) : null;

                    if (arguments != null && arguments.Trim().Length == 0)
                    {
                        arguments = null;
                    }

                    if (!logged_in && !(cmd.Equals("USER") || cmd.Equals("PASS")))
                    {
                        controlStreamWriter.WriteLine("530 Please login with USER and PASS.");
                        controlStreamWriter.Flush();
                        continue;
                    }

                    switch (cmd)
                    {
                        case "USER":
                            processUSERCommand(arguments);
                            break;
                        case "PASS":
                            processPASSCommand(arguments);
                            break;
                        case "LIST":
                            processDIRCommand();
                            dataClient.Close();
                            break;
                        case "RETR":
                            processRETRCommand(arguments);
                            dataClient.Close();
                            break;
                        case "PASV":
                            processPASVCommand();
                            break;
                        case "PORT":
                            processPORTCommand(arguments);
                            break;
                        case "PWD":
                            controlStreamWriter.WriteLine("257 \" / \" is the current directory.");
                            controlStreamWriter.Flush();
                            break;
                        case "QUIT":
                            controlClient.Close();
                            controlClient.Dispose();
                            break;
                        default:
                            controlStreamWriter.WriteLine("200 Command not supported");
                            controlStreamWriter.Flush();
                            break;
                    }
                }
            } catch (Exception e)
            {
                Console.WriteLine("This exception was raised while handling client ({0}):", controlClient.Client.RemoteEndPoint);
                Console.WriteLine(e.ToString());
            }
        }

        /// <summary>
        /// This method handles the username entry of the client
        /// </summary>
        /// <param name="argument">potential username</param>
        private void processUSERCommand(string argument)
        {
            NetworkStream networkStream = controlClient.GetStream();
            StreamWriter controlStreamWriter = new StreamWriter(networkStream);

            if (logged_in)
            {
                controlStreamWriter.WriteLine("530 You are already logged in");
                controlStreamWriter.Flush();
            }
            else if (argument != null && (argument.Equals("ftp") || argument.Equals("anonymous")))
            {
                username = argument;
                controlStreamWriter.WriteLine("331 Username accepted. Please enter password:");
                controlStreamWriter.Flush();
            }
            else
            {
                controlStreamWriter.WriteLine("530 This server is anonymous only. Please use 'ftp' or 'anonymous' for username.");
                controlStreamWriter.Flush();
            }
        }

        /// <summary>
        /// This method handles the password entry for the client
        /// </summary>
        /// <param name="argument">potential password</param>
        private void processPASSCommand(string argument)
        {
            NetworkStream networkStream = controlClient.GetStream();
            StreamReader controlStreamReader = new StreamReader(networkStream);
            StreamWriter controlStreamWriter = new StreamWriter(networkStream);

            if (logged_in)
            {
                controlStreamWriter.WriteLine("530 You are already logged in");
                controlStreamWriter.Flush();
            }
            else if (username == null)
            {
                controlStreamWriter.WriteLine("530 Please enter a username first");
                controlStreamWriter.Flush();
            }
            else if (argument == null || argument.Length == 0)
            {
                controlStreamWriter.WriteLine("530 Your password length must be greater than 0");
                controlStreamWriter.Flush();
            }
            else
            {
                controlStreamWriter.WriteLine("230 Login successful");
                controlStreamWriter.WriteLine(welcome_msg);
                controlStreamWriter.Flush();
                password = argument;
                logged_in = true;
            }
        }

        /// <summary>
        /// This method processes the dir command.
        /// It sends a listing of the current directory.
        /// </summary>
        private void processDIRCommand()
        {
            NetworkStream networkStream = controlClient.GetStream();
            StreamReader controlStreamReader = new StreamReader(networkStream);
            StreamWriter controlStreamWriter = new StreamWriter(networkStream);
            NetworkStream dataDetworkStream = controlClient.GetStream();

            controlStreamWriter.WriteLine("150 Here comes the directory listing");
            controlStreamWriter.Flush();

            StreamWriter dataWriter = new StreamWriter(dataDetworkStream);

            IEnumerable<string> directories = Directory.EnumerateDirectories(".");

            foreach (string dir in directories)
            {
                DirectoryInfo d = new DirectoryInfo(dir);

                string date = d.LastWriteTime < DateTime.Now - TimeSpan.FromDays(180) ?
                    d.LastWriteTime.ToString("MMM dd  yyyy") :
                    d.LastWriteTime.ToString("MMM dd HH:mm");

                string line = string.Format("drwxr-xr-x    2 0     0     {0,8} {1} {2}", "4096", date, d.Name);

                dataWriter.WriteLine(line);
                dataWriter.Flush();
            }

            IEnumerable<string> files = Directory.EnumerateFiles(".");

            foreach (string file in files)
            {
                FileInfo f = new FileInfo(file);

                string date = f.LastWriteTime < DateTime.Now - TimeSpan.FromDays(180) ?
                    f.LastWriteTime.ToString("MMM dd  yyyy") :
                    f.LastWriteTime.ToString("MMM dd HH:mm");

                string line = string.Format("-rw-r--r--    2 0     0     {0,8} {1} {2}", f.Length, date, f.Name);

                dataWriter.WriteLine(line);
                dataWriter.Flush();
            }

            controlStreamWriter.WriteLine("226 Directory send OK");
            controlStreamWriter.Flush();
        }

        /// <summary>
        /// This method is used for sending files to the client
        /// </summary>
        /// <param name="argument">path of the file desired</param>
        private void processRETRCommand(string argument)
        {
            NetworkStream networkStream = controlClient.GetStream();
            StreamWriter controlStreamWriter = new StreamWriter(networkStream);

            controlStreamWriter.WriteLine("150 Opening data connection for file transfer");
            controlStreamWriter.Flush();

            if (argument != null)
            {
                if (File.Exists(argument))
                {
                    NetworkStream dataNetworkStream = dataClient.GetStream();
                    StreamWriter dataStreamWriter = new StreamWriter(dataNetworkStream);

                    const int bufsize = 8192;

                    var buffer = new byte[bufsize];
                    NetworkStream ns = dataClient.GetStream();

                    using (var s = File.OpenRead(argument))
                    {
                        int actuallyRead;
                        while ((actuallyRead = s.Read(buffer, 0, bufsize)) > 0)
                        {
                            ns.Write(buffer, 0, actuallyRead);
                        }
                    }
                    ns.Flush();

                    controlStreamWriter.WriteLine("226 Transfer complete");
                    controlStreamWriter.Flush();
                }
                else
                {
                    controlStreamWriter.WriteLine("550 File not found");
                    controlStreamWriter.Flush();
                }
            }
            else
            {
                controlStreamWriter.WriteLine("550 Please enter a valid file name");
                controlStreamWriter.Flush();
            }
        }

        /// <summary>
        /// This method handles the passive command from the client.
        /// It creates the listener for the data channel and 
        /// waits for the client to connect.
        /// </summary>
        private void processPASVCommand()
        {
            NetworkStream networkStream = controlClient.GetStream();
            StreamReader controlStreamReader = new StreamReader(networkStream);
            StreamWriter controlStreamWriter = new StreamWriter(networkStream);

            IPAddress localIP = ((IPEndPoint)controlClient.Client.LocalEndPoint).Address;
            passive_listener = new TcpListener(localIP, 0);
            passive_listener.Start();

            passive_listener.BeginAcceptTcpClient(AcceptPassiveClient, passive_listener);

            string ip = ((IPEndPoint)passive_listener.LocalEndpoint).ToString().Replace('.', ',').Split(':')[0];
            int port = ((IPEndPoint)passive_listener.LocalEndpoint).Port;
            
            string response = "227 Entering Passive Mode (" + ip + "," + (port / 256).ToString() + "," + (port % 256).ToString() + ")";
            controlStreamWriter.WriteLine(response);
            controlStreamWriter.Flush();
        }

        /// <summary>
        /// The callback function for when the client connects on
        /// the listener for passive connection.
        /// </summary>
        /// <param name="result">info for the data client</param>
        private void AcceptPassiveClient(IAsyncResult result)
        {
            dataClient = passive_listener.EndAcceptTcpClient(result);
        }

        /// <summary>
        /// The method estblishes a connection with the client in PORT
        /// mode. It connects to the channel/listener created by the client.
        /// </summary>
        /// <param name="argument">the ip and the port</param>
        private void processPORTCommand(string argument)
        {
            NetworkStream networkStream = controlClient.GetStream();
            StreamReader controlStreamReader = new StreamReader(networkStream);
            StreamWriter controlStreamWriter = new StreamWriter(networkStream);

            portAndIP addr = parsePortAndIP(argument);

            dataClient = new TcpClient();
            
            dataClient.Client.Connect(IPAddress.Parse(addr.ip), addr.port);

            controlStreamWriter.WriteLine("200 Data connection established");
            controlStreamWriter.Flush();
        }

        /// <summary>
        /// Simple method used to parse the info string sent for the
        /// PORT mode. Parses the ip and the port number.
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        private portAndIP parsePortAndIP(string s)
        {
            s = s.Replace(',', '.');
            string[] com = s.Split('.');
            string ipAddress = com[0] + "." + com[1] + "." + com[2] + "." + com[3];
            int port = (Int32.Parse(com[4]) * 256) + Int32.Parse(com[5]);

            return new portAndIP(ipAddress, port);
        }
    }

    /// <summary>
    /// Simple class to represent the ip and the port for 
    /// when the client wants to be in PORT mode
    /// </summary>
    class portAndIP
    {
        public string ip { get; set; }
        public int port { get; set; }
        public portAndIP(string ip, int port)
        {
            this.ip = ip;
            this.port = port;
        }
    }
}