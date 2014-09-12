using System;
using System.Collections.Generic;
using System.Net.Sockets;

namespace WithTcpConnection
{
    /// <summary>
    /// Encodes outgoing messages in frames and decodes incoming frames. 
    /// For decoding it uses an internal state, raising the registered 
    /// callback once a full message has arrived
    /// </summary>
    public interface IMessageFramer
    {
        void UnFrameData(IEnumerable<ArraySegment<byte>> data);
        void UnFrameData(ArraySegment<byte> data);
        IEnumerable<ArraySegment<byte>> FrameData(ArraySegment<byte> data);

        void RegisterMessageArrivedCallback(Action<ArraySegment<byte>> handler);
    }
}