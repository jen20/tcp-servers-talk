using System;
using System.Collections.Generic;
using System.Text;

namespace WithTcpConnection
{
    internal class CrappyTemporaryFramer : IMessageFramer
    {
        private byte[] _internalBuffer;
        private int _bufferIndex;
        private Action<ArraySegment<byte>> _receivedHandler;

        public CrappyTemporaryFramer()
        {
            _internalBuffer = new byte[1024];
            _bufferIndex = 0;
        }

        public void UnFrameData(IEnumerable<ArraySegment<byte>> data)
        {
            if (data == null)
                throw new ArgumentException("data");

            foreach (var segment in data)
                Parse(segment);
        }

        public void UnFrameData(ArraySegment<byte> data)
        {
            Parse(data);
        }

        private void Parse(ArraySegment<byte> incomingBytes)
        {
            var data = incomingBytes.Array;
            var count = Math.Min(_internalBuffer.Length - _bufferIndex, incomingBytes.Count);
            Buffer.BlockCopy(data, incomingBytes.Offset, _internalBuffer, _bufferIndex, count);
            _bufferIndex += count;

            var test = Encoding.UTF8.GetString(_internalBuffer, 0, _bufferIndex);
            if (test.Contains(Environment.NewLine))
            {
                if (_receivedHandler != null)
                    _receivedHandler(new ArraySegment<byte>(_internalBuffer, 0, _bufferIndex));

                _bufferIndex = 0;
            }
        }

        public IEnumerable<ArraySegment<byte>> FrameData(ArraySegment<byte> data)
        {
            throw new NotImplementedException();
        }

        public void RegisterMessageArrivedCallback(Action<ArraySegment<byte>> handler)
        {
            if (handler == null)
                throw new ArgumentNullException("handler");

            _receivedHandler = handler;
        }
    }
}