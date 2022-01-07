using System;
using System.Diagnostics;
using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace E.Collections.Unsafe
{
    public unsafe struct UnsafeChunkedQueue : ICollection, IChunked, IResizeable, IDisposable, IEquatable<UnsafeChunkedQueue>
    {
        #region No ThreadSafe

        private struct Head
        {
            public int existsMark;
            public Allocator allocator;
            public int elementSize;
            public int elementCount;
            public int elementStart;
            public int chunkCount;
            public int elementCountInChunk;
            public int maxElementCount;
            public int lockedMark;
            public int maxChunkCount;
            public long chunkSize;
            public long fixedChunkSize;
            public byte** chunks;
        }

        [NativeDisableUnsafePtrRestriction]
        private Head* m_Head;

        private const int ExistsMark = 1000002;

        private const int ExpendCount = 32;

        public bool IsCreated => m_Head != null && m_Head->existsMark == ExistsMark;

        public int Count => IsCreated ? m_Head->elementCount : 0;

        public long ChunkSize => IsCreated ? m_Head->chunkSize : 0;

        public int ChunkCount => IsCreated ? m_Head->chunkCount : 0;

        public int ElementSize => IsCreated ? m_Head->elementSize : 0;

        public UnsafeChunkedQueue(int elementSize, long chunkSize, Allocator allocator)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (elementSize <= 0)
            {
                throw new ArgumentException("elementSize must bigger then 0.");
            }
            if (chunkSize <= 0)
            {
                throw new ArgumentException("chunkSize must bigger then 0.");
            }
            if (elementSize > chunkSize)
            {
                throw new ArgumentException("chunkSize must bigger then elementSize.");
            }
#endif
            m_Head = (Head*)Memory.Malloc<Head>(1, allocator);
            *m_Head = new Head()
            {
                existsMark = ExistsMark,
                allocator = allocator,
                elementSize = elementSize,
                elementCount = 0,
                elementStart = 0,
                chunkSize = chunkSize,
                fixedChunkSize = chunkSize - (chunkSize % elementSize),
                chunkCount = 0,
                elementCountInChunk = (int)(chunkSize / elementSize),
                maxElementCount = 0,
                maxChunkCount = ExpendCount,
                chunks = (byte**)Memory.Malloc<byte>(Memory.PtrSize * ExpendCount, allocator),
                lockedMark = 0
            };
        }

        /// <summary>
        /// Get but not remove.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public byte* this[int index] => Peek(index);

        /// <summary>
        /// Enqueue.
        /// </summary>
        /// <returns></returns>
        public byte* Enqueue()
        {
            CheckExists();
            return InternalEnqueue();
        }

        /// <summary>
        /// Dequeue.
        /// </summary>
        /// <returns></returns>
        public byte* Dequeue()
        {
            CheckExists();
            return InternalDequeue();
        }

        /// <summary>
        /// Get but not reamove.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public byte* Peek(int index)
        {
            CheckExists();
            CheckIndexNoLock(index);
            return InternalPeek(index);
        }

        /// <summary>
        /// Extend.
        /// </summary>
        /// <param name="count"></param>
        public void Extend(int count)
        {
            CheckExists();
            InternalExtend(count);
        }

        /// <summary>
        /// Clear.
        /// </summary>
        public void Clear()
        {
            CheckExists();
            m_Head->elementCount = 0;
            m_Head->elementStart = 0;
        }

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
            CheckExists();
            m_Head->existsMark = 0;
            for (int i = 0; i < m_Head->chunkCount; i++)
            {
                Memory.Free(*(m_Head->chunks + i), m_Head->allocator);
            }
            Memory.Free(m_Head->chunks, m_Head->allocator);
            Memory.Free(m_Head, m_Head->allocator);
            m_Head = null;
        }

        public override bool Equals(object obj) => obj is UnsafeChunkedQueue queue && m_Head == queue.m_Head;

        public bool Equals(UnsafeChunkedQueue other) => m_Head == other.m_Head;

        public override int GetHashCode() => m_Head != null ? (int)m_Head : 0;

        public static bool operator ==(UnsafeChunkedQueue left, UnsafeChunkedQueue right) => left.m_Head == right.m_Head;

        public static bool operator !=(UnsafeChunkedQueue left, UnsafeChunkedQueue right) => left.m_Head != right.m_Head;

        #endregion

        #region ThreadSafe

        public ThreadSafe AsThreadSafe() => new ThreadSafe(this);

        public struct ThreadSafe : ICollection, IChunked, IResizeable, IThreadSafe, IEquatable<ThreadSafe>
        {
            public UnsafeChunkedQueue AsNoThreadSafe() => m_Instance;

            public ThreadSafe(UnsafeChunkedQueue instance) => m_Instance = instance;

            private readonly UnsafeChunkedQueue m_Instance;

            public bool IsCreated => m_Instance.IsCreated;

            public int Count => m_Instance.LockedCount();

            public long ChunkSize => m_Instance.ChunkSize;

            public int ChunkCount => m_Instance.LockedChunkCount();

            public int ElementSize => m_Instance.ElementSize;

            public byte* this[int index] => m_Instance.LockedPeek(index);

            public byte* Enqueue() => m_Instance.LockedEnqueue();

            public byte* Dequeue() => m_Instance.LockedDequeue();

            public byte* Peek(int index) => m_Instance.LockedPeek(index);

            public void Clear() => m_Instance.LockedClear();

            public void Extend(int count) => m_Instance.LockedExtend(count);

            public override bool Equals(object obj) => obj is ThreadSafe safe && m_Instance == safe.m_Instance;

            public bool Equals(ThreadSafe other) => m_Instance == other.m_Instance;

            public override int GetHashCode() => m_Instance.GetHashCode();

            public static bool operator ==(ThreadSafe left, ThreadSafe right) => left.m_Instance == right.m_Instance;

            public static bool operator !=(ThreadSafe left, ThreadSafe right) => left.m_Instance != right.m_Instance;
        }

        private int LockedCount()
        {
            if (!IsCreated) return 0;
            Lock();
            int count = m_Head->elementCount;
            Unlock();
            return count;
        }

        private int LockedChunkCount()
        {
            if (!IsCreated) return 0;
            Lock();
            int count = m_Head->chunkCount;
            Unlock();
            return count;
        }

        private byte* LockedEnqueue()
        {
            CheckExists();
            Lock();
            byte* result = InternalEnqueue();
            Unlock();
            return result;
        }

        private byte* LockedDequeue()
        {
            CheckExists();
            Lock();
            byte* result = InternalDequeue();
            Unlock();
            return result;
        }

        private byte* LockedPeek(int index)
        {
            CheckExists();
            Lock();
            CheckIndex(index);
            byte* result = InternalPeek(index);
            Unlock();
            return result;
        }

        private void LockedExtend(int count)
        {
            CheckExists();
            Lock();
            InternalExtend(count);
            Unlock();
        }

        private void LockedClear()
        {
            CheckExists();
            Lock();
            m_Head->elementCount = 0;
            m_Head->elementStart = 0;
            Unlock();
        }

        #endregion

        #region Internal

        private byte* InternalEnqueue()
        {
            InternalExtend(1);
            byte* ptr = GetDataPtr((m_Head->elementStart + m_Head->elementCount++) % m_Head->maxElementCount);
            return ptr;
        }

        private byte* InternalDequeue()
        {
            if (m_Head->elementCount < 1) return default;
            byte* ptr = GetDataPtr(m_Head->elementStart++);
            m_Head->elementStart %= m_Head->maxElementCount;
            --m_Head->elementCount;
            return ptr;
        }

        private byte* InternalPeek(int index)
        {
            if (m_Head->elementCount < 1) return default;
            byte* ptr = GetDataPtr((m_Head->elementStart + index) % m_Head->maxElementCount);
            return ptr;
        }

        private void InternalExtend(int count)
        {
            int targetCount = m_Head->elementCount + count;
            if (targetCount <= m_Head->maxElementCount) return;
            int targetChunkCount = (int)Math.Ceiling((double)targetCount / m_Head->elementCountInChunk);
            int maxChunkCount = m_Head->maxChunkCount;
            Allocator allocator = m_Head->allocator;
            if (targetChunkCount > maxChunkCount)
            {
                // extend m_Head->chunks size
                long oldSize = Memory.PtrSize * maxChunkCount;
                maxChunkCount = targetChunkCount + ExpendCount;
                long newSize = Memory.PtrSize * maxChunkCount;
                byte** tempChunks = (byte**)Memory.Malloc<byte>(newSize, allocator);
                byte** chunks = m_Head->chunks;
                UnsafeUtility.MemCpy(tempChunks, chunks, oldSize);
                Memory.Free(chunks, allocator);
                m_Head->chunks = tempChunks;
                m_Head->maxChunkCount = maxChunkCount;
            }
            // new chunks
            for (int i = m_Head->chunkCount; i < targetChunkCount; i++)
            {
                *(m_Head->chunks + i) = (byte*)Memory.Malloc<byte>(m_Head->chunkSize, allocator);
            }
            if (m_Head->maxElementCount > 0)
            {
                // move elements
                int startIndex = m_Head->elementStart;
                int endIndex = (startIndex + m_Head->chunkCount - 1) % m_Head->maxElementCount;
                if (endIndex < startIndex)
                {
                    // move elements before start
                    GetDataPos(endIndex, out int endInChunk, out long endPosInChunk);
                    byte* endAtChunk = *(m_Head->chunks + endInChunk);
                    byte* firstNewChunk = *(m_Head->chunks + m_Head->chunkCount);
                    UnsafeUtility.MemCpy(firstNewChunk, endAtChunk, endPosInChunk + m_Head->elementSize);
                    if (endInChunk > 0)
                    {
                        //move chunks [0, endInChunk) to m_Head->chunkCount
                        int beforeCount = endInChunk;
                        int midCount = m_Head->chunkCount - endInChunk;
                        long beforeSize = Memory.PtrSize * beforeCount;
                        long midSize = Memory.PtrSize * midCount;
                        byte** tempChunks = (byte**)Memory.Malloc(beforeSize, 1, Allocator.Temp);
                        UnsafeUtility.MemCpy(tempChunks, m_Head->chunks, beforeSize);
                        UnsafeUtility.MemMove(m_Head->chunks, m_Head->chunks + beforeCount, midSize);
                        UnsafeUtility.MemCpy(m_Head->chunks + midCount, tempChunks, beforeSize);
                        Memory.Free(tempChunks, Allocator.Temp);
                    }
                    // reset start
                    GetDataPos(startIndex, out int startInChunk, out long startPosInChunk);
                    m_Head->elementStart = (int)(startPosInChunk / m_Head->elementSize);
                }
            }
            m_Head->chunkCount = targetChunkCount;
            m_Head->maxElementCount = targetChunkCount * m_Head->elementCountInChunk;
        }

        /// <summary>
        /// Get element by id.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        private byte* GetDataPtr(int index)
        {
            long pos = index * m_Head->elementSize;
            int inChunk = (int)(pos / m_Head->fixedChunkSize);
            long posInChunk = pos % m_Head->fixedChunkSize;
            return *(m_Head->chunks + inChunk) + posInChunk;
        }

        private void GetDataPos(int index, out int inChunk, out long posInChunk)
        {
            long pos = index * m_Head->elementSize;
            inChunk = (int)(pos / m_Head->fixedChunkSize);
            posInChunk = pos % m_Head->fixedChunkSize;
        }

        private void Lock()
        {
            while (1 == Interlocked.Exchange(ref m_Head->lockedMark, 1)) ;
        }

        private void Unlock()
        {
            Interlocked.Exchange(ref m_Head->lockedMark, 0);
        }

        #endregion

        #region Check

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckExists()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (m_Head == null || m_Head->existsMark != ExistsMark)
            {
                throw new NullReferenceException($"{nameof(UnsafeChunkedQueue)} is yet created or already disposed.");
            }
#endif
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckIndex(int index)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (index < 0 || index >= m_Head->elementCount)
            {
                Unlock();
                throw new IndexOutOfRangeException($"{nameof(UnsafeChunkedQueue)} index must >= 0 && < Count.");
            }
#endif
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckIndexNoLock(int index)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (index < 0 || index >= m_Head->elementCount)
            {
                throw new IndexOutOfRangeException($"{nameof(UnsafeChunkedQueue)} index must >= 0 && < Count.");
            }
#endif
        }

        #endregion
    }
}