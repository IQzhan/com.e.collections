using System;
using System.Threading;

namespace E.Collections
{
    public unsafe struct Lock : IDisposable
    {
        private readonly int* m_Location;

        public Lock(int* location)
        {
            m_Location = location;
            InternalLock();
        }

        public void Dispose()
        {
            InternalUnlock();
        }

        private void InternalLock()
        {
            while (1 == Interlocked.Exchange(ref *m_Location, 1)) ;
        }

        private void InternalUnlock()
        {
            Interlocked.Exchange(ref *m_Location, 0);
        }
    }
}