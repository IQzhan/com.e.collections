using System;

namespace E.Collections.Unsafe
{
    public unsafe struct UnsafeNode :
        IEquatable<UnsafeNode>
    {
        public static readonly UnsafeNode Null = default;

        public int Index => m_Index;

        public byte* Value => m_Value;

        private readonly int m_Index;

        private readonly byte* m_Value;

        internal UnsafeNode(int index, byte* value)
        {
            m_Index = index;
            m_Value = value;
        }

        public override bool Equals(object obj)
            => obj is UnsafeNode node && Equals(node);

        public bool Equals(UnsafeNode other)
            => (m_Index == other.m_Index) && (m_Value == other.m_Value);

        public override int GetHashCode()
            => HashCode.Combine(m_Index, (long)m_Value);

        public static bool operator ==(UnsafeNode left, UnsafeNode right)
            => left.Equals(right);

        public static bool operator !=(UnsafeNode left, UnsafeNode right)
            => !left.Equals(right);
    }
}