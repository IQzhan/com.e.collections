using E.Collections.Unsafe;
using NUnit.Framework;

namespace E.Collections.Test
{
    public unsafe class TestUnsafeChunkedQueue
    {
        [Test]
        public void TestUnsafeChunkedQueueEnqueuePeek()
        {
            int elemSize = Memory.SizeOf<int>();
            long chunkSize = 16;
            using (UnsafeChunkedQueue queue = new UnsafeChunkedQueue(elemSize, chunkSize, Unity.Collections.Allocator.Temp))
            {
                for (int i = 0; i < 20; i++)
                {
                    *(int*)queue.Enqueue().Value = i;
                }
                foreach(var v in queue)
                {
                    Assert.AreEqual(v.Index, *(int*)v.Value);
                }
                Assert.AreEqual(20, queue.Count);
            }
        }

        [Test]
        public void TestUnsafeChunkedQueueDequeue()
        {
            int elemSize = Memory.SizeOf<int>();
            long chunkSize = 16;
            using (UnsafeChunkedQueue queue = new UnsafeChunkedQueue(elemSize, chunkSize, Unity.Collections.Allocator.Temp))
            {
                for (int i = 0; i < 20; i++)
                {
                    *(int*)queue.Enqueue().Value = i;
                }
                for (int i = 0; i < 20; i++)
                {
                    int v = *(int*)queue.Dequeue().Value;
                    Assert.AreEqual(i, v);
                    Assert.AreEqual(19 - i, queue.Count);
                }
            }
        }
    }
}