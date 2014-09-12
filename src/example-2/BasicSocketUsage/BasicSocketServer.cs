using System;
using System.Net;
using System.Net.Sockets;

namespace BasicSocketUsage
{
    class BasicSocketServer : IEnableLog
    {
        private const int ConcurrentAccepts = 2;
        private const int SocketCloseTimeoutMs = 500;

        private readonly IPEndPoint _serverEndPoint;
        private readonly Socket _listeningSocket;

        public BasicSocketServer(IPEndPoint serverEndPoint)
        {
            _serverEndPoint = serverEndPoint;
            _listeningSocket = new Socket(_serverEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        }

        public void Start()
        {
            this.Log().Info("Starting listening on TCP endpoint: {0}.", _serverEndPoint);
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
            var socketArgs = new SocketAsyncEventArgs();
            socketArgs.Completed += (s, e) => ProcessAccept(e);
            try
            {
                var firedAsync = _listeningSocket.AcceptAsync(socketArgs);
                if (!firedAsync)
                    ProcessAccept(socketArgs);
            }
            catch (ObjectDisposedException)
            {
                Eat.Exception(() =>
                {
                    if (socketArgs.AcceptSocket != null)
                        socketArgs.AcceptSocket.Close(SocketCloseTimeoutMs);
                });
            }
        }

        private void ProcessAccept(SocketAsyncEventArgs socketArgs)
        {
            this.Log().Info("Started processing an accept from {0}", (IPEndPoint)socketArgs.AcceptSocket.RemoteEndPoint);
            if (socketArgs.SocketError != SocketError.Success)
            {
                Eat.Exception(() =>
                {
                    if (socketArgs.AcceptSocket != null)
                        socketArgs.AcceptSocket.Close(SocketCloseTimeoutMs);
                });
            }
            else
            {
                //TODO: We've done accepting, here we'll just close immediately.
                // (I know, right, most irritating server evar.
                this.Log().Info("Closing the accepted socket to {0}", (IPEndPoint)socketArgs.AcceptSocket.RemoteEndPoint);
                socketArgs.AcceptSocket.Close();
            }

            StartAccepting();
        }
    }
}