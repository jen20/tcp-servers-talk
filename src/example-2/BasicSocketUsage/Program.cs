using System;
using System.Net;
using System.Threading;

namespace BasicSocketUsage
{
    public class Program
    {
        private static void ThreadMain()
        {
            var endPoint = new IPEndPoint(IPAddress.Loopback, 11000);
            var server = new BasicSocketServer(endPoint);
            server.Start();
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
