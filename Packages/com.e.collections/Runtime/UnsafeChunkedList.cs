using System;
using System.Diagnostics;
using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace E.Collections.Unsafe
{
    public unsafe struct UnsafeChunkedList : ICollection, IChunked, IResizeable, IDisposable, IEquatable<UnsafeChunkedList>
    {
        #region No ThreadSafe

        private struct Head
        {
            public int existsMark;
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

        [NativeDisableUnsafePtrRestriction]
        private Head* m_Head;

        private const int ExistsMark = 1000001;

        private const int ExpendCount = 32;

        public bool IsCreated => m_Head != null && m_Head->existsMark == ExistsMark;

        public int Count => IsCreated ? m_Head->elementCount : 0;

        public long ChunkSize => IsCreated ? m_Head->chunkSize : 0;

        public int ChunkCount => IsCreated ? m_Head->chunkCount : 0;

        public int ElementSize => IsCreated ? m_Head->elementSize : 0;

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
                existsMark = ExistsMark,
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

        public byte* this[int index] => Get(index);

        public byte* Add()
        {
            CheckExists();
            return InternalAdd();
        }

        public byte* Insert(int index)
        {
            CheckExists();
            CheckIndexInsertNoLock(index);
            return InternalInsert(index);
        }

        public byte* Get(int index)
        {
            CheckExists();
            CheckIndexNoLock(index);
            return GetDataPtr(index);
        }

        public void Remove(int index)
        {
            CheckExists();
            CheckIndexNoLock(index);
            InternalRemove(index);
        }

        public void RemoveLast()
        {
            CheckExists();
            int index = m_Head->elementCount - 1;
            CheckIndexNoLock(index);
            m_Head->elementCount = index;
        }

        /// <summary>
        /// Swap last and this then remove this.
        /// </summary>
        /// <param name="index"></param>
        public void SwapLastAndRemove(int index)
        {
            CheckExists();
            CheckIndexNoLock(index);
            InternalSwapLastAndRemove(index);
        }

        public void Extend(int count)
        {
            CheckExists();
            InternalExtend(count);
        }

        public void Clear()
        {
            CheckExists();
            m_Head->elementCount = 0;
        }

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

        public override bool Equals(object obj) => obj is UnsafeChunkedList list && m_Head == list.m_Head;

        public bool Equals(UnsafeChunkedList other) => m_Head == other.m_Head;

        public override int GetHashCode() => m_Head != null ? (int)m_Head : 0;

        public static bool operator ==(UnsafeChunkedList left, UnsafeChunkedList right) => left.m_Head == right.m_Head;

        public static bool operator !=(UnsafeChunkedList left, UnsafeChunkedList right) => left.m_Head != right.m_Head;

        #endregion

        #region ThreadSafe

        public ThreadSafe AsThreadSafe() => new ThreadSafe(this);

        public struct ThreadSafe : ICollection, IChunked, IResizeable, IThreadSafe, IEquatable<ThreadSafe>
        {
            public UnsafeChunkedList AsNoThreadSafe() => m_Instance;

            public ThreadSafe(UnsafeChunkedList instance) => m_Instance = instance;

            private readonly UnsafeChunkedList m_Instance;

            public bool IsCreated => m_Instance.IsCreated;

            public int Count => m_Instance.LockedCount();

            public long ChunkSize => m_Instance.ChunkSize;

            public int ChunkCount => m_Instance.LockedChunkCount();

            public int ElementSize => m_Instance.ElementSize;

            public byte* this[int index] => m_Instance.LockedGet(index);

            public byte* Add() => m_Instance.LockedAdd();

            public byte* Insert(int index) => m_Instance.LockedInsert(index);

            public byte* Get(int index) => m_Instance.LockedGet(index);

            public void Remove(int index) => m_Instance.LockedRemove(index);

            public void RemoveLast() => m_Instance.LockedRemoveLast();

            /// <summary>
            /// Swap last and this then remove this.
            /// </summary>
            /// <param name="index"></param>
            public void SwapLastAndRemove(int index) => m_Instance.LockedSwapLastAndRemove(index);

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

        private byte* LockedAdd()
        {
            CheckExists();
            Lock();
            byte* result = InternalAdd();
            Unlock();
            return result;
        }

        private byte* LockedInsert(int index)
        {
            CheckExists();
            Lock();
            CheckIndexInsert(index);
            byte* result = InternalInsert(index);
            Unlock();
            return result;
        }

        private byte* LockedGet(int index)
        {
            CheckExists();
            Lock();
            CheckIndex(index);
            byte* result = GetDataPtr(index);
            Unlock();
            return result;
        }

        private void LockedRemove(int index)
        {
            CheckExists();
            Lock();
            CheckIndex(index);
            InternalRemove(index);
            Unlock();
        }

        private void LockedRemoveLast()
        {
            CheckExists();
            Lock();
            int index = m_Head->elementCount - 1;
            CheckIndex(index);
            m_Head->elementCount = index;
            Unlock();
        }

        private void LockedSwapLastAndRemove(int index)
        {
            CheckExists();
            Lock();
            CheckIndex(index);
            InternalSwapLastAndRemove(index);
            Unlock();
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
            Unlock();
        }

        #endregion

        #region Internal

        private byte* InternalAdd()
        {
            InternalExtend(1);
            return GetDataPtr(m_Head->elementCount++);
        }

        private byte* InternalInsert(int index)
        {
            InternalExtend(1);
            for (int i = m_Head->elementCount; i > index; i--)
            {
                UnsafeUtility.MemCpy(GetDataPtr(i), GetDataPtr(i - 1), m_Head->elementSize);
            }
            m_Head->elementCount++;
            return GetDataPtr(index);
        }

        private void InternalRemove(int index)
        {
            int count = m_Head->elementCount - 1;
            for (int i = index; i < count; i++)
            {
                UnsafeUtility.MemCpy(GetDataPtr(i), GetDataPtr(i + 1), m_Head->elementSize);
            }
            m_Head->elementCount = count;
        }

        private void InternalSwapLastAndRemove(int index)
        {
            int lastIndex = m_Head->elementCount - 1;
            if (index != lastIndex)
            {
                //move last to this
                UnsafeUtility.MemCpy(GetDataPtr(index), GetDataPtr(lastIndex), m_Head->elementSize);
            }
            m_Head->elementCount = lastIndex;
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

        private bool BinarySearch<Compare>(byte* value, out int index)
            where Compare : struct, IPtrCompareCallback
        {
            Compare ptrCompare = new Compare();
            int low = 0;
            int high = m_Head->elementCount - 1;
            while (low <= high)
            {
                int mid = (low + high) / 2;
                byte* midValue = GetDataPtr(mid);
                int compare = ptrCompare.Compare(midValue, value);
                if (compare < 0)
                {
                    // in right
                    low = mid + 1;
                }
                else if (compare > 0)
                {
                    // in left
                    high = mid - 1;
                }
                else
                {
                    // match
                    index = mid;
                    return true;
                }
            }
            // the last search index
            index = low;
            return false;
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

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckIndexInsertNoLock(int index)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (index < 0 || index > m_Head->elementCount)
            {
                throw new IndexOutOfRangeException($"{nameof(UnsafeChunkedList)} index must >= 0 && < Count.");
            }
#endif
        }

        #endregion
    }
}