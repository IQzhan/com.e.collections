using System;
using System.Diagnostics;
using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace E.Collections.Unsafe
{
    public unsafe struct UnsafeChunkedList : ICollection, IChunked, IResizeable
    {
        private struct Head
        {
            public Allocator allocator;
            public int elementSize;
            public int elementCount;
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

        public UnsafeChunkedList(int elementSize, long chunkSize, Allocator allocator)
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
                chunkSize = chunkSize,
                fixedChunkSize = chunkSize - (chunkSize % elementSize),
                maxElementCount = 0,
                chunkCount = 0,
                elementCountInChunk = (int)(chunkSize / elementSize),
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
        /// Get no thread safe.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public byte* this[int index]
        {
            get
            {
                CheckExists();
                CheckIndexNoLock(index);
                byte* ptr = GetDataPtr(index);
                return ptr;
            }
        }

        /// <summary>
        /// Add thread safe.
        /// </summary>
        /// <returns></returns>
        public byte* Add()
        {
            CheckExists();
            Lock();
            InternalExtend(1);
            byte* ptr = GetDataPtr(m_Head->elementCount++);
            Unlock();
            return ptr;
        }

        /// <summary>
        /// Insert thread safe.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public byte* Insert(int index)
        {
            CheckExists();
            Lock();
            CheckIndexInsert(index);
            InternalExtend(1);
            for (int i = m_Head->elementCount; i > index; i--)
            {
                UnsafeUtility.MemCpy(GetDataPtr(i), GetDataPtr(i - 1), m_Head->elementSize);
            }
            m_Head->elementCount++;
            byte* ptr = GetDataPtr(index);
            Unlock();
            return ptr;
        }

        /// <summary>
        /// get thread safe.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public byte* Get(int index)
        {
            CheckExists();
            Lock();
            CheckIndex(index);
            byte* ptr = GetDataPtr(index);
            Unlock();
            return ptr;
        }

        /// <summary>
        /// Remove thread safe.
        /// </summary>
        /// <param name="index"></param>
        public void Remove(int index)
        {
            CheckExists();
            Lock();
            CheckIndex(index);
            int count = m_Head->elementCount - 1;
            for (int i = index; i < count; i++)
            {
                UnsafeUtility.MemCpy(GetDataPtr(i), GetDataPtr(i + 1), m_Head->elementSize);
            }
            m_Head->elementCount = count;
            Unlock();
        }

        /// <summary>
        /// Remove Last thread safe.
        /// </summary>
        public void RemoveLast()
        {
            CheckExists();
            Lock();
            int index = m_Head->elementCount - 1;
            CheckIndex(index);
            m_Head->elementCount = index;
            Unlock();
        }

        /// <summary>
        /// Swap last and this and remove this thread safe.
        /// </summary>
        /// <param name="index"></param>
        public void SwapLastAndRemove(int index)
        {
            CheckExists();
            Lock();
            CheckIndex(index);
            int lastIndex = m_Head->elementCount - 1;
            if (index != lastIndex)
            {
                //move last to this
                UnsafeUtility.MemCpy(GetDataPtr(index), GetDataPtr(lastIndex), m_Head->elementSize);
            }
            m_Head->elementCount = lastIndex;
            Unlock();
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

        internal void InternalExtend(int count)
        {
            int targetCount = m_Head->elementCount + count;
            if (targetCount <= m_Head->maxElementCount) return;
            int targetChunkCount = (int)Math.Ceiling((double)targetCount / m_Head->elementCountInChunk);
            int maxChunkCount = m_Head->maxChunkCount;
            Allocator allocator = m_Head->allocator;
            if (targetChunkCount > maxChunkCount)
            {
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
            for (int i = m_Head->chunkCount; i < targetChunkCount; i++)
            {
                *(m_Head->chunks + i) = (byte*)Memory.Malloc<byte>(m_Head->chunkSize, allocator);
            }
            m_Head->chunkCount = targetChunkCount;
            m_Head->maxElementCount = targetChunkCount * m_Head->elementCountInChunk;
        }

        /// <summary>
        /// Clear safe.
        /// </summary>
        public void Clear()
        {
            CheckExists();
            Lock();
            m_Head->elementCount = 0;
            Unlock();
        }

        /// <summary>
        /// Dispose.
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
        /// Get element's pointer by index.
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
                throw new NullReferenceException($"{nameof(UnsafeChunkedList)} is yet created or already disposed.");
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
                throw new IndexOutOfRangeException($"{nameof(UnsafeChunkedList)} index must >= 0 && < Count.");
            }
#endif
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckIndexNoLock(int index)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (index < 0 || index >= m_Head->elementCount)
            {
                throw new IndexOutOfRangeException($"{nameof(UnsafeChunkedList)} index must >= 0 && < Count.");
            }
#endif
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckIndexInsert(int index)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (index < 0 || index > m_Head->elementCount)
            {
                Unlock();
                throw new IndexOutOfRangeException($"{nameof(UnsafeChunkedList)} index must >= 0 && < Count.");
            }
#endif
        }
    }
}