using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace BasicSocketUsage
{
    public class Program : IEnableLog
    {
        private static void ThreadMain()
        {
            var endPoint = new IPEndPoint(IPAddress.Loopback, 11000);
            var server = new BasicSocketServer(endPoint);
            server.Start(HandleConnection);
        }

        private static void HandleConnection(IPEndPoint clientEndPoint, Socket clientSocket)
        {
            LoggerExtensions.Log(null).Info("Accepted connection from: {0}", clientEndPoint);
            //clientSocket.Close();
        }


        public static void Main(string[] args)
        {
            Console.WriteLine("Press <ENTER> to stop the server");

            var serverThread = new Thread(ThreadMain);
            serverThread.Start();

            Console.ReadLine();
        }
    }
}
