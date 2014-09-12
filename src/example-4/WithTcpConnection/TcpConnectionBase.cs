using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace WithTcpConnection
{
    public class TcpConnectionBase 
    {
        private readonly IPEndPoint _remoteEndPoint;
        private int _inSendBytes;
        private bool _isClosed;
        private long _lastReceiveStarted = -1;
        private long _lastSendStarted = -1;
        private IPEndPoint _localEndPoint;
        private int _pendingReceivedBytes;
        private int _pendingSendBytes;
        private int _recvAsyncCallbacks;
        private int _recvAsyncs;
        private int _sentAsyncCallbacks;
        private int _sentAsyncs;
        private Socket _socket;
        private long _totalBytesReceived;
        private long _totalBytesSent;

        public TcpConnectionBase(IPEndPoint remoteEndPoint)
        {
            Ensure.NotNull(remoteEndPoint, "remoteEndPoint");
            _remoteEndPoint = remoteEndPoint;
        }

        public IPEndPoint RemoteEndPoint
        {
            get { return _remoteEndPoint; }
        }

        public IPEndPoint LocalEndPoint
        {
            get { return _localEndPoint; }
        }

        public int SendCalls
        {
            get { return _sentAsyncs; }
        }

        public int SendCallbacks
        {
            get { return _sentAsyncCallbacks; }
        }

        public int ReceiveCalls
        {
            get { return _recvAsyncs; }
        }

        public int ReceiveCallbacks
        {
            get { return _recvAsyncCallbacks; }
        }

        public bool IsInitialized
        {
            get { return _socket != null; }
        }

        public bool IsClosed
        {
            get { return _isClosed; }
        }

        public bool InSend
        {
            get { return Interlocked.Read(ref _lastSendStarted) >= 0; }
        }

        public bool InReceive
        {
            get { return Interlocked.Read(ref _lastReceiveStarted) >= 0; }
        }

        public int PendingSendBytes
        {
            get { return _pendingSendBytes; }
        }

        public int InSendBytes
        {
            get { return _inSendBytes; }
        }

        public int PendingReceivedBytes
        {
            get { return _pendingReceivedBytes; }
        }

        public long TotalBytesSent
        {
            get { return Interlocked.Read(ref _totalBytesSent); }
        }

        public long TotalBytesReceived
        {
            get { return Interlocked.Read(ref _totalBytesReceived); }
        }

        public bool IsReadyForSend
        {
            get
            {
                try
                {
                    return !_isClosed && _socket.Poll(0, SelectMode.SelectWrite);
                }
                catch (ObjectDisposedException)
                {
                    return false;
                }
            }
        }

        public bool IsReadyForReceive
        {
            get
            {
                try
                {
                    return !_isClosed && _socket.Poll(0, SelectMode.SelectRead);
                }
                catch (ObjectDisposedException)
                {
                    return false;
                }
            }
        }

        public bool IsFaulted
        {
            get
            {
                try
                {
                    return !_isClosed && _socket.Poll(0, SelectMode.SelectError);
                }
                catch (ObjectDisposedException)
                {
                    return false;
                }
            }
        }

        public DateTime? LastSendStarted
        {
            get
            {
                var ticks = Interlocked.Read(ref _lastSendStarted);
                return ticks >= 0 ? new DateTime(ticks) : (DateTime?) null;
            }
        }

        public DateTime? LastReceiveStarted
        {
            get
            {
                var ticks = Interlocked.Read(ref _lastReceiveStarted);
                return ticks >= 0 ? new DateTime(ticks) : (DateTime?) null;
            }
        }

        protected void InitConnectionBase(Socket socket)
        {
            Ensure.NotNull(socket, "socket");

            _socket = socket;
            _localEndPoint = Eat.Exception(() => (IPEndPoint)socket.LocalEndPoint);
        }

        protected void NotifySendEnqueued(int bytes)
        {
            Interlocked.Add(ref _pendingSendBytes, bytes);
        }

        protected void NotifySendStarting(int bytes)
        {
            if (Interlocked.CompareExchange(ref _lastSendStarted, DateTime.UtcNow.Ticks, -1) != -1)
                throw new Exception("Concurrent send detected.");
            Interlocked.Add(ref _pendingSendBytes, -bytes);
            Interlocked.Add(ref _inSendBytes, bytes);
            Interlocked.Increment(ref _sentAsyncs);
        }

        protected void NotifySendCompleted(int bytes)
        {
            Interlocked.Exchange(ref _lastSendStarted, -1);
            Interlocked.Add(ref _inSendBytes, -bytes);
            Interlocked.Add(ref _totalBytesSent, bytes);
            Interlocked.Increment(ref _sentAsyncCallbacks);
        }

        protected void NotifyReceiveStarting()
        {
            if (Interlocked.CompareExchange(ref _lastReceiveStarted, DateTime.UtcNow.Ticks, -1) != -1)
                throw new Exception("Concurrent receive detected.");
            Interlocked.Increment(ref _recvAsyncs);
        }

        protected void NotifyReceiveCompleted(int bytes)
        {
            Interlocked.Exchange(ref _lastReceiveStarted, -1);
            Interlocked.Add(ref _pendingReceivedBytes, bytes);
            Interlocked.Add(ref _totalBytesReceived, bytes);
            Interlocked.Increment(ref _recvAsyncCallbacks);
        }

        protected void NotifyReceiveDispatched(int bytes)
        {
            Interlocked.Add(ref _pendingReceivedBytes, -bytes);
        }

        protected void NotifyClosed()
        {
            _isClosed = true;
        }
    }
}