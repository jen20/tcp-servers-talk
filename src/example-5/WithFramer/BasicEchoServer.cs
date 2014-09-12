using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace WithTcpConnection
{
    public class BasicEchoServer : IEnableLog
    {
        private readonly TcpServerListener _serverListener;
        private ConcurrentDictionary<Guid, Tuple<ITcpConnection, IMessageFramer>> _clientFramers;

        public BasicEchoServer(IPEndPoint serverEndPoint)
        {
            _serverListener = new TcpServerListener(serverEndPoint);
	    _clientFramers = new ConcurrentDictionary<Guid, Tuple<ITcpConnection, IMessageFramer>>();
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
            IMessageFramer framer;
            Tuple<ITcpConnection, IMessageFramer> pair;
            if (!_clientFramers.TryGetValue(conn.ConnectionId, out pair))
            {
                framer = new CrappyTemporaryFramer();
                framer.RegisterMessageArrivedCallback(CompleteMessageArrived);
                _clientFramers.TryAdd(conn.ConnectionId, new Tuple<ITcpConnection, IMessageFramer>(conn, framer));

                //Note: we stick the connection ID in the first part of the message just so we
                // can find it later. This isn't especially nice and is fixed in real code
                var connectionId = conn.ConnectionId.ToByteArray();
                framer.UnFrameData(new ArraySegment<byte>(connectionId, 0, connectionId.Length));
            }
            else
            {
                framer = pair.Item2;
            }

            framer.UnFrameData(data);
            conn.ReceiveAsync(OnDataReceived);
        }

        private void CompleteMessageArrived(ArraySegment<byte> data)
        {
            //We deliberately stuck the connection ID in the message...
            var connectionIdBytes = new byte[16];
            for (int i = data.Offset, n = data.Offset + 16; i < n; i++)
            {
                connectionIdBytes[i - data.Offset] = data.Array[i];
            }

            var connectionId = new Guid(connectionIdBytes);

            Tuple<ITcpConnection, IMessageFramer> pair;
            if (!_clientFramers.TryRemove(connectionId, out pair))
                throw new Exception("How did this happen?");

            var connection = pair.Item1;
            var segmentToSend = new ArraySegment<byte>(data.Array, data.Offset + 16, data.Count - 16);

            connection.EnqueueSend(new[] { segmentToSend });
        }

        private void ConnectionClosed(ITcpConnection conn, SocketError error)
        {
            this.Log().Info("Tcp connection closed: [{0}, L{1}, {2:B}].", conn.RemoteEndPoint, conn.LocalEndPoint,
                    conn.ConnectionId);
        }
    }
}