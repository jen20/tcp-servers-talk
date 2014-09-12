using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace WithTcpConnection
{
    public class BasicEchoServer : IEnableLog
    {
        private readonly TcpServerListener _serverListener;

        public BasicEchoServer(IPEndPoint serverEndPoint)
        {
            _serverListener = new TcpServerListener(serverEndPoint);
        }

        public void Start()
        {
            _serverListener.Start(OnConnectionAccepted);
        }

        private void OnConnectionAccepted(IPEndPoint endPoint, Socket socket)
        {
            var conn = TcpConnection.CreateAcceptedTcpConnection(Guid.NewGuid(), endPoint, socket, verbose: true);
            this.Log().Info("TCP connection accepted: [{0}, L{1}, {2:B}].", conn.RemoteEndPoint, conn.LocalEndPoint, conn.ConnectionId);

            conn.ConnectionClosed += ConnectionClosed;
            conn.ReceiveAsync(OnDataReceived);
        }

        private void OnDataReceived(ITcpConnection conn, IEnumerable<ArraySegment<byte>> data)
        {
            conn.EnqueueSend(data);
            conn.ReceiveAsync(OnDataReceived);
        }

        private void ConnectionClosed(ITcpConnection conn, SocketError error)
        {
            this.Log().Info("Tcp connection closed: [{0}, L{1}, {2:B}].", conn.RemoteEndPoint, conn.LocalEndPoint,
                    conn.ConnectionId);
        }
    }
}