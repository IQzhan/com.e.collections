using System;
using System.Diagnostics;
using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace E.Collections.Unsafe
{
    public unsafe struct UnsafeChunkedQueue : ICollection, IChunked, IResizeable
    {
        private struct Head
        {
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

        private Head* m_Head;

        private const int ExpendCount = 32;

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

        public bool IsCreated => m_Head != null;

        public int Count
        {
            get
            {
                CheckExists();
                return m_Head->elementCount;
            }
        }

        public long ChunkSize
        {
            get
            {
                CheckExists();
                return m_Head->chunkSize;
            }
        }

        public int ChunkCount
        {
            get
            {
                CheckExists();
                return m_Head->chunkCount;
            }
        }

        public int ElementSize
        {
            get
            {
                CheckExists();
                return m_Head->elementSize;
            }
        }

        /// <summary>
        /// Get but not remove no thread safe.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public byte* this[int index]
        {
            get
            {
                CheckExists();
                CheckIndexNoLock(index);
                if (m_Head->elementCount < 1) return default;
                byte* ptr = GetDataPtr((m_Head->elementStart + index) % m_Head->maxElementCount);
                return ptr;
            }
        }

        /// <summary>
        /// Enqueue thread safe.
        /// </summary>
        /// <returns></returns>
        public byte* Enqueue()
        {
            CheckExists();
            Lock();
            InternalExtend(1);
            byte* ptr = GetDataPtr((m_Head->elementStart + m_Head->elementCount++) % m_Head->maxElementCount);
            Unlock();
            return ptr;
        }

        /// <summary>
        /// Dequeue thread safe.
        /// </summary>
        /// <returns></returns>
        public byte* Dequeue()
        {
            CheckExists();
            Lock();
            if (m_Head->elementCount < 1) return default;
            byte* ptr = GetDataPtr(m_Head->elementStart++);
            m_Head->elementStart %= m_Head->maxElementCount;
            --m_Head->elementCount;
            Unlock();
            return ptr;
        }

        /// <summary>
        /// Get but not reamove thread safe.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public byte* Peek(int index)
        {
            CheckExists();
            Lock();
            CheckIndex(index);
            if (m_Head->elementCount < 1) return default;
            byte* ptr = GetDataPtr((m_Head->elementStart + index) % m_Head->maxElementCount);
            Unlock();
            return ptr;
        }

        /// <summary>
        /// Extend thread safe.
        /// </summary>
        /// <param name="count"></param>
        public void Extend(int count)
        {
            Lock();
            InternalExtend(count);
            Unlock();
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
        /// Clear thread safe.
        /// </summary>
        public void Clear()
        {
            CheckExists();
            Lock();
            m_Head->elementCount = 0;
            m_Head->elementStart = 0;
            Unlock();
        }

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
            CheckExists();
            for (int i = 0; i < m_Head->chunkCount; i++)
            {
                Memory.Free(*(m_Head->chunks + i), m_Head->allocator);
            }
            Memory.Free(m_Head->chunks, m_Head->allocator);
            Memory.Free(m_Head, m_Head->allocator);
            m_Head = null;
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

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckExists()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (m_Head == null)
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
    }
}