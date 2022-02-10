using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace E.Collections.Unsafe
{
    /// <summary>
    /// Hash + Red-Black-Tree, faster then UnsafeChunkedSet
    /// </summary>
    /// <typeparam name="Key"></typeparam>
    public unsafe struct UnsafeChunkedHashSet<Key> :
        ICollection<UnsafeSetNode<Key>>,
        IChunked,
        IResizeable,
        ILockable,
        IDisposable,
        IEquatable<UnsafeChunkedHashSet<Key>>
        where Key : unmanaged, IComparable<Key>
    {
        #region Main

        private struct Head
        {
            public ExistenceMark existenceMark;

            public Allocator allocator;

            public int lockedMark;

            /// <summary>
            /// Internal node struct's size.
            /// </summary>
            public int nodeStructSize;

            /// <summary>
            /// Length of hash map.
            /// </summary>
            public uint mapLength;

            /// <summary>
            /// Tree root.
            /// </summary>
            public RBTree<Key>* map;

            /// <summary>
            /// Chunks.
            /// </summary>
            public UnsafeChunkedList data;

            /// <summary>
            /// Internal Red-Black-Tree functions.
            /// </summary>
            public RBTreeStruct<Key, Functions> functions;
        }

        internal struct Functions : RBTreeStruct<Key, Functions>.IFunctions
        {
            public Functions(uint mapLength, RBTree<Key>* map)
            {
                m_MapLength = mapLength;
                m_Map = map;
            }

            private readonly RBTree<Key>* m_Map;

            private readonly uint m_MapLength;

            public RBTree<Key>* GetTree(Key key)
                => m_Map + ((uint)key.GetHashCode() % m_MapLength);
        }

        [NativeDisableUnsafePtrRestriction]
        private Head* m_Head;

        private ExistenceMark m_ExistenceMark;

        public bool IsCreated => m_Head != null && m_Head->existenceMark == m_ExistenceMark;

        public int Count => IsCreated ? m_Head->data.Count : 0;

        public long ChunkSize => IsCreated ? m_Head->data.ChunkSize : 0;

        public int ChunkCount => IsCreated ? m_Head->data.ChunkCount : 0;

        public int ElementSize => IsCreated ? m_Head->data.ElementSize : 0;

        public UnsafeChunkedHashSet(int hashMapLength, int valueSize, long chunkSize, Allocator allocator)
        {
            m_Head = default;
            m_ExistenceMark = ExistenceMark.Null;
            CheckArguments(hashMapLength, valueSize, chunkSize, allocator);
            InitializeHead(hashMapLength, valueSize, chunkSize, allocator);
        }

        public UnsafeSetNode<Key> this[int index] => GetByIndex(index);

        public bool Contains(Key key)
        {
            CheckExists();
            var tree = GetTree(key);
            return m_Head->functions.TryGetNode(tree, key, out var _);
        }

        public bool TryGetByKey(Key key, out UnsafeSetNode<Key> node)
        {
            CheckExists();
            var tree = GetTree(key);
            if (m_Head->functions.TryGetNode(tree, key, out var val))
            {
                node = new UnsafeSetNode<Key>(m_Head->nodeStructSize, val);
                return true;
            }
            node = default;
            return false;
        }

        public UnsafeSetNode<Key> GetByIndex(int index)
        {
            CheckExists();
            CheckIndex(index);
            var node = (RBTNode<Key>*)m_Head->data[index].Value;
            return new UnsafeSetNode<Key>(m_Head->nodeStructSize, node);
        }

        public UnsafeSetNode<Key> Set(Key key)
        {
            CheckExists();
            var tree = GetTree(key);
            var node = m_Head->functions.Set(tree, key);
            return new UnsafeSetNode<Key>(m_Head->nodeStructSize, node);
        }

        public bool Remove(Key key)
        {
            CheckExists();
            var tree = GetTree(key);
            return m_Head->functions.Remove(tree, key);
        }

        public void Extend(int count)
        {
            CheckExists();
            m_Head->data.Extend(count);
        }

        public void Clear()
        {
            CheckExists();
            ClearMap();
            m_Head->data.Clear();
        }

        /// <summary>
        /// Reset element size while element count is zero.
        /// </summary>
        /// <param name="size"></param>
        public void ResetElementSize(int size)
        {
            CheckExists();
            CheckResetElementSize(size);
            m_Head->data.ResetElementSize(m_Head->nodeStructSize + size);
        }

        public void Dispose()
        {
            CheckExists();
            m_Head->existenceMark = ExistenceMark.Null;
            m_Head->data.Dispose();
            Memory.Free(m_Head, m_Head->allocator);
            m_Head = null;
        }

        public SpinLock GetLock()
        {
            CheckExists();
            return new SpinLock(&m_Head->lockedMark);
        }

        public override bool Equals(object obj) => obj is UnsafeChunkedHashSet<Key> set && m_Head == set.m_Head;

        public bool Equals(UnsafeChunkedHashSet<Key> other) => m_Head == other.m_Head;

        public override int GetHashCode() => m_Head != null ? (int)m_Head : 0;

        public static bool operator ==(UnsafeChunkedHashSet<Key> left, UnsafeChunkedHashSet<Key> right) => left.m_Head == right.m_Head;

        public static bool operator !=(UnsafeChunkedHashSet<Key> left, UnsafeChunkedHashSet<Key> right) => left.m_Head != right.m_Head;

        #endregion

        #region IEnumerator

        public IEnumerator<UnsafeSetNode<Key>> GetEnumerator() => new Enumerator(this);

        IEnumerator IEnumerable.GetEnumerator() => new Enumerator(this);

        public struct Enumerator : IEnumerator<UnsafeSetNode<Key>>, IEnumerator, IDisposable
        {
            private readonly UnsafeChunkedHashSet<Key> m_Instance;

            private int index;

            public Enumerator(UnsafeChunkedHashSet<Key> instance)
            {
                m_Instance = instance;
                index = -1;
            }

            object IEnumerator.Current => Current;

            public UnsafeSetNode<Key> Current => m_Instance[index];

            public bool MoveNext()
            {
                if (index + 1 < m_Instance.Count)
                {
                    ++index;
                    return true;
                }
                return false;
            }

            public void Reset()
            {
                index = -1;
            }

            public void Dispose()
            {

            }
        }

        #endregion

        #region Internal

        private void InitializeHead(int hashMapLength, int valueSize, long chunkSize, Allocator allocator)
        {
            int nodeStructSize = Memory.SizeOf<RBTNode<Key>>();
            int nodeSize = nodeStructSize + valueSize;
            int headSize = Memory.SizeOf<Head>();
            int mapSize = Memory.SizeOf<RBTree<Key>>() * hashMapLength;
            m_Head = (Head*)Memory.Malloc(headSize + mapSize, 1, allocator);
            *m_Head = new Head()
            {
                existenceMark = m_ExistenceMark = ExistenceMark.Create(),
                allocator = allocator,
                lockedMark = 0,
                nodeStructSize = nodeStructSize,
                mapLength = (uint)hashMapLength,
                map = (RBTree<Key>*)((byte*)m_Head + headSize),
                data = new UnsafeChunkedList(nodeSize, chunkSize, allocator)
            };
            InitializeFunctions((uint)hashMapLength);
            ClearMap();
        }

        private RBTree<Key>* GetTree(Key key)
        {
            return m_Head->map + ((uint)key.GetHashCode() % m_Head->mapLength);
        }

        private void ClearMap()
        {
            Memory.Clear<RBTree<Key>>(m_Head->map, m_Head->mapLength);
        }

        private void InitializeFunctions(uint hashMapLength)
        {
            m_Head->functions = new RBTreeStruct<Key, Functions>(
                new Functions(hashMapLength, m_Head->map), m_Head->data);
        }

        #endregion

        #region Check

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckArguments(int hashMapLength, int valueSize, long chunkSize, Allocator allocator)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (hashMapLength <= 0)
            {
                throw new ArgumentException("hashMapLength must bigger then 0.");
            }
            if (valueSize <= 0)
            {
                throw new ArgumentException("elementSize must bigger then 0.");
            }
            if (chunkSize <= 0)
            {
                throw new ArgumentException("chunkSize must bigger then 0.");
            }
            if (valueSize > chunkSize)
            {
                throw new ArgumentException("chunkSize must bigger then elementSize.");
            }
#endif
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckExists()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (m_Head == null || m_Head->existenceMark != m_ExistenceMark)
            {
                throw new NullReferenceException($"{nameof(UnsafeChunkedSet<Key>)} is yet created or already disposed.");
            }
#endif
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckIndex(int index)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (index < 0 || index >= m_Head->data.Count)
            {
                throw new IndexOutOfRangeException($"{nameof(UnsafeChunkedSet<Key>)} index must >= 0 && < Count.");
            }
#endif
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        internal void Check(Key key)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var tree = GetTree(key);
            m_Head->functions.Check(tree);
#endif
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckResetElementSize(int size)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (m_Head->data.Count > 0)
            {
                throw new Exception("Can not reset element size while element count is not zero.");
            }
            if (size <= 0)
            {
                throw new ArgumentException("must bigger then 0.", "size");
            }
#endif
        }

        #endregion
    }
}