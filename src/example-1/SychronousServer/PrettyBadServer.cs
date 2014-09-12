using System;
using System.Net;
using System.Net.Sockets;

namespace SynchronousServer
{
    public class PrettyBadServer : IEnableLog
    {
        public void Run()
        {
            const int maxConcurrentAccepts = 2;

            var serverEndPoint = new IPEndPoint(IPAddress.Loopback, 11000);
            this.Log().Info("Starting server on {0}", serverEndPoint);
            
            Socket socketListener = null;
            try
            {
                socketListener = new Socket(serverEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                socketListener.Bind(serverEndPoint);
                socketListener.Listen(maxConcurrentAccepts);
            }
            catch (SocketException e)
            {
                this.Log().Error("Error binding and listening {0} - {1}", e.ErrorCode, e.Message);
                Environment.Exit(e.ErrorCode);
            }

            var receiveBuffer = new byte[1024];

            while (true)
            {
                try
                {
                    var client = socketListener.Accept();

                    this.Log().Info("Handling client {0}", client.RemoteEndPoint);

                    var bytesEchoed = 0;
                    int bytesReceived;
                    while ((bytesReceived = client.Receive(receiveBuffer, 0, receiveBuffer.Length, SocketFlags.None)) >
                           0)
                    {
                        client.Send(receiveBuffer, 0, bytesReceived, SocketFlags.None);
                        bytesEchoed += bytesReceived;
                    }
                    this.Log().Info("Echoed {0} bytes", bytesEchoed);

                    client.Close();
                }
                catch (Exception e)
                {
                    this.Log().Info("Error: {0}", e.Message);
                }
            }
        }
    }
}