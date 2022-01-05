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

    public interface IThreadSafe { }

    public unsafe interface IPtrCompareCallback
    {
        public int Compare(byte* a, byte* b);
    }
}