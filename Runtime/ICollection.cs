using System;

namespace E.Collections
{
    public interface ICollection : IDisposable
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
}