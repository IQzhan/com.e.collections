using NUnit.Framework;
using Unity.Collections;

namespace E.Collections.Test
{
    public class TestUnsafeBitSet
    {
        [Test]
        public void TestUnsafeBitSetFunctions()
        {
            int count = 1 << 18;
            using (var bitSet = new UnsafeBitSet(count, Allocator.Temp))
            {
                Assert.IsFalse(bitSet.IsNotEmpty());
                for (int i = 0; i < count; i++)
                {
                    Assert.IsFalse(bitSet.Get(i));
                }
                for (int i = 0; i < count; i++)
                {
                    bitSet.Set(i, true);
                }
                Assert.IsTrue(bitSet.IsNotEmpty());
                for (int i = 0; i < count; i++)
                {
                    Assert.IsTrue(bitSet.Get(i));
                }
                bitSet.Clear();
                Assert.IsFalse(bitSet.IsNotEmpty());
                for (int i = 0; i < count; i++)
                {
                    Assert.IsFalse(bitSet.Get(i));
                }

                for (int i = 0; i < count; i++)
                {
                    bitSet.Set(i, (i & 1) == 1);
                }
                Assert.IsTrue(bitSet.IsNotEmpty());
                for (int i = 0; i < count; i++)
                {
                    bool val = bitSet.Get(i);
                    if ((i & 1) == 1)
                    {
                        Assert.IsTrue(val);
                    }
                    else
                    {
                        Assert.IsFalse(val);
                    }
                }

                //GetFirstOneIndex
                long halfCount = count >> 1;
                Assert.AreEqual(halfCount, bitSet.LongCount);
                for (int i = 0; i < halfCount; i++)
                {
                    long index = bitSet.GetFirstOneIndexThenSetZero();
                    Assert.AreEqual((long)(2 * i + 1), index);
                }
                Assert.IsFalse(bitSet.IsNotEmpty());
                
                //Expand
                for (int i = 0; i < count; i++)
                {
                    bitSet.Set(i, (i & 1) == 1);
                }
                Assert.AreEqual(count, bitSet.Capacity);
                bitSet.Expand(1 << 6);
                Assert.AreEqual(count + (1 << 6), bitSet.Capacity);
                for (int i = 0; i < count; i++)
                {
                    bool val = bitSet.Get(i);
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
    }
}