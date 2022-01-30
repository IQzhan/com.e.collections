using System;
using System.Threading;

namespace E.Collections.Unsafe
{
    internal unsafe struct ExistenceMark : IEquatable<ExistenceMark>
    {
        public static readonly ExistenceMark Null = default;

        private static int m_Latest = 1;

        private readonly int m_Mark;

        private ExistenceMark(int mark) => m_Mark = mark;

        public static ExistenceMark Create()
            => new ExistenceMark(Interlocked.Increment(ref m_Latest));

        public override bool Equals(object obj)
            => obj is ExistenceMark mark && m_Mark == mark.m_Mark;

        public bool Equals(ExistenceMark other)
            => m_Mark == other.m_Mark;

        public override int GetHashCode()
            => HashCode.Combine(m_Mark);

        public static bool operator ==(ExistenceMark left, ExistenceMark right)
            => left.m_Mark == right.m_Mark;

        public static bool operator !=(ExistenceMark left, ExistenceMark right)
            => left.m_Mark != right.m_Mark;
    }
}