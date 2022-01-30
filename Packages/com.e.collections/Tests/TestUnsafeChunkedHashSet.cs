using E.Collections.Unsafe;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace E.Collections.Test
{
    public unsafe class TestUnsafeChunkedHashSet
    {
        private const int HashLength = 2039;

        [Test]
        public void TestUnsafeChunkedHashSetSet()
        {
            int valueSize = Memory.SizeOf<int>();
            using (UnsafeChunkedHashSet<int> tree = new UnsafeChunkedHashSet<int>(HashLength, valueSize, 1 << 16, Allocator.Temp))
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
                        var node = tree.Set(array[i]);
                        *(int*)node.Value = array[i];
                    }
                    for (int i = 0; i < tree.Count; i++)
                    {
                        array[i] = *(int*)tree[i].Value;
                    }
                    for (int i = 0; i < tree.Count; i++)
                    {
                        int key = array[i];
                        tree.TryGetByKey(key, out var node);
                        int index = node.Index;
                        Assert.AreEqual(true, index != -1);
                        Assert.AreEqual(key, *(int*)node.Value);
                        tree.Check(key);
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
        public void TestUnsafeChunkedHashSetRemove()
        {
            int valueSize = Memory.SizeOf<int>();
            using (UnsafeChunkedHashSet<int> tree = new UnsafeChunkedHashSet<int>(HashLength, valueSize, 1 << 16, Allocator.Temp))
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
                        var node = tree.Set(array[i]);
                        *(int*)node.Value = array[i];
                    }
                    for (int i = 0; i < tree.Count; i++)
                    {
                        array[i] = *(int*)tree[i].Value;
                    }
                    int treeCount = tree.Count;
                    for (int i = 0; i < treeCount; i++)
                    {
                        int key = array[i];
                        Assert.AreEqual(true, tree.Contains(key));
                        tree.Remove(key);
                        Assert.AreEqual(false, tree.Contains(key));
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
        public void TestUnsafeChunkedHashSetJob()
        {
            int valueSize = Memory.SizeOf<int>();
            using (UnsafeChunkedHashSet<int> tree = new UnsafeChunkedHashSet<int>(HashLength, valueSize, 1 << 16, Allocator.Temp))
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
                        array[i] = *(int*)tree[i].Value;
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
            public UnsafeChunkedHashSet<int> set;

            public NativeArray<int> array;

            public int count;

            public void Execute(int index)
            {
                using (set.GetLock())
                {
                    var node = set.Set(array[index]);
                    *(int*)node.Value = array[index];
                }
            }
        }

        //[BurstCompile]
        struct SetJob1 : IJobParallelFor
        {
            public UnsafeChunkedHashSet<int> set;

            public NativeArray<int> array;

            public void Execute(int index)
            {
                int key = array[index];
                using (set.GetLock())
                {
                    Assert.IsTrue(set.Contains(key));
                    set.Remove(key);
                    Assert.IsFalse(set.Contains(key));
                }
            }
        }
    }
}