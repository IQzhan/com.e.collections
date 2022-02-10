using E.Collections.Unsafe;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace E.Collections
{
    public unsafe struct BitMap :
        ICollection<bool>,
        IResizeable,
        ILockable,
        IDisposable
    {
        internal struct Head
        {
            public ExistenceMark existenceMark;
            public Allocator allocator;
            public int lockedMark;
            public int count;
            public ulong capacity;
            public ulong size;
            public long* array;
        }

        [NativeDisableUnsafePtrRestriction]
        private Head* m_Head;

        private ExistenceMark m_ExistenceMark;

        public bool IsCreated => m_Head != null && m_Head->existenceMark == m_ExistenceMark;

        public int Count => IsCreated ? m_Head->count : 0;

        public BitMap(ulong expectedCapacity, Allocator allocator)
        {
            m_ExistenceMark = default;
            m_Head = default;
            InitializeHead(expectedCapacity, allocator);
        }

        public void Extend(int count)
        {
            CheckExists();

        }

        public void Clear()
        {
            CheckExists();

        }

        public void Dispose()
        {
            CheckExists();

        }

        public SpinLock GetLock()
        {
            CheckExists();
            return new SpinLock(ref m_Head->lockedMark);
        }

        public IEnumerator<bool> GetEnumerator()
        {
            return default;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return default;
        }

        private void InitializeHead(ulong expectedCapacity, Allocator allocator)
        {
            ulong rem = expectedCapacity % 64;
            expectedCapacity = (rem == 0 ? expectedCapacity : (expectedCapacity + (64 - rem)));
            m_Head = (Head*)Memory.Malloc<Head>(1, allocator);
            *m_Head = new Head()
            {
                existenceMark = m_ExistenceMark = ExistenceMark.Create(),
                allocator = allocator,
                lockedMark = 0,
                count = 0,
                capacity = expectedCapacity,
                size = expectedCapacity / 8,
                array = null
            };

        }

        public void Set(ulong index, bool value)
        {
            CheckExists();
            CheckIndex(index);

        }

        public bool Get(ulong index)
        {
            CheckExists();
            CheckIndex(index);
            return default;
        }

        public int GetFirstOneIndex()
        {
            return default;
        }

        public int GetAndRemoveFirstOneIndex()
        {

            return default;
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckExists()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (m_Head == null || m_Head->existenceMark != m_ExistenceMark)
            {
                throw new NullReferenceException($"{nameof(BitMap)} is yet created or already disposed.");
            }
#endif
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckIndex(ulong index)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (index >= m_Head->capacity)
            {
                throw new IndexOutOfRangeException($"{nameof(BitMap)} index must < capacity.");
            }
#endif
        }
    }
}