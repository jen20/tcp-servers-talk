﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace WithTcpConnection
{
    public class TcpConnection : TcpConnectionBase, ITcpConnection, IEnableLog
    {
        internal const int MaxSendPacketSize = 64 * 1024;
        internal static readonly BufferManager BufferManager;
        private static readonly SocketArgsPool SocketArgsPool;

        static TcpConnection()
        {
            BufferManager = new BufferManager(TcpConfiguration.BufferChunksCount, TcpConfiguration.SocketBufferSize);
            SocketArgsPool = new SocketArgsPool("TcpConnection.SocketArgsPool", TcpConfiguration.SendReceivePoolSize, () => new SocketAsyncEventArgs());
        }

        public static ITcpConnection CreateConnectingTcpConnection(Guid connectionId,
                                                                   IPEndPoint remoteEndPoint,
                                                                   TcpClientConnector connector,
                                                                   TimeSpan connectionTimeout,
                                                                   Action<ITcpConnection> onConnectionEstablished,
                                                                   Action<ITcpConnection, SocketError> onConnectionFailed,
                                                                   bool verbose)
        {
            var connection = new TcpConnection(connectionId, remoteEndPoint, verbose);
            // ReSharper disable ImplicitlyCapturedClosure
            connector.InitConnect(remoteEndPoint,
                                  (_, socket) => {
                                      connection.InitSocket(socket);
                                      if (onConnectionEstablished != null)
                                          onConnectionEstablished(connection);
                                  },
                                  (_, socketError) => {
                                      if (onConnectionFailed != null)
                                          onConnectionFailed(connection, socketError);
                                  },
                                  connection, connectionTimeout);
            // ReSharper restore ImplicitlyCapturedClosure
            return connection;
        }

        public static ITcpConnection CreateAcceptedTcpConnection(Guid connectionId,
                                                                 IPEndPoint remoteEndPoint,
                                                                 Socket socket,
                                                                 bool verbose)
        {
            var connection = new TcpConnection(connectionId, remoteEndPoint, verbose);
            connection.InitSocket(socket);
            return connection;
        }

        public event Action<ITcpConnection, SocketError> ConnectionClosed;
        public Guid ConnectionId { get { return _connectionId; } }
        public int SendQueueSize { get { return _sendQueue.Count; } }

        private readonly Guid _connectionId;
        private readonly bool _verbose;

        private Socket _socket;
        private SocketAsyncEventArgs _receiveSocketArgs;
        private SocketAsyncEventArgs _sendSocketArgs;

        private readonly ConcurrentQueue<ArraySegment<byte>> _sendQueue;
        private readonly Queue<ReceivedData> _receiveQueue;
        private readonly MemoryStream _memoryStream;

        private readonly object _receivingLock = new object();
        private readonly SpinLock2 _sendingLock = new SpinLock2();
        private bool _isSending;
        private volatile int _closed;

        private Action<ITcpConnection, IEnumerable<ArraySegment<byte>>> _receiveCallback;

        private TcpConnection(Guid connectionId, IPEndPoint remoteEndPoint, bool verbose)
            : base(remoteEndPoint)
        {
            Ensure.NotEmptyGuid(connectionId, "connectionId");

            _sendQueue = new ConcurrentQueue<ArraySegment<byte>>();
            _receiveQueue = new Queue<ReceivedData>();
            _memoryStream = new MemoryStream();

            _connectionId = connectionId;
            _verbose = verbose;
        }

        private void InitSocket(Socket socket)
        {
            InitConnectionBase(socket);
            using (_sendingLock.Acquire())
            {
                _socket = socket;
                try
                {
                    socket.NoDelay = true;
                }
                catch (ObjectDisposedException)
                {
                    CloseInternal(SocketError.Shutdown, "Socket disposed.");
                    _socket = null;
                    return;
                }

                _receiveSocketArgs = SocketArgsPool.Get();
                _receiveSocketArgs.AcceptSocket = socket;
                _receiveSocketArgs.Completed += OnReceiveAsyncCompleted;

                _sendSocketArgs = SocketArgsPool.Get();
                _sendSocketArgs.AcceptSocket = socket;
                _sendSocketArgs.Completed += OnSendAsyncCompleted;
            }
            StartReceive();
            TrySend();
        }

        public void EnqueueSend(IEnumerable<ArraySegment<byte>> data)
        {
            using (_sendingLock.Acquire())
            {
                var bytes = 0;
                foreach (var segment in data)
                {
                    _sendQueue.Enqueue(segment);
                    bytes += segment.Count;
                }
                NotifySendEnqueued(bytes);
            }
            TrySend();
        }

        private void TrySend()
        {
            using (_sendingLock.Acquire())
            {
                if (_isSending || _sendQueue.Count == 0 || _socket == null) return;
                _isSending = true;
            }

            _memoryStream.SetLength(0);

            ArraySegment<byte> sendPiece;
            while (_sendQueue.TryDequeue(out sendPiece))
            {
                _memoryStream.Write(sendPiece.Array, sendPiece.Offset, sendPiece.Count);
                if (_memoryStream.Length >= MaxSendPacketSize)
                    break;
            }

            _sendSocketArgs.SetBuffer(_memoryStream.GetBuffer(), 0, (int)_memoryStream.Length);

            try
            {
                NotifySendStarting(_sendSocketArgs.Count);
                var firedAsync = _sendSocketArgs.AcceptSocket.SendAsync(_sendSocketArgs);
                if (!firedAsync)
                    ProcessSend(_sendSocketArgs);
            }
            catch (ObjectDisposedException)
            {
                ReturnSendingSocketArgs();
            }
        }

        private void OnSendAsyncCompleted(object sender, SocketAsyncEventArgs e)
        {
            // No other code should go here. Handling is the same for sync/async completion.
            ProcessSend(e);
        }

        private void ProcessSend(SocketAsyncEventArgs socketArgs)
        {
            if (socketArgs.SocketError != SocketError.Success)
            {
                NotifySendCompleted(0);
                ReturnSendingSocketArgs();
                CloseInternal(socketArgs.SocketError, "Socket send error");
            }
            else
            {
                NotifySendCompleted(socketArgs.Count);

                if (_closed != 0)
                    ReturnSendingSocketArgs();
                else
                {
                    using (_sendingLock.Acquire())
                    {
                        _isSending = false;
                    }
                    TrySend();
                }
            }
        }

        public void ReceiveAsync(Action<ITcpConnection, IEnumerable<ArraySegment<byte>>> callback)
        {
            if (callback == null)
                throw new ArgumentNullException("callback");

            lock (_receivingLock)
            {
                if (_receiveCallback != null)
                {
                    this.Log().Fatal("ReceiveAsync called again while previous call wasn't fulfilled");
                    throw new InvalidOperationException("ReceiveAsync called again while previous call wasn't fulfilled");
                }
                _receiveCallback = callback;
            }

            TryDequeueReceivedData();
        }

        private void StartReceive()
        {
            var buffer = BufferManager.CheckOut();
            if (buffer.Array == null || buffer.Count == 0 || buffer.Array.Length < buffer.Offset + buffer.Count)
                throw new Exception("Invalid buffer allocated");
            // TODO AN: do we need to lock on _receiveSocketArgs?..
            lock (_receiveSocketArgs)
            {
                _receiveSocketArgs.SetBuffer(buffer.Array, buffer.Offset, buffer.Count);
                if (_receiveSocketArgs.Buffer == null) throw new Exception("Buffer was not set");
            }
            try
            {
                NotifyReceiveStarting();
                bool firedAsync;
                lock (_receiveSocketArgs)
                {
                    if (_receiveSocketArgs.Buffer == null) throw new Exception("Buffer was lost");
                    firedAsync = _receiveSocketArgs.AcceptSocket.ReceiveAsync(_receiveSocketArgs);
                }
                if (!firedAsync)
                    ProcessReceive(_receiveSocketArgs);
            }
            catch (ObjectDisposedException)
            {
                ReturnReceivingSocketArgs();
            }
        }

        private void OnReceiveAsyncCompleted(object sender, SocketAsyncEventArgs e)
        {
            // No other code should go here.  All handling is the same on async and sync completion.
            ProcessReceive(e);
        }

        private void ProcessReceive(SocketAsyncEventArgs socketArgs)
        {
            // socket closed normally or some error occurred
            if (socketArgs.BytesTransferred == 0 || socketArgs.SocketError != SocketError.Success)
            {
                NotifyReceiveCompleted(0);
                ReturnReceivingSocketArgs();
                CloseInternal(socketArgs.SocketError, socketArgs.SocketError != SocketError.Success ? "Socket receive error" : "Socket closed");
                return;
            }

            NotifyReceiveCompleted(socketArgs.BytesTransferred);

            lock (_receivingLock)
            {
                var buf = new ArraySegment<byte>(socketArgs.Buffer, socketArgs.Offset, socketArgs.Count);
                _receiveQueue.Enqueue(new ReceivedData(buf, socketArgs.BytesTransferred));
            }

            lock (_receiveSocketArgs)
            {
                if (socketArgs.Buffer == null)
                    throw new Exception("Cleaning already null buffer");
                socketArgs.SetBuffer(null, 0, 0);
            }

            StartReceive();
            TryDequeueReceivedData();
        }

        private void TryDequeueReceivedData()
        {
            Action<ITcpConnection, IEnumerable<ArraySegment<byte>>> callback;
            List<ReceivedData> res;
            lock (_receivingLock)
            {
                // no awaiting callback or no data to dequeue
                if (_receiveCallback == null || _receiveQueue.Count == 0)
                    return;

                res = new List<ReceivedData>(_receiveQueue.Count);
                while (_receiveQueue.Count > 0)
                {
                    res.Add(_receiveQueue.Dequeue());
                }

                callback = _receiveCallback;
                _receiveCallback = null;
            }

            var data = new ArraySegment<byte>[res.Count];
            var bytes = 0;
            for (var i = 0; i < data.Length; ++i)
            {
                var d = res[i];
                bytes += d.DataLen;
                data[i] = new ArraySegment<byte>(d.Buf.Array, d.Buf.Offset, d.DataLen);
            }
            callback(this, data);

            for (int i = 0, n = res.Count; i < n; ++i)
            {
                BufferManager.CheckIn(res[i].Buf); // dispose buffers
            }
            NotifyReceiveDispatched(bytes);
        }

        public void Close(string reason)
        {
            CloseInternal(SocketError.Success, reason ?? "Normal socket close."); // normal socket closing
        }

        private void CloseInternal(SocketError socketError, string reason)
        {
#pragma warning disable 420
            if (Interlocked.CompareExchange(ref _closed, 1, 0) != 0)
                return;
#pragma warning restore 420

            NotifyClosed();

            if (_verbose)
            {
                this.Log().Info("{12} closed [{0:HH:mm:ss.fff}: N{1}, L{2}, {3:B}]:\nReceived bytes: {4}, Sent bytes: {5}\n"
                         + "Send calls: {6}, callbacks: {7}\nReceive calls: {8}, callbacks: {9}\nClose reason: [{10}] {11}\n",
                         DateTime.UtcNow, RemoteEndPoint, LocalEndPoint, _connectionId,
                         TotalBytesReceived, TotalBytesSent,
                         SendCalls, SendCallbacks,
                         ReceiveCalls, ReceiveCallbacks,
                         socketError, reason, GetType().Name);
            }

            if (_socket != null)
            {
                Eat.Exception(() => _socket.Shutdown(SocketShutdown.Both));
                Eat.Exception(() => _socket.Close(TcpConfiguration.SocketCloseTimeoutMs));
                _socket = null;
            }

            using (_sendingLock.Acquire())
            {
                if (!_isSending)
                    ReturnSendingSocketArgs();
            }

            var handler = ConnectionClosed;
            if (handler != null)
                handler(this, socketError);
        }

        private void ReturnSendingSocketArgs()
        {
            var socketArgs = Interlocked.Exchange(ref _sendSocketArgs, null);
            if (socketArgs == null)
                return;
            
            socketArgs.Completed -= OnSendAsyncCompleted;
            socketArgs.AcceptSocket = null;
            if (socketArgs.Buffer != null)
                socketArgs.SetBuffer(null, 0, 0);
            SocketArgsPool.Return(socketArgs);
        }

        private void ReturnReceivingSocketArgs()
        {
            var socketArgs = Interlocked.Exchange(ref _receiveSocketArgs, null);
            if (socketArgs == null)
                return;
            
            socketArgs.Completed -= OnReceiveAsyncCompleted;
            socketArgs.AcceptSocket = null;
            if (socketArgs.Buffer != null)
            {
                BufferManager.CheckIn(new ArraySegment<byte>(socketArgs.Buffer, socketArgs.Offset, socketArgs.Count));
                socketArgs.SetBuffer(null, 0, 0);
            }
            SocketArgsPool.Return(socketArgs);
        }

        public override string ToString()
        {
            return RemoteEndPoint.ToString();
        }

        private struct ReceivedData
        {
            public readonly ArraySegment<byte> Buf;
            public readonly int DataLen;

            public ReceivedData(ArraySegment<byte> buf, int dataLen)
            {
                Buf = buf;
                DataLen = dataLen;
            }
        }
    }
}