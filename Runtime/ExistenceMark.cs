using System;
using System.Threading;

namespace E.Collections.Unsafe
{
    internal unsafe struct ExistenceMark : IEquatable<ExistenceMark>
    {
        public static readonly ExistenceMark Null = default;

        private static readonly int m_LatestMark = 1;

        private static readonly int* m_LatestMarkPtr;

        private readonly int m_Mark;

        static ExistenceMark()
        {
            fixed(int* ptr = &m_LatestMark)
            {
                m_LatestMarkPtr = ptr;
            }
        }

        private ExistenceMark(int mark) => m_Mark = mark;

        public static ExistenceMark Create()
            => new ExistenceMark(Interlocked.Increment(ref *m_LatestMarkPtr));

        public override bool Equals(object obj)
            => obj is ExistenceMark mark && m_Mark == mark.m_Mark;

        public bool Equals(ExistenceMark other)
            => m_Mark == other.m_Mark;

        public override int GetHashCode()
            => m_Mark;

        public static bool operator ==(ExistenceMark left, ExistenceMark right)
            => left.m_Mark == right.m_Mark;

        public static bool operator !=(ExistenceMark left, ExistenceMark right)
            => left.m_Mark != right.m_Mark;
    }
}