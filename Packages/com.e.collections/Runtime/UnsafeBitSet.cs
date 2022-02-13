using E.Collections.Unsafe;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace E.Collections
{
    /// <summary>
    /// Bit tree set.
    /// </summary>
    /// 1 means children not empty.
    /// Such as in 4 bits
    ///  ----------------------
    /// |1    |0    |0    |1   |
    /// |0010 |0000 |0000 |1010|
    ///  ----------------------
    ///  Now we use 64 bits
    ///  ------------------------------------------------
    /// |Rank |Bits                                      |
    /// |0    |long               (Max count to 1)       |
    /// |1    |long long long ... (Max count 64 << 1)    |
    /// |2    |long long long ... (Max count to 64 << 2) |
    /// |...  |...                                       |
    /// |n    |long long long ... (Max count to 64 << n) |
    ///  ------------------------------------------------
    public unsafe struct UnsafeBitSet :
        ICollection<bool>,
        IResizeable,
        ILockable,
        IDisposable,
        IEquatable<UnsafeBitSet>
    {
        #region Main

        internal struct Head
        {
            public ExistenceMark existenceMark;
            public Allocator allocator;
            public int lockedMark;
            public int rank;
            public long count;
            public long capacity;

            /// <summary>
            /// RankData
            /// RankData
            /// ...
            /// long
            /// long long long ...
            /// long long long ...
            /// </summary>
            public RankData* rankData;
        }

        internal struct RankData
        {
            public long longCount;
            public long* data;
        }

        [NativeDisableUnsafePtrRestriction]
        private Head* m_Head;

        private ExistenceMark m_ExistenceMark;

        public bool IsCreated => m_Head != null && m_Head->existenceMark == m_ExistenceMark;

        public int Count => IsCreated ? (int)m_Head->count : 0;

        public long LongCount => IsCreated ? m_Head->count : 0;

        public long Capacity => IsCreated ? m_Head->capacity : 0;

        public UnsafeBitSet(long expectedCapacity, Allocator allocator)
        {
            m_ExistenceMark = default;
            m_Head = default;
            CheckArguments(expectedCapacity, allocator);
            InitializeHead(expectedCapacity, allocator);
        }

        public bool this[long index] => Get(index);

        public bool IsNotEmpty()
        {
            CheckExists();
            return InternalIsNotEmpty();
        }

        public void Set(long index, bool value)
        {
            CheckExists();
            CheckIndex(index);
            InternalSet(index, value);
        }

        public bool Get(long index)
        {
            CheckExists();
            CheckIndex(index);
            return InternalGet(index);
        }

        public long GetFirstOneIndex()
        {
            CheckExists();
            return InternalGetFirstOneIndex();
        }

        public long GetFirstOneIndexThenSetZero()
        {
            CheckExists();
            var searchedIndex = InternalGetFirstOneIndex();
            if (searchedIndex != -1)
            {
                InternalSet(searchedIndex, false);
            }
            return searchedIndex;
        }

        public void Expand(int count)
        {
            CheckExists();
            InternalExpand(count);
        }

        public void Clear()
        {
            CheckExists();
            InternalClear();
        }

        public void Dispose()
        {
            CheckExists();
            DisposeHead();
        }

        public SpinLock GetLock()
        {
            CheckExists();
            return new SpinLock(ref m_Head->lockedMark);
        }

        public override bool Equals(object obj) => obj is UnsafeBitSet list && m_Head == list.m_Head;

        public bool Equals(UnsafeBitSet other) => m_Head == other.m_Head;

        public override int GetHashCode() => m_Head != null ? (int)m_Head : 0;

        public static bool operator ==(UnsafeBitSet left, UnsafeBitSet right) => left.m_Head == right.m_Head;

        public static bool operator !=(UnsafeBitSet left, UnsafeBitSet right) => left.m_Head != right.m_Head;

        #endregion

        #region IEnumerator

        public IEnumerator<bool> GetEnumerator() => new Enumerator(this);

        IEnumerator IEnumerable.GetEnumerator() => new Enumerator(this);

        public struct Enumerator : IEnumerator<bool>, IEnumerator, IDisposable
        {
            private readonly UnsafeBitSet m_Instance;

            private long index;

            public Enumerator(UnsafeBitSet instance)
            {
                m_Instance = instance;
                index = -1;
            }

            object IEnumerator.Current => Current;

            public bool Current => m_Instance.InternalGet(index);

            public bool MoveNext()
            {
                if (index + 1 < m_Instance.LongCount)
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

        private void InitializeHead(long expectedCapacity, Allocator allocator)
        {
            m_Head = (Head*)Memory.Malloc<Head>(1, allocator);
            *m_Head = new Head()
            {
                existenceMark = m_ExistenceMark = ExistenceMark.Create(),
                allocator = allocator,
                lockedMark = 0,
                count = 0
            };
            InitializePtr(expectedCapacity, allocator);
        }

        private void InitializePtr(long expectedCapacity, Allocator allocator)
        {
            expectedCapacity = GetRealCapacity(expectedCapacity);
            long longCount = GetLongCount(expectedCapacity);
            GetRankAndTotalSize(longCount, out int rank, out long totalSize);
            m_Head->capacity = expectedCapacity;
            m_Head->rank = rank;
            m_Head->rankData = (RankData*)Memory.Malloc<byte>(totalSize, allocator);
            for (int i = rank - 1; i >= 0; i--)
            {
                *(m_Head->rankData + i) = new RankData()
                {
                    longCount = longCount
                };
                longCount = (longCount >> 6) + ((longCount & 0b111111) > 0 ? 1 : 0);
            }
            long* basePtr = (long*)(m_Head->rankData + rank);
            long offset = 0;
            for (int i = 0; i < rank; i++)
            {
                var rankData = m_Head->rankData + i;
                rankData->data = basePtr + offset;
                Memory.Clear<long>(rankData->data, rankData->longCount);
                offset += rankData->longCount;
            }
        }

        private long GetRealCapacity(long expectedCapacity)
        {
            long rem = expectedCapacity & 0b111111;
            return (rem == 0 ? expectedCapacity : (expectedCapacity + (64 - rem)));
        }

        private long GetLongCount(long capacity)
        {
            return capacity >> 6;
        }

        private void GetRankAndTotalSize(long longCount, out int rank, out long totalSize)
        {
            rank = 1;
            totalSize = longCount << 3;
            while (longCount > 1)
            {
                longCount = (longCount >> 6) + ((longCount & 0b111111) > 0 ? 1 : 0);
                rank++;
                totalSize += (longCount << 3);
            }
            totalSize += (rank * Memory.SizeOf<RankData>());
        }

        private bool InternalIsNotEmpty()
        {
            return *m_Head->rankData[0].data != 0;
        }

        private void InternalSet(long index, bool value)
        {
            var rankDatas = m_Head->rankData;
            var rank = m_Head->rank;
            var lastValue = value;
            var target = index;
            var countChangd = 0;
            for (int i = rank - 1; i >= 0; i--)
            {
                var rankData = rankDatas[i];
                int moveCount = 6 * (rank - i);
                // exp = rank - i;
                // indexInRank = target / 64^exp
                var indexInRank = target >> moveCount;
                // offset = (target % 64^exp) / (64^(exp-1)) 
                var offset = (int)(target & (-1L >> (64 - moveCount))) >> (moveCount - 6);
                var ptr = rankData.data + indexInRank;
                var compare = 1L << offset;
                bool ori = (*ptr & compare) != 0;
                bool isDiff = lastValue ^ ori;
                if (isDiff)
                {
                    if (lastValue)
                    {
                        // 0 -> 1
                        *ptr |= compare;
                        countChangd = 1;
                    }
                    else
                    {
                        // 1 -> 0
                        *ptr &= ~compare;
                        countChangd = 2;
                    }
                    lastValue = *ptr != 0;
                }
                else
                {
                    break;
                }
            }
            if (countChangd == 1)
            {
                m_Head->count++;
            }
            else if (countChangd == 2)
            {
                m_Head->count--;
            }
        }

        private bool InternalGet(long index)
        {
            long* ptr = m_Head->rankData[m_Head->rank - 1].data + (index >> 6);
            return (*ptr & (1L << (int)(index & 0b111111L))) != 0;
        }

        private long InternalGetFirstOneIndex()
        {
            var rankDatas = m_Head->rankData;
            var rank = m_Head->rank;
            var searchedIndex = 0;
            for (int i = 0; i < rank; i++)
            {
                var rankData = rankDatas[i];
                var val = *(rankData.data + searchedIndex);
                if (val == 0) return -1;
                searchedIndex = (searchedIndex << 6) + BitUtility.GetTrailingZerosCount(val);
            }
            return searchedIndex;
        }

        private void InternalExpand(int count)
        {
            if (count < 0) return;
            long oriCapacity = m_Head->capacity;
            long newCapacity = GetRealCapacity(oriCapacity + count);
            if (newCapacity > oriCapacity)
            {
                var oldRank = m_Head->rank;
                var oldRankDatas = m_Head->rankData;
                InitializePtr(newCapacity, m_Head->allocator);
                var newRank = m_Head->rank;
                var newRankDatas = m_Head->rankData;
                var oldRankData = oldRankDatas[oldRank - 1];
                var newRankData = newRankDatas[newRank - 1];
                var oldLongCount = oldRankData.longCount;
                // copy
                Memory.Copy(newRankData.data, oldRankData.data, oldLongCount * 8);
                Memory.Free(oldRankDatas, m_Head->allocator);
                // reset
                if (newRank > 1)
                {
                    var data = newRankData.data;
                    for (int i = 0; i < oldLongCount; i++)
                    {
                        if (data[i] != 0)
                        {
                            //set
                            long target = i;
                            for (int rankIndex = newRank - 2; rankIndex >= 0; rankIndex--)
                            {
                                var rankData = newRankDatas[rankIndex];
                                int moveCount = 6 * (newRank - rankIndex - 1);
                                // exp = newRank - rankIndex - 1;
                                // indexInRank = target / 64^exp
                                var indexInRank = target >> moveCount;
                                // offset = (target % 64^exp) / (64^(exp-1)) 
                                var offset = (int)(target & (-1L >> (64 - moveCount))) >> (moveCount - 6);
                                var ptr = rankData.data + indexInRank;
                                *ptr |= (1L << offset);
                            }
                        }
                    }
                }
            }
        }

        private void InternalClear()
        {
            var rank = m_Head->rank;
            var rankDatas = m_Head->rankData;
            for (int i = 0; i < rank; i++)
            {
                var rankData = rankDatas[i];
                Memory.Clear<long>(rankData.data, rankData.longCount);
            }
            m_Head->count = 0;
        }

        private void DisposeHead()
        {
            m_Head->existenceMark = ExistenceMark.Null;
            Memory.Free(m_Head->rankData, m_Head->allocator);
            Memory.Free(m_Head, m_Head->allocator);
            m_Head = null;
        }

        #endregion

        #region Check

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckArguments(long expectedCapacity, Allocator allocator)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (expectedCapacity <= 0)
            {
                throw new ArgumentException("expectedCapacity must bigger then 0.");
            }
#endif
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckExists()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (m_Head == null || m_Head->existenceMark != m_ExistenceMark)
            {
                throw new NullReferenceException($"{nameof(UnsafeBitSet)} is yet created or already disposed.");
            }
#endif
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckIndex(long index)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (index < 0 || index >= m_Head->capacity)
            {
                throw new IndexOutOfRangeException($"{nameof(UnsafeBitSet)} index must must >= 0 && < capacity.");
            }
#endif
        }

        #endregion
    }
}