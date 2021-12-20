using System;
using System.Diagnostics;
using System.Threading;
using Unity.Collections;

namespace E.Collections.Unsafe
{
    public unsafe struct UnsafeSortedChunkedList<Key> : ICollection, IChunked, IResizeable
        where Key : unmanaged, IComparable<Key>, IComparable
    {
        private struct Head
        {
            public Allocator allocator;
            public UnsafeChunkedList list;
            public int keySize;
            public int lockedMark;
        }

        private Head* m_Head;

        public bool IsCreated => m_Head != null;

        public int Count
        {
            get
            {
                CheckExists();
                return m_Head->list.Count;
            }
        }

        public long ChunkSize
        {
            get
            {
                CheckExists();
                return m_Head->list.ChunkSize;
            }
        }

        public int ChunkCount
        {
            get
            {
                CheckExists();
                return m_Head->list.ChunkCount;
            }
        }

        public int ElementSize
        {
            get
            {
                CheckExists();
                return m_Head->list.ElementSize;
            }
        }

        public UnsafeSortedChunkedList(int elementSize, long chunkSize, Allocator allocator)
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
            int keySize = Memory.SizeOf<Key>();
            int pairSize = keySize + elementSize;
            *m_Head = new Head()
            {
                allocator = allocator,
                list = new UnsafeChunkedList(pairSize, chunkSize, allocator),
                keySize = keySize,
                lockedMark = 0
            };
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
                return m_Head->list[index];
            }
        }

        /// <summary>
        /// Add thread safe.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public byte* Add(Key key)
        {
            CheckExists();
            Lock();
            byte* h, v;
            if (!BinarySearch(key, out int i))
            {
                h = m_Head->list.Insert(i);
                *(Key*)h = key;
            }
            else
            {
                h = m_Head->list.Get(i);
            }
            v = h + m_Head->keySize;
            Unlock();
            return v;
        }

        /// <summary>
        /// Get thread safe.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="ptr"></param>
        /// <returns></returns>
        public bool TryGetValue(Key key, out byte* ptr)
        {
            CheckExists();
            Lock();
            if (BinarySearch(key, out int i))
            {
                ptr = m_Head->list[i] + m_Head->keySize;
                Unlock();
                return true;
            }
            ptr = default;
            Unlock();
            return false;
        }

        /// <summary>
        /// Remove thread safe.
        /// </summary>
        /// <param name="key"></param>
        public void Remove(Key key)
        {
            CheckExists();
            Lock();
            if (BinarySearch(key, out int i))
            {
                m_Head->list.Remove(i);
            }
            Unlock();
        }

        /// <summary>
        /// Check key exists.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool Contains(Key key)
        {
            CheckExists();
            return BinarySearch(key, out int _);
        }

        /// <summary>
        /// Get index if key exists.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public int IndexOf(Key key)
        {
            CheckExists();
            if (BinarySearch(key, out int index))
            {
                return index;
            }
            return -1;
        }

        /// <summary>
        /// Search key.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="index"></param>
        /// <returns>true founded. false not.</returns>
        private bool BinarySearch(Key key, out int index)
        {
            int low = 0;
            int high = Count - 1;
            while (low <= high)
            {
                int mid = (low + high) / 2;
                Key midKey = *(Key*)m_Head->list[mid];
                int compare = midKey.CompareTo(key);
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

        public void Clear()
        {
            CheckExists();
            m_Head->list.Clear();
        }

        public void Dispose()
        {
            CheckExists();
            m_Head->list.Dispose();
            Memory.Free(m_Head, m_Head->allocator);
            m_Head = null;
        }

        public void Extend(int count)
        {
            CheckExists();
            m_Head->list.Extend(count);
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
    }
}