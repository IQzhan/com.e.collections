using System;
using System.Diagnostics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace E.Collections.Unsafe
{
    public unsafe struct UnsafeChunkedQueue : ICollection, IPtrIndexable, IChunked, IResizeable, ILockable, IDisposable, IEquatable<UnsafeChunkedQueue>
    {
        #region Main

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
        public byte* this[int index] => Get(index);

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
        public byte* Get(int index)
        {
            CheckExists();
            CheckIndex(index);
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

        public Lock GetLock()
        {
            CheckExists();
            return new Lock(&m_Head->lockedMark);
        }

        public override bool Equals(object obj) => obj is UnsafeChunkedQueue queue && m_Head == queue.m_Head;

        public bool Equals(UnsafeChunkedQueue other) => m_Head == other.m_Head;

        public override int GetHashCode() => m_Head != null ? (int)m_Head : 0;

        public static bool operator ==(UnsafeChunkedQueue left, UnsafeChunkedQueue right) => left.m_Head == right.m_Head;

        public static bool operator !=(UnsafeChunkedQueue left, UnsafeChunkedQueue right) => left.m_Head != right.m_Head;

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
                Memory.Copy(tempChunks, chunks, oldSize);
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
                    Memory.Copy(firstNewChunk, endAtChunk, endPosInChunk + m_Head->elementSize);
                    if (endInChunk > 0)
                    {
                        //move chunks [0, endInChunk) to m_Head->chunkCount
                        int beforeCount = endInChunk;
                        int midCount = m_Head->chunkCount - endInChunk;
                        long beforeSize = Memory.PtrSize * beforeCount;
                        long midSize = Memory.PtrSize * midCount;
                        byte** tempChunks = (byte**)Memory.Malloc(beforeSize, 1, Allocator.Temp);
                        Memory.Copy(tempChunks, m_Head->chunks, beforeSize);
                        Memory.Move(m_Head->chunks, m_Head->chunks + beforeCount, midSize);
                        Memory.Copy(m_Head->chunks + midCount, tempChunks, beforeSize);
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
                throw new IndexOutOfRangeException($"{nameof(UnsafeChunkedQueue)} index must >= 0 && < Count.");
            }
#endif
        }

        #endregion
    }
}