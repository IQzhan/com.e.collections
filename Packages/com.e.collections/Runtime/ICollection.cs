using System.Collections;
using System.Collections.Generic;

namespace E.Collections
{
    public interface ICollection :
        IEnumerable
    {
        public bool IsCreated { get; }
        public int Count { get; }
        public void Clear();
    }

    public interface ICollection<T> :
        IEnumerable<T>
        where T : unmanaged
    { }

    public interface IResizeable
    {
        public void Extend(int count);
    }

    public interface IChunked
    {
        public long ChunkSize { get; }
        public int ChunkCount { get; }
        public int ElementSize { get; }
    }

    public interface ILockable
    {
        public SpinLock GetLock();
    }

    public interface ILockable<Key>
    {
        public SpinLock GetLock(Key key);
    }

    public unsafe interface IPtrCompareCallback
    {
        public int Compare(byte* a, byte* b);
    }
}