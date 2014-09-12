using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace WithTcpConnection
{
    public interface ITcpConnection
    {
        Guid ConnectionId { get; }
        IPEndPoint RemoteEndPoint { get; }
        IPEndPoint LocalEndPoint { get; }
        int SendQueueSize { get; }
        bool IsClosed { get; }
        event Action<ITcpConnection, SocketError> ConnectionClosed;

        void ReceiveAsync(Action<ITcpConnection, IEnumerable<ArraySegment<byte>>> callback);
        void EnqueueSend(IEnumerable<ArraySegment<byte>> data);
        void Close(string reason);
    }
}
