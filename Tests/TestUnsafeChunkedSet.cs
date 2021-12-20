using E.Collections.Unsafe;
using NUnit.Framework;
using Unity.Collections;
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
                        bool exists = tree.TryGetValue(key, out byte* v);
                        Assert.AreEqual(true, exists);
                        Assert.AreEqual(key, *(int*)v);
                        tree.Check();
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
                        tree.Check();
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
    }
}