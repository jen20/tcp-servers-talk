using System;

namespace WithTcpConnection
{
    public class UnableToAllocateBufferException : Exception
    {
        public UnableToAllocateBufferException(int numberOfAttempts)
            : base(string.Format("Unable to allocate buffer after {0} attempts.", numberOfAttempts))
        {
        }
    }
}