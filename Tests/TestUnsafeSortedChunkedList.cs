using E.Collections.Unsafe;
using NUnit.Framework;

namespace E.Collections.Test
{
    public unsafe class TestUnsafeSortedChunkedList
    {
        [Test]
        public void TestUnsafeSortedChunkedListAdd()
        {
            int elemSize = Memory.SizeOf<int>();
            int chunkSize = 16;
            using (UnsafeSortedChunkedList<int> list = new UnsafeSortedChunkedList<int>(elemSize, chunkSize, Unity.Collections.Allocator.Temp))
            {
                *(int*)list.Add(2) = 2;
                *(int*)list.Add(14) = 14;
                *(int*)list.Add(1) = 4;
                *(int*)list.Add(65) = 65;
                *(int*)list.Add(32) = 32;
                *(int*)list.Add(7) = 7;
                Assert.AreEqual(0, list.IndexOf(1));
                Assert.AreEqual(1, list.IndexOf(2));
                Assert.AreEqual(2, list.IndexOf(7));
                Assert.AreEqual(3, list.IndexOf(14));
                Assert.AreEqual(4, list.IndexOf(32));
                Assert.AreEqual(5, list.IndexOf(65));

                Assert.AreEqual(false, list.Contains(64));
                Assert.AreEqual(true, list.Contains(65));
            }
        }

        [Test]
        public void TestUnsafeSortedChunkedListRemove()
        {
            int elemSize = Memory.SizeOf<int>();
            int chunkSize = 16;
            using (UnsafeSortedChunkedList<int> list = new UnsafeSortedChunkedList<int>(elemSize, chunkSize, Unity.Collections.Allocator.Temp))
            {
                *(int*)list.Add(2) = 2;
                *(int*)list.Add(14) = 14;
                *(int*)list.Add(1) = 4;
                *(int*)list.Add(65) = 65;
                *(int*)list.Add(32) = 32;
                *(int*)list.Add(7) = 7;

                if (list.TryGetValue(14, out byte* value))
                {
                    Assert.AreEqual(14, *(int*)value);
                }
                Assert.AreEqual(false, list.TryGetValue(20, out byte* _));

                list.Remove(14);
                Assert.AreEqual(false, list.TryGetValue(14, out byte* _));
            }
        }
    }
}