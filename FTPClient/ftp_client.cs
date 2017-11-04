/*
 * FTP Client class
 * Author : Qasim Ali (qxa7606@rit.edu)
 * Template by Jeremy Brown (jsb@cs.rit.edu)
 */
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace FTPClient
{
    /// <summary>
    /// This is the main class for an ftp client. It handles all the 
    /// network communications and user input.
    /// </summary>
    class ftp_client
    {
        // The prompt
        public const string PROMPT = "FTP> ";

        TcpListener listener;
        TcpClient controlClient;
        TcpClient dataClient;

        byte[] buffer;
        int bytesRead;
        string dataReceived;

        String input = null;

        enum MODE { PASSIVE_MODE, ACTIVE_MODE };

        MODE current_mode = MODE.PASSIVE_MODE;

        bool debug = false;

        // Information to parse commands
        public static readonly string[] COMMANDS = { "ascii",
                          "binary",
                          "cd",
                          "cdup",
                          "debug",
                          "dir",
                          "get",
                          "help",
                          "passive",
                          "put",
                          "pwd",
                          "quit",
                          "user" };

        public const int ASCII = 0;
        public const int BINARY = 1;
        public const int CD = 2;
        public const int CDUP = 3;
        public const int DEBUG = 4;
        public const int DIR = 5;
        public const int GET = 6;
        public const int HELP = 7;
        public const int PASSIVE = 8;
        public const int PUT = 9;
        public const int PWD = 10;
        public const int QUIT = 11;
        public const int USER = 12;

        // Help message

        public static readonly String[] HELP_MESSAGE = {
    "ascii      --> Set ASCII transfer type",
    "binary     --> Set binary transfer type",
    "cd <path>  --> Change the remote working directory",
    "cdup       --> Change the remote working directory to the",
        "               parent directory (i.e., cd ..)",
    "debug      --> Toggle debug mode",
    "dir        --> List the contents of the remote directory",
    "get path   --> Get a remote file",
    "help       --> Displays this text",
    "passive    --> Toggle passive/active mode",
    "put path   --> Transfer the specified file to the server",
    "pwd        --> Print the working directory on the server",
    "quit       --> Close the connection to the server and terminate",
    "user login --> Specify the user name (will prompt for password" };

        public void handle(string[] args)
        {
            bool eof = false;

            // Handle the command line stuff

            if (args.Length != 1 && args.Length != 2)
            {
                Console.Error.WriteLine("Usage: [mono] Ftp <server(required)> <port>");
                Console.ReadLine();
                Environment.Exit(1);
            }

            controlClient = new TcpClient();
            controlClient.Client.Connect(args[0], args.Length > 1 ? Int32.Parse(args[1]) : 21);
            buffer = new byte[controlClient.ReceiveBufferSize];
            NetworkStream networkStream = controlClient.GetStream();
            StreamWriter clientStreamWriter = new StreamWriter(networkStream);

            bytesRead = controlClient.GetStream().Read(buffer, 0, controlClient.ReceiveBufferSize);
            dataReceived = Encoding.ASCII.GetString(buffer, 0, bytesRead);
            Console.Write(dataReceived);

            addCredentials();
            activatePassiveMode(); // defualt to passive mode

            // Command line is done - accept commands
            do
            {
                try
                {
                    Console.Write(PROMPT);
                    input = Console.ReadLine();
                }
                catch (Exception)
                {
                    eof = true;
                }

                // Keep going if we have not hit end of file
                if (!eof && input.Length > 0)
                {
                    int cmd = -1;
                    string[] argv = Regex.Split(input, "\\s+");

                    // What command was entered?
                    for (int i = 0; i < COMMANDS.Length && cmd == -1; i++)
                    {
                        if (COMMANDS[i].Equals(argv[0], StringComparison.CurrentCultureIgnoreCase))
                        {
                            cmd = i;
                        }
                    }

                    // Execute the command
                    switch (cmd)
                    {
                        case ASCII:
                            updateTransferType(ASCII);
                            break;

                        case BINARY:
                            updateTransferType(BINARY);
                            break;

                        case CD:
                            if (argv.Length < 2)
                            {
                                Console.WriteLine("Enter the destination path.");
                            }
                            else
                            {
                                changeWorkingDirectory(argv[1]);
                            }
                            break;

                        case CDUP:
                            changeWorkingDirectoryToParent();
                            break;

                        case DEBUG:
                            if (debug)
                            {
                                debug = false;
                                Console.WriteLine("Debug mode is OFF.");
                            }
                            else
                            {
                                debug = true;
                                Console.WriteLine("Debug mode is ON.");
                            }
                            break;

                        case DIR:
                            if (current_mode == MODE.PASSIVE_MODE)
                            {
                                activatePassiveMode();
                            }
                            else
                            {
                                activateActiveMode();
                            }
                            getDirectoryListing();
                            break;

                        case GET:
                            if (argv.Length < 2)
                            {
                                Console.WriteLine("Enter the destination path.");
                            }
                            else
                            {
                                if (current_mode == MODE.PASSIVE_MODE)
                                {
                                    activatePassiveMode();
                                }
                                else
                                {
                                    activateActiveMode();
                                }
                                retrieveFile(argv[1]);
                            }
                            break;

                        case HELP:
                            for (int i = 0; i < HELP_MESSAGE.Length; i++)
                            {
                                Console.WriteLine(HELP_MESSAGE[i]);
                            }
                            break;

                        case PASSIVE:
                            if (current_mode == MODE.ACTIVE_MODE)
                            {
                                current_mode = MODE.PASSIVE_MODE;
                                Console.WriteLine("Switched to PASSIVE mode");
                            }
                            else
                            {
                                current_mode = MODE.ACTIVE_MODE;
                                Console.WriteLine("Switched to PORT mode");
                            }
                            break;

                        case PUT:
                            Console.WriteLine("PUT command is not supported.");
                            break;

                        case PWD:
                            getWorkingDirectory();
                            break;

                        case QUIT:
                            eof = true;
                            break;

                        case USER:
                            Console.WriteLine("Command not supported. Please restart the program if you want to login as a different user.");
                            break;

                        default:
                            Console.WriteLine("Invalid command");
                            break;
                    }
                }
            } while (!eof);
        }

        /// <summary>
        /// This method is responsible for getting a file from the server
        /// </summary>
        /// <param name="path">The file to get</param>
        private void retrieveFile(string path)
        {
            NetworkStream networkStream = controlClient.GetStream();
            StreamReader controlStreamReader = new StreamReader(networkStream);
            StreamWriter controlStreamWriter = new StreamWriter(networkStream);

            controlStreamWriter.WriteLine("RETR " + path);
            controlStreamWriter.Flush();

            if (debug)
            {
                Console.WriteLine("Sent RETR command to the server");
            }

            bytesRead = networkStream.Read(buffer, 0, controlClient.ReceiveBufferSize);
            dataReceived = Encoding.ASCII.GetString(buffer, 0, bytesRead);
            Console.Write(dataReceived);
            
            if (dataReceived.Contains("550"))
            {
                Console.WriteLine("There was an error retrieving the file");
                return;
            }

            try
            {
                if (debug)
                {
                    Console.WriteLine("Attempting to recieve file data");
                }

                System.Threading.Thread.Sleep(100);

                NetworkStream dataNetworkStream = dataClient.GetStream();
                StreamReader dataStreamReader = new StreamReader(dataNetworkStream);
                bytesRead = dataNetworkStream.Read(buffer, 0, dataClient.ReceiveBufferSize);
                dataReceived = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                File.WriteAllText(path.Split('/')[path.Split('/').Length - 1], dataReceived);
            }
            catch (Exception)
            {
                Console.WriteLine("Unable to download file through the data connection. This exception was thrown:");
                return;
            }

            bytesRead = networkStream.Read(buffer, 0, controlClient.ReceiveBufferSize);
            dataReceived = Encoding.ASCII.GetString(buffer, 0, bytesRead);
            Console.Write(dataReceived);

            Console.WriteLine("Please examine your local directory");
        }

        /// <summary>
        /// This changes the current working directory to the parent directory
        /// </summary>
        private void changeWorkingDirectoryToParent()
        {
            NetworkStream networkStream = controlClient.GetStream();
            StreamReader controlStreamReader = new StreamReader(networkStream);
            StreamWriter controlStreamWriter = new StreamWriter(networkStream);

            controlStreamWriter.WriteLine("CDUP");
            controlStreamWriter.Flush();

            if (debug)
            {
                Console.WriteLine("Sent CDUP command to the server");
            }

            bytesRead = networkStream.Read(buffer, 0, controlClient.ReceiveBufferSize);
            dataReceived = Encoding.ASCII.GetString(buffer, 0, bytesRead);
            Console.Write(dataReceived);
        }

        /// <summary>
        /// This method changes the current working directory to what the user wants
        /// </summary>
        /// <param name="path">The directory to change to</param>
        private void changeWorkingDirectory(string path)
        {
            NetworkStream networkStream = controlClient.GetStream();
            StreamReader controlStreamReader = new StreamReader(networkStream);
            StreamWriter controlStreamWriter = new StreamWriter(networkStream);

            controlStreamWriter.WriteLine("CWD " + path);
            controlStreamWriter.Flush();

            if (debug)
            {
                Console.WriteLine("Sent CWD command to the server");
            }

            bytesRead = networkStream.Read(buffer, 0, controlClient.ReceiveBufferSize);
            dataReceived = Encoding.ASCII.GetString(buffer, 0, bytesRead);
            Console.Write(dataReceived);

            getWorkingDirectory();
        }

        /// <summary>
        /// This method gets the current working directory for the user
        /// </summary>
        private void getWorkingDirectory()
        {
            NetworkStream networkStream = controlClient.GetStream();
            StreamReader controlStreamReader = new StreamReader(networkStream);
            StreamWriter controlStreamWriter = new StreamWriter(networkStream);

            controlStreamWriter.WriteLine("PWD");
            controlStreamWriter.Flush();

            if (debug)
            {
                Console.WriteLine("Sent PWD command to the server");
            }

            bytesRead = networkStream.Read(buffer, 0, controlClient.ReceiveBufferSize);
            dataReceived = Encoding.ASCII.GetString(buffer, 0, bytesRead);
            Console.Write(dataReceived);
        }

        /// <summary>
        /// This method updates the transfer type to either binary or ascii
        /// </summary>
        /// <param name="type">Which type the user wants (ascii vs binary)</param>
        private void updateTransferType(int type)
        {
            NetworkStream networkStream = controlClient.GetStream();
            StreamReader controlStreamReader = new StreamReader(networkStream);
            StreamWriter controlStreamWriter = new StreamWriter(networkStream);

            controlStreamWriter.WriteLine("TYPE " + (type == ASCII ? "A" : "I"));
            controlStreamWriter.Flush();

            if (debug)
            {
                Console.WriteLine("Sent TYPE command to the server");
            }

            bytesRead = networkStream.Read(buffer, 0, controlClient.ReceiveBufferSize);
            dataReceived = Encoding.ASCII.GetString(buffer, 0, bytesRead);
            Console.Write(dataReceived);
        }
        
        /// <summary>
        /// This method enables the user to login to the server
        /// </summary>
        private void addCredentials()
        {
            NetworkStream networkStream = controlClient.GetStream();
            StreamReader controlStreamReader = new StreamReader(networkStream);
            StreamWriter controlStreamWriter = new StreamWriter(networkStream);

            Console.Write("Username: ");
            input = Console.ReadLine();

            controlStreamWriter.WriteLine("USER " + input);
            controlStreamWriter.Flush();

            if (debug)
            {
                Console.WriteLine("Sent USER command to the server");
            }

            bytesRead = networkStream.Read(buffer, 0, controlClient.ReceiveBufferSize);
            dataReceived = Encoding.ASCII.GetString(buffer, 0, bytesRead);
            Console.Write(dataReceived);

            while (dataReceived.Contains("530"))
            {
                Console.WriteLine("Please enter a valid username");

                Console.Write("Username: ");
                input = Console.ReadLine();

                controlStreamWriter.WriteLine("USER " + input);
                controlStreamWriter.Flush();

                bytesRead = networkStream.Read(buffer, 0, controlClient.ReceiveBufferSize);
                dataReceived = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                Console.Write(dataReceived);
            }

            Console.Write("Password: ");
            input = Console.ReadLine();

            controlStreamWriter.WriteLine("PASS " + input);
            controlStreamWriter.Flush();

            if (debug)
            {
                Console.WriteLine("Sent PASS command to the server");
            }

            bytesRead = networkStream.Read(buffer, 0, controlClient.ReceiveBufferSize);
            dataReceived = Encoding.ASCII.GetString(buffer, 0, bytesRead);
            Console.Write(dataReceived);
        }

        /// <summary>
        /// This method creates a data connection with the server in active mode
        /// </summary>
        private void activateActiveMode()
        {
            IPAddress IP = IPAddress.Parse("0.0.0.0");
            NetworkStream networkStream = controlClient.GetStream();
            StreamReader controlStreamReader = new StreamReader(networkStream);
            StreamWriter controlStreamWriter = new StreamWriter(networkStream);

            IPAddress[] localIPs = Dns.GetHostAddresses(Dns.GetHostName());
            foreach (IPAddress addr in localIPs)
            {
                if (addr.AddressFamily == AddressFamily.InterNetwork)
                {
                    IP = IPAddress.Parse(addr.ToString());
                }
            }

            listener = new TcpListener(IPAddress.Any, 0);
            listener.Start();
            listener.BeginAcceptTcpClient(HandleAcceptTcpClient, listener);
            
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;

            string port_command = "PORT " + IP.ToString().Replace('.',',') + "," + (port / 256).ToString() + "," + (port % 256).ToString();
            controlStreamWriter.WriteLine(port_command);
            controlStreamWriter.Flush();

            if (debug)
            {
                Console.WriteLine("Sent PORT command to the server");
            }

            bytesRead = networkStream.Read(buffer, 0, controlClient.ReceiveBufferSize);
            dataReceived = Encoding.ASCII.GetString(buffer, 0, bytesRead);
            Console.Write(dataReceived);
        }
        
        /// <summary>
        /// This is the callback function for when the server connects to the client (active mode)
        /// </summary>
        /// <param name="result"></param>
        private void HandleAcceptTcpClient(IAsyncResult result)
        {
            dataClient = listener.EndAcceptTcpClient(result);

            if (debug)
            {
                Console.WriteLine("Server has connected to the data connection");
            }
        }

        /// <summary>
        /// The method creates a data connection with the server in passive mode
        /// </summary>
        private void activatePassiveMode()
        {
            NetworkStream networkStream = controlClient.GetStream();
            StreamReader controlStreamReader = new StreamReader(networkStream);
            StreamWriter controlStreamWriter = new StreamWriter(networkStream);

            controlStreamWriter.WriteLine("PASV");
            controlStreamWriter.Flush();

            if (debug)
            {
                Console.WriteLine("Sent PASV command to the server");
            }

            buffer = new byte[controlClient.ReceiveBufferSize];

            bytesRead = networkStream.Read(buffer, 0, controlClient.ReceiveBufferSize);
            dataReceived = Encoding.ASCII.GetString(buffer, 0, bytesRead);
            Console.Write(dataReceived);

            portAndIP addr = parsePortAndIP(dataReceived);

            dataClient = new TcpClient();

            dataClient.Client.Connect(IPAddress.Parse(addr.ip), addr.port);

            if (debug)
            {
                Console.WriteLine("Connected to data channel for passive mode");
            }
        }

        /// <summary>
        /// This method gets the listing for the current working directory
        /// </summary>
        private void getDirectoryListing()
        {
            buffer = new byte[controlClient.ReceiveBufferSize];

            NetworkStream controlNetworkStream = controlClient.GetStream();
            StreamReader controlStreamReader = new StreamReader(controlNetworkStream, Encoding.ASCII);
            StreamWriter controlStreamWriter = new StreamWriter(controlNetworkStream, Encoding.ASCII);

            controlStreamWriter.WriteLine("LIST");
            controlStreamWriter.Flush();

            if (debug)
            {
                Console.WriteLine("Sent LIST command to the server");
            }

            bytesRead = controlNetworkStream.Read(buffer, 0, controlClient.ReceiveBufferSize);
            dataReceived = Encoding.ASCII.GetString(buffer, 0, bytesRead);
            Console.Write(dataReceived);

            System.Threading.Thread.Sleep(100);

            NetworkStream dataNetworkStream = dataClient.GetStream();
            StreamReader dataStreamReader = new StreamReader(dataNetworkStream);

            bytesRead = dataNetworkStream.Read(buffer, 0, dataClient.ReceiveBufferSize);
            dataReceived = Encoding.ASCII.GetString(buffer, 0, bytesRead);
            Console.Write(dataReceived);

            bytesRead = controlNetworkStream.Read(buffer, 0, controlClient.ReceiveBufferSize);
            dataReceived = Encoding.ASCII.GetString(buffer, 0, bytesRead);
            Console.Write(dataReceived);
        }

        /// <summary>
        /// This method parses the passive channel info sent by the server
        /// </summary>
        /// <param name="s">The string sent by the server (PORT x,x,x,x,x,x)</param>
        /// <returns></returns>
        private portAndIP parsePortAndIP(string s)
        {
            int index1 = dataReceived.IndexOf('(');
            int index2 = dataReceived.IndexOf(')');

            string ipData = dataReceived.Substring(index1 + 1, index2 - index1 - 1);

            int[] parts = new int[6];

            int len = ipData.Length;
            int partCount = 0;
            string buf = "";

            for (int i = 0; i < len && partCount <= 6; i++)
            {
                char ch = char.Parse(ipData.Substring(i, 1));

                if (char.IsDigit(ch))
                    buf += ch;

                if (ch == ',' || i + 1 == len)
                {
                    parts[partCount++] = int.Parse(buf);
                    buf = "";
                }
            }

            string ipAddress = parts[0] + "." + parts[1] + "." + parts[2] + "." + parts[3];

            int port = (parts[4] << 8) + parts[5];

            return new portAndIP(ipAddress, port);
        }
    }

    /// <summary>
    /// A simple class to represent an address (an IP and a port)
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