using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace FTPServer
{
    /// <summary>
    /// This is the main server class. It opens a connection that 
    /// anyone can use to connect to it. It handles each connection
    /// by making a new connection class and initiating it.
    /// </summary>
    class ftp_server
    {
        private bool listening = false;

        private TcpListener tcplistener;
        private List<client_connection> connections; // list of current connections
        private IPEndPoint localEndPoint = null;

        /// <summary>
        /// This is the constructor of this class. Just gets/saves the IP
        /// of the machine it's on
        /// </summary>
        public ftp_server()
        {
            IPAddress[] localIPs = Dns.GetHostAddresses(Dns.GetHostName());
            foreach (IPAddress addr in localIPs)
            {
                if (addr.AddressFamily == AddressFamily.InterNetwork)
                {
                    localEndPoint = new IPEndPoint(addr, 2121);
                }
            }
        }

        /// <summary>
        /// This starts the tcp listener and creates the list for connections.
        /// </summary>
        public void Start()
        {
            tcplistener = new TcpListener(IPAddress.Any, 2121);
            connections = new List<client_connection>();
            tcplistener.Start();

            listening = true;

            Console.WriteLine("Server listening on IP: {0}, Port: {1}", localEndPoint.Address, localEndPoint.Port);

            StartAccept();
        }

        /// <summary>
        /// Simple functions to begin accepting tcp clients
        /// </summary>
        private void StartAccept()
        {
            tcplistener.BeginAcceptTcpClient(HandleAcceptTcpClient, tcplistener);
        }

        /// <summary>
        /// Called when someone tries to connect to the server. Creates a new 
        /// connection object and puts it in a queue.
        /// </summary>
        /// <param name="result"></param>
        private void HandleAcceptTcpClient(IAsyncResult result)
        {
            if (listening)
            {
                StartAccept();

                TcpClient client = tcplistener.EndAcceptTcpClient(result);

                client_connection connection = new client_connection(client);

                connections.Add(connection);

                ThreadPool.QueueUserWorkItem(connection.handleClient, client);
            }
        }
    }
}