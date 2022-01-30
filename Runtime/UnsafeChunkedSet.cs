using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace E.Collections.Unsafe
{
    /// <summary>
    /// A set with Red-Black-Tree.
    /// </summary>
    /// <typeparam name="Key"></typeparam>
    public unsafe struct UnsafeChunkedSet<Key> :
        ICollection<UnsafeSetNode<Key>>,
        IChunked,
        IResizeable,
        ILockable,
        IDisposable,
        IEquatable<UnsafeChunkedSet<Key>>
        where Key : unmanaged, IComparable<Key>
    {
        #region Main

        private struct Head
        {
            public int existsMark;

            public Allocator allocator;

            public int lockedMark;

            /// <summary>
            /// Internal node struct's size.
            /// </summary>
            public int nodeStructSize;

            /// <summary>
            /// Tree root.
            /// </summary>
            public RBTree<Key> tree;

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
            public Functions(RBTree<Key>* tree) => m_Tree = tree;

            public readonly RBTree<Key>* m_Tree;

            public RBTree<Key>* GetTree(Key key) => m_Tree;
        }

        [NativeDisableUnsafePtrRestriction]
        private Head* m_Head;

        private const int ExistsMark = 1000003;

        public bool IsCreated => m_Head != null && m_Head->existsMark == ExistsMark;

        public int Count => IsCreated ? m_Head->data.Count : 0;

        public long ChunkSize => IsCreated ? m_Head->data.ChunkSize : 0;

        public int ChunkCount => IsCreated ? m_Head->data.ChunkCount : 0;

        public int ElementSize => IsCreated ? m_Head->data.ElementSize : 0;

        public const int MaxCount = 0x3FFFFFFF;

        /// <summary>
        /// Create a red-black tree.
        /// </summary>
        /// <param name="valueSize"></param>
        /// <param name="chunkSize"></param>
        /// <param name="allocator"></param>
        public UnsafeChunkedSet(int valueSize, long chunkSize, Allocator allocator)
        {
            m_Head = default;
            CheckArguments(valueSize, chunkSize, allocator);
            InitializeHead(valueSize, chunkSize, allocator);
        }

        public UnsafeSetNode<Key> this[int index] => GetByIndex(index);

        public bool Contains(Key key)
        {
            CheckExists();
            return m_Head->functions.TryGetNode(&m_Head->tree, key, out var _);
        }

        public bool TryGetByKey(Key key, out UnsafeSetNode<Key> node)
        {
            CheckExists();
            if (m_Head->functions.TryGetNode(&m_Head->tree, key, out var val))
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
            var node = m_Head->functions.Set(&m_Head->tree, key);
            return new UnsafeSetNode<Key>(m_Head->nodeStructSize, node);
        }

        public bool Remove(Key key)
        {
            CheckExists();
            return m_Head->functions.Remove(&m_Head->tree, key);
        }

        public void Extend(int count)
        {
            CheckExists();
            m_Head->data.Extend(count);
        }

        public void Clear()
        {
            CheckExists();
            m_Head->data.Clear();
            m_Head->tree.root = null;
        }

        public void Dispose()
        {
            CheckExists();
            m_Head->existsMark = 0;
            m_Head->data.Dispose();
            Memory.Free(m_Head, m_Head->allocator);
            m_Head = null;
        }

        public SpinLock GetLock()
        {
            CheckExists();
            return new SpinLock(&m_Head->lockedMark);
        }

        public override bool Equals(object obj) => obj is UnsafeChunkedSet<Key> set && m_Head == set.m_Head;

        public bool Equals(UnsafeChunkedSet<Key> other) => m_Head == other.m_Head;

        public override int GetHashCode() => m_Head != null ? (int)m_Head : 0;

        public static bool operator ==(UnsafeChunkedSet<Key> left, UnsafeChunkedSet<Key> right) => left.m_Head == right.m_Head;

        public static bool operator !=(UnsafeChunkedSet<Key> left, UnsafeChunkedSet<Key> right) => left.m_Head != right.m_Head;

        #endregion

        #region IEnumerator

        public IEnumerator<UnsafeSetNode<Key>> GetEnumerator() => new Enumerator(this);

        IEnumerator IEnumerable.GetEnumerator() => new Enumerator(this);

        public struct Enumerator : IEnumerator<UnsafeSetNode<Key>>, IEnumerator, IDisposable
        {
            private readonly UnsafeChunkedSet<Key> m_Instance;

            private int index;

            public Enumerator(UnsafeChunkedSet<Key> instance)
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

        private void InitializeHead(int valueSize, long chunkSize, Allocator allocator)
        {
            int nodeStructSize = Memory.SizeOf<RBTNode<Key>>();
            int nodeSize = nodeStructSize + valueSize;
            m_Head = (Head*)Memory.Malloc<Head>(1, allocator);
            *m_Head = new Head()
            {
                existsMark = ExistsMark,
                allocator = allocator,
                nodeStructSize = nodeStructSize,
                lockedMark = 0,
                tree = default,
                data = new UnsafeChunkedList(nodeSize, chunkSize, allocator)
            };
            IInitializeFunctions();
        }

        private void IInitializeFunctions()
        {
            m_Head->functions = new RBTreeStruct<Key, Functions>(
                new Functions(&m_Head->tree), m_Head->data);
        }

        #endregion

        #region Check

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckArguments(int valueSize, long chunkSize, Allocator allocator)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
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
            if (m_Head == null || m_Head->existsMark != ExistsMark)
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
        internal void Check()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_Head->functions.Check(&m_Head->tree);
#endif
        }

        #endregion
    }
}