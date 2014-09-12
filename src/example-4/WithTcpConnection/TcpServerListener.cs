using System;
using System.Net;
using System.Net.Sockets;

namespace WithTcpConnection
{
    internal class TcpServerListener : IEnableLog
    {
        private const int ConcurrentAccepts = 1;
        private const int SocketCloseTimeoutMs = 500;

        private readonly IPEndPoint _serverEndPoint;
        private readonly Socket _listeningSocket;
        private readonly SocketArgsPool _acceptSocketArgsPool;
        private Action<IPEndPoint, Socket> _onSocketAccepted;

        public TcpServerListener(IPEndPoint serverEndPoint)
        {
            _serverEndPoint = serverEndPoint;
            _listeningSocket = new Socket(_serverEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            _acceptSocketArgsPool = new SocketArgsPool("TcpServerListener.AcceptSocketArgsPool",
                ConcurrentAccepts*2,
                CreateAcceptSocketArgs);
        }

        private SocketAsyncEventArgs CreateAcceptSocketArgs()
        {
            var socketArgs = new SocketAsyncEventArgs();
            socketArgs.Completed += AcceptCompleted;
            return socketArgs;
        }

        private void AcceptCompleted(object sender, SocketAsyncEventArgs e)
        {
            ProcessAccept(e);
        }

        public void Start(Action<IPEndPoint, Socket> onSocketAccepted)
        {
            this.Log().Info("Starting listening on TCP endpoint: {0}.", _serverEndPoint);
            _onSocketAccepted = onSocketAccepted;
            try
            {
                _listeningSocket.Bind(_serverEndPoint);
                _listeningSocket.Listen(ConcurrentAccepts);
            }
            catch (Exception)
            {
                this.Log().Info("Failed to listen on TCP endpoint: {0}.", _serverEndPoint);
                Eat.Exception(() => _listeningSocket.Close(SocketCloseTimeoutMs));
                throw;
            }

            for (var i = 0; i < ConcurrentAccepts; ++i)
                StartAccepting();
        }

        private void StartAccepting()
        {
            //NOTE: This is exceptionally poor practice!
            var socketArgs = _acceptSocketArgsPool.Get();
            try
            {
                var firedAsync = _listeningSocket.AcceptAsync(socketArgs);
                if (!firedAsync)
                    ProcessAccept(socketArgs);
            }
            catch (ObjectDisposedException)
            {
                HandleBadAccept(socketArgs);
            }
        }

        private void HandleBadAccept(SocketAsyncEventArgs socketArgs)
        {
            Eat.Exception(() =>
            {
                if (socketArgs.AcceptSocket != null) // avoid annoying exceptions
                    socketArgs.AcceptSocket.Close(SocketCloseTimeoutMs);
            });
            socketArgs.AcceptSocket = null;
            _acceptSocketArgsPool.Return(socketArgs);
        }

        private void ProcessAccept(SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
                HandleBadAccept(e);
            else
            {
                var acceptSocket = e.AcceptSocket;
                e.AcceptSocket = null;
                _acceptSocketArgsPool.Return(e);

                OnSocketAccepted(acceptSocket);
            }

            StartAccepting();
        }

        private void OnSocketAccepted(Socket socket)
        {
            IPEndPoint socketEndPoint;
            try
            {
                socketEndPoint = (IPEndPoint) socket.RemoteEndPoint;
            }
            catch (Exception)
            {
                return;
            }

            _onSocketAccepted(socketEndPoint, socket);
        }
    }
}