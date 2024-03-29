﻿using System;
using System.Threading;

/*
 * Quote from
 *
 * Professional .NET Framework 2.0 (Programmer to Programmer) (Paperback)
 * by Joe Duffy (Author)
 *
 * # Paperback: 601 pages
 * # Publisher: Wrox (April 10, 2006)
 * # Language: English
 * # ISBN-10: 0764571354
 * # ISBN-13: 978-0764571350
 */

namespace WithTcpConnection
{
    public class SpinLock2
    {
        private int _state;
        private readonly EventWaitHandle _available = new AutoResetEvent(false);

        // This looks at the total number of hardware threads available; if it's
        // only 1, we will use an optimized code path
        private static readonly bool IsSingleProc = (Environment.ProcessorCount == 1);

        private const int OuterTryCount = 5;
        private const int CompareExchangeTryCount = 100;

        public void Enter(out bool taken)
        {
            // Taken is an out parameter so that we set it *inside* the critical
            // region, rather than returning it and permitting aborts to creep in.
            // Without this, the caller could take the lock, but not release it
            // because it didn't know it had to.
            taken = false;

            while (!taken)
            {
                if (IsSingleProc)
                {
                    // Don't busy wait on 1-logical processor machines; try
                    // a single swap, and if it fails, drop back to EventWaitHandle.
                    Thread.BeginCriticalRegion();
                    taken = Interlocked.CompareExchange(ref _state, 1, 0) == 0;
                    if (!taken)
                        Thread.EndCriticalRegion();
                }
                else
                {
                    for (var i = 0; !taken && i < OuterTryCount; i++)
                    {
                        // Tell the CLR we're in a critical region;
                        // interrupting could lead to deadlocks.
                        Thread.BeginCriticalRegion();

                        // Try 'cexTryCount' times to CEX the state variable:
                        var tries = 0;
                        while (!(taken = Interlocked.CompareExchange(ref _state, 1, 0) == 0) && tries++ < CompareExchangeTryCount)
                        {
                            Thread.SpinWait(1);
                        }

                        if (taken)
                            continue;
                        
                        // We failed to acquire in the busy spin, mark the end
                        // of our critical region and yield to let another
                        // thread make forward progress.
                        Thread.EndCriticalRegion();
                        Thread.Sleep(0);
                    }
                }

                // If we didn't acquire the lock, block.
                if (!taken)
                    _available.WaitOne();
            }
        }

        public LockReleaserSlim Acquire()
        {
            bool taken;
            Enter(out taken);
            
            if (taken)
                return new LockReleaserSlim(this);
            throw new Exception("Unable to acquire lock, this shouldn't happen.");
        }

        public void Exit()
        {
            if (Interlocked.CompareExchange(ref _state, 0, 1) != 1)
                return;

            // We notify the waking threads inside our critical region so
            // that an abort doesn't cause us to lose a pulse, (which could
            // lead to deadlocks).
            _available.Set();
            Thread.EndCriticalRegion();
        }
    }
}