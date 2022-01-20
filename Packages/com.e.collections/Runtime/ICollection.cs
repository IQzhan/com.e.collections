namespace E.Collections
{
    public interface ICollection
    {
        public bool IsCreated { get; }
        public int Count { get; }
        public void Clear();
    }

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
        public Lock GetLock();
    }

    public interface ILockable<Key>
    {
        public Lock GetLock(Key key);
    }

    public unsafe interface IPtrIndexable
    {
        public byte* this[int index] { get; }
    }

    public unsafe interface IPtrCompareCallback
    {
        public int Compare(byte* a, byte* b);
    }
}