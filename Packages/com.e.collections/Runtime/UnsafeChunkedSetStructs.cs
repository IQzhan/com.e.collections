using System;

namespace E.Collections.Unsafe
{
    public unsafe struct UnsafeSetNode<TKey> :
        IEquatable<UnsafeSetNode<TKey>>
        where TKey : unmanaged, IComparable<TKey>
    {
        public static readonly UnsafeSetNode<TKey> Null = default;

        /// <summary>
        /// Key of this node.
        /// </summary>
        public TKey Key => m_Node->m_Key;

        /// <summary>
        /// Index in chunks of this node.
        /// </summary>
        public int Index => m_Node->Index;

        /// <summary>
        /// Is this node marks as dirty?
        /// </summary>
        public bool IsDirty { get => m_Node->IsDirty; set => m_Node->IsDirty = value; }

        /// <summary>
        /// Value's ptr of this node.
        /// </summary>
        public byte* Value => (byte*)m_Node + m_NodeStructSize;

        /// <summary>
        /// Internal node struct's size.
        /// </summary>
        internal readonly int m_NodeStructSize;

        /// <summary>
        /// Internal node.
        /// </summary>
        internal readonly RBTNode<TKey>* m_Node;

        internal UnsafeSetNode(int nodeStructSize, RBTNode<TKey>* node)
        {
            m_NodeStructSize = nodeStructSize;
            m_Node = node;
        }

        public override bool Equals(object obj)
            => obj is UnsafeSetNode<TKey> node && Equals(node);

        public bool Equals(UnsafeSetNode<TKey> other)
            => (m_NodeStructSize == other.m_NodeStructSize) && (m_Node == other.m_Node);

        public override int GetHashCode()
            => HashCode.Combine(m_NodeStructSize, (long)m_Node);

        public static bool operator ==(UnsafeSetNode<TKey> left, UnsafeSetNode<TKey> right)
            => left.Equals(right);

        public static bool operator !=(UnsafeSetNode<TKey> left, UnsafeSetNode<TKey> right)
            => !left.Equals(right);
    }
}