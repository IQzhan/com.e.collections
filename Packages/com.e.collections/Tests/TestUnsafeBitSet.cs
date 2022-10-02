using E.Collections.Unsafe;
using NUnit.Framework;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace E.Collections.Test
{
    public class TestUnsafeBitSet
    {
        [Test]
        public void TestUnsafeBitMaskBasic()
        {
            int count = 1 << 18;
            using (var bitMask = new UnsafeBitMask(count, Allocator.Temp))
            {
                //full
                Assert.IsFalse(bitMask.IsNotEmpty());
                for (int i = 0; i < count; i++)
                {
                    Assert.IsFalse(bitMask.Get(i));
                }
                for (int i = 0; i < count; i++)
                {
                    bitMask.Set(i, true);
                }
                Assert.IsTrue(bitMask.IsNotEmpty());
                for (int i = 0; i < count; i++)
                {
                    Assert.IsTrue(bitMask.Get(i));
                }
                bitMask.Clear();
                Assert.IsFalse(bitMask.IsNotEmpty());
                for (int i = 0; i < count; i++)
                {
                    Assert.IsFalse(bitMask.Get(i));
                }

                //not full
                for (int i = 0; i < count; i++)
                {
                    bitMask.Set(i, (i & 1) == 1);
                }
                Assert.IsTrue(bitMask.IsNotEmpty());
                for (int i = 0; i < count; i++)
                {
                    bool val = bitMask.Get(i);
                    if ((i & 1) == 1)
                    {
                        Assert.IsTrue(val);
                    }
                    else
                    {
                        Assert.IsFalse(val);
                    }
                }

                //Expand
                Assert.AreEqual(count, bitMask.Capacity);
                bitMask.Expand(1 << 6);
                Assert.AreEqual(count + (1 << 6), bitMask.Capacity);
                for (int i = 0; i < count; i++)
                {
                    bool val = bitMask.Get(i);
                    if ((i & 1) == 1)
                    {
                        Assert.IsTrue(val);
                    }
                    else
                    {
                        Assert.IsFalse(val);
                    }
                }
            }
        }

        [Test]
        public void TestGetFirstOneIndex()
        {
            int count = 1 << 18;
            using (var bitMask = new UnsafeBitMask(count, Allocator.Temp))
            {
                for (int i = 0; i < count; i++)
                {
                    bitMask.Set(i, (i & 1) == 1);
                }
                long halfCount = count >> 1;
                Assert.AreEqual(halfCount, bitMask.LongCount);
                for (int i = 0; i < halfCount; i++)
                {
                    long index = bitMask.GetFirstThenRemove();
                    Assert.AreEqual((long)(2 * i + 1), index);
                }
                Assert.IsFalse(bitMask.IsNotEmpty());
            }
        }

        [Test]
        public void TestForEach()
        {
            int count = 1 << 18;
            using (var bitMask = new UnsafeBitMask(count, Allocator.Temp))
            {
                for (int i = 0; i < count; i++)
                {
                    bitMask.Set(i, (i & 1) == 1);
                }
                // foreach
                int halfCount1 = 0;
                foreach (var val in bitMask)
                {
                    Assert.IsTrue((val & 1) == 1);
                    halfCount1++;
                }
                Assert.AreEqual(count / 2, halfCount1);
            }
        }

        [Test]
        public void TestRandom()
        {
            int maxNum = 10000;
            int count = 100;
            HashSet<int> set = new HashSet<int>(count);
            for (int i = 0; i < count; i++)
            {
                int value = Random.Range(0, maxNum);
                while (set.Contains(value))
                {
                    value = Random.Range(0, maxNum);
                }
                set.Add(value);
            }
            using (var bitMask = new UnsafeBitMask(maxNum, Allocator.Temp))
            {
                foreach (int value in set)
                {
                    bitMask.Set(value, true);
                }
                Assert.AreEqual(bitMask.Count, count);
                foreach (var index in bitMask)
                {
                    Assert.IsTrue(set.Contains((int)index));
                }
                foreach (var index in bitMask)
                {
                    bitMask.Set(index, false);
                }
                Assert.AreEqual(bitMask.Count, 0);
            }
        }
    }
}