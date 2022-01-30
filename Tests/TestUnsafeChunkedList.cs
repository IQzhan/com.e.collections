using E.Collections.Unsafe;
using NUnit.Framework;
using UnityEngine;

namespace E.Collections.Test
{
    public unsafe class TestUnsafeChunkedList
    {
        [Test]
        public void TestUnsafeChunkedListAdd()
        {
            int elemSize = Memory.SizeOf<int>();
            int count = 21;
            long chunkSize = 16;
            using (UnsafeChunkedList list = new UnsafeChunkedList(elemSize, chunkSize, Unity.Collections.Allocator.Temp))
            {
                CheckEqual(list, 0, chunkSize, elemSize, 0);
                for (int i = 0; i < count; i++)
                {
                    *(int*)list.Add().Value = i;
                }
                CheckEqual(list, 6, chunkSize, elemSize, count);
                foreach(var v in list)
                {
                    Assert.AreEqual(v.Index, *(int*)v.Value);
                }
            }
        }

        [Test]
        public void TestUnsafeChunkedListInsert()
        {
            int elemSize = Memory.SizeOf<int>();
            long chunkSize = 16;
            using (UnsafeChunkedList list = new UnsafeChunkedList(elemSize, chunkSize, Unity.Collections.Allocator.Temp))
            {
                *(int*)list.Insert(0).Value = 4;
                CheckEqual(list, 1, chunkSize, elemSize, 1);
                Assert.AreEqual(4, *(int*)list[0].Value);
                for (int i = 0; i < 20; i++)
                {
                    *(int*)list.Add().Value = i;
                }
                *(int*)list.Insert(15).Value = 8;
                Assert.AreEqual(8, *(int*)list[15].Value);
                Assert.AreEqual(14, *(int*)list[16].Value);
                CheckEqual(list, 6, chunkSize, elemSize, 22);
            }
        }

        [Test]
        public void TestUnsafeChunkedListRemove()
        {
            int elemSize = Memory.SizeOf<int>();
            long chunkSize = 16;
            using (UnsafeChunkedList list = new UnsafeChunkedList(elemSize, chunkSize, Unity.Collections.Allocator.Temp))
            {
                for (int i = 0; i < 20; i++)
                {
                    *(int*)list.Add().Value = i;
                }
                CheckEqual(list, 5, chunkSize, elemSize, 20);
                list.Remove(15);
                Assert.AreEqual(16, *(int*)list[15].Value);
                list.RemoveLast();
                Assert.AreEqual(18, list.Count);
                Assert.AreEqual(18, *(int*)list[list.Count - 1].Value);
                list.SwapLastAndRemove(3);
                Assert.AreEqual(18, *(int*)list[3].Value);
                list.Clear();
                CheckEqual(list, 5, chunkSize, elemSize, 0);
            }
        }

        struct A
        {
            public int index;
            public int value;
        }

        [Test]
        public void TestUnsafeChunkedListRemove1()
        {
            int elemSize = Memory.SizeOf<A>();
            long chunkSize = 1 << 16;
            using (UnsafeChunkedList list = new UnsafeChunkedList(elemSize, chunkSize, Unity.Collections.Allocator.Temp))
            {
                for(int i = 0; i < 1000; i++)
                {
                    *(A*)list.Add().Value = new A()
                    {
                        index = i,
                        value = i
                    };
                }
                for (int i = 0; i < 999; i++)
                {
                    list.SwapLastAndRemove(0);
                    ((A*)list[0].Value)->index = 0;
                }
            }
        }

        private void CheckEqual(UnsafeChunkedList list, int chunkCount, long chunkSize, int elementSize, int count)
        {
            Assert.AreEqual(chunkCount, list.ChunkCount);
            Assert.AreEqual(chunkSize, list.ChunkSize);
            Assert.AreEqual(elementSize, list.ElementSize);
            Assert.AreEqual(count, list.Count);
        }
    }
}