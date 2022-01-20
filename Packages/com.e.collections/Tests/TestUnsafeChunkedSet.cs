using E.Collections.Unsafe;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace E.Collections.Test
{
    public unsafe class TestUnsafeChunkedSet
    {
        [Test]
        public void TestUnsafeChunkedSetSet()
        {
            int valueSize = Memory.SizeOf<int>();
            using (UnsafeChunkedSet<int> tree = new UnsafeChunkedSet<int>(valueSize, 1 << 16, Allocator.Temp))
            {
                NativeArray<int> array = default;
                try
                {
                    int count = 10000;
                    array = new NativeArray<int>(count, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                    for (int i = 0; i < count; i++)
                    {
                        array[i] = Random.Range(0, count + 1);
                    }
                    for (int i = 0; i < count; i++)
                    {
                        *(int*)tree.Set(array[i]) = array[i];
                    }
                    for (int i = 0; i < tree.Count; i++)
                    {
                        array[i] = *(int*)tree[i];
                    }
                    for (int i = 0; i < tree.Count; i++)
                    {
                        int key = array[i];
                        int index = tree.IndexOf(key, out byte* v);
                        Assert.AreEqual(true, index != -1);
                        Assert.AreEqual(key, *(int*)v);
                        //tree.Check();
                    }
                }
                catch
                {
                    throw;
                }
                finally
                {
                    array.Dispose();
                }
            }
        }

        [Test]
        public void TestUnsafeChunkedSetRemove()
        {
            int valueSize = Memory.SizeOf<int>();
            using (UnsafeChunkedSet<int> tree = new UnsafeChunkedSet<int>(valueSize, 1 << 16, Allocator.Temp))
            {
                NativeArray<int> array = default;
                try
                {
                    int count = 10000;
                    array = new NativeArray<int>(count, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                    for (int i = 0; i < count; i++)
                    {
                        array[i] = Random.Range(0, count + 1);
                    }
                    for (int i = 0; i < count; i++)
                    {
                        *(int*)tree.Set(array[i]) = array[i];
                    }
                    for (int i = 0; i < tree.Count; i++)
                    {
                        array[i] = *(int*)tree[i];
                    }
                    int treeCount = tree.Count;
                    for (int i = 0; i < treeCount; i++)
                    {
                        int key = array[i];
                        Assert.AreEqual(true, tree.Contains(key));
                        tree.Remove(key);
                        Assert.AreEqual(false, tree.Contains(key));
                        //tree.Check();
                    }
                }
                catch
                {
                    throw;
                }
                finally
                {
                    array.Dispose();
                }
            }
        }

        [Test]
        public void TestUnsafeChunkedSetJob()
        {
            int valueSize = Memory.SizeOf<int>();
            using (UnsafeChunkedSet<int> tree = new UnsafeChunkedSet<int>(valueSize, 1 << 16, Allocator.Temp))
            {
                NativeArray<int> array = default;
                try
                {
                    int count = 10000;
                    array = new NativeArray<int>(count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                    var setJob = new SetJob0()
                    {
                        array = array,
                        set = tree,
                        count = count
                    };
                    var setJob1 = new SetJob1()
                    {
                        array = array,
                        set = tree
                    };
                    for (int i = 0; i < count; i++)
                    {
                        array[i] = Random.Range(0, count + 1);
                    }
                    JobHandle jobHandle = setJob.Schedule(count, 32);
                    jobHandle.Complete();
                    for (int i = 0; i < tree.Count; i++)
                    {
                        array[i] = *(int*)tree[i];
                    }
                    int treeCount = tree.Count;
                    JobHandle jobHandle1 = setJob1.Schedule(treeCount, 32);
                    jobHandle1.Complete();
                }
                catch
                {
                    throw;
                }
                finally
                {
                    array.Dispose();
                }
            }
        }

        [BurstCompile]
        struct SetJob0 : IJobParallelFor
        {
            public UnsafeChunkedSet<int> set;

            public NativeArray<int> array;

            public int count;

            public void Execute(int index)
            {
                using (set.GetLock())
                {
                    *(int*)set.Set(array[index]) = array[index];
                }
            }
        }

        //[BurstCompile]
        struct SetJob1 : IJobParallelFor
        {
            public UnsafeChunkedSet<int> set;

            public NativeArray<int> array;

            public void Execute(int index)
            {
                int key = array[index];
                using (set.GetLock())
                {
                    Assert.IsTrue(set.Contains(key));
                    set.Remove(key);
                    Assert.IsFalse(set.Contains(key));
                    //set.Check();
                }
            }
        }
    }
}