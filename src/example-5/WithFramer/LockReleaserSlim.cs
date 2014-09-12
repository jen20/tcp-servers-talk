using System;

namespace WithTcpConnection
{
    /// <summary>
    /// Be very careful using this!
    /// </summary>
    public struct LockReleaserSlim : IDisposable
    {
        private readonly SpinLock2 _spinLock;

        public LockReleaserSlim(SpinLock2 spinLock)
        {
            _spinLock = spinLock;
        }

        public void Dispose()
        {
            _spinLock.Exit();
        }
    }
}