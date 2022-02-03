using NUnit.Framework;
using System;
using System.Text;

namespace E.Collections.Test
{
    public unsafe class TestTrailingZerosCount
    {
        [Test]
        public void TestTrailingZerosCount32()
        {
            int a = 0b1000000000000001110101101100000;
            int b = 0b0000000000000000000100000000000;
            Assert.AreEqual(11, Utility.TrailingZerosCount(b));
            Assert.AreEqual(5, Utility.TrailingZerosCount(a));
            Assert.AreEqual(0, Utility.TrailingZerosCount(1));
            Assert.AreEqual(0, Utility.TrailingZerosCount(0));
        }

        [Test]
        public void TestTrailingZerosCount64()
        {
            long a = 0b1000000000000001110101101100000;
            long b = 0b0000000000000000000100000000000;
            Assert.AreEqual(11, Utility.TrailingZerosCount(b));
            Assert.AreEqual(5, Utility.TrailingZerosCount(a));
            Assert.AreEqual(0, Utility.TrailingZerosCount(1));
            Assert.AreEqual(0, Utility.TrailingZerosCount(0));
        }

        private struct Node64
        {
            public int value;
            public Node64* exit0;
            public Node64* exit1;
        }

        [Test]
        public void Find64BitIndexesNumber()
        {
            Node64* nodes = stackalloc Node64[64];
            CreateNode64s(nodes);
            int* counts = stackalloc int[64];
            long indexesNumber = 0;
            if (FindEulerCircuit64(nodes, 0, 63, counts, ref indexesNumber))
            {
                Print64(indexesNumber, counts);
            }
        }

        private void CreateNode64s(Node64* nodes)
        {
            int mask = 0b011111;
            for (int value = 0; value < 64; value++)
            {
                int exit0 = (value & mask) << 1;
                int exit1 = exit0 | 0b000001;
                *(nodes + value) = new Node64()
                {
                    value = value,
                    exit0 = nodes + exit0,
                    exit1 = nodes + exit1
                };
            }
        }

        private bool FindEulerCircuit64(Node64* node, long mark, int offsetInNumber, int* counts, ref long indexesNumber)
        {
            int nodeValue = node->value;
            long checkMark = 1L << nodeValue;
            if ((mark & checkMark) == checkMark)
            {
                // If this node has already marked.
                // Check whether Euler circuit has formed.
                // -1 means all bits are 1, all nodes are marked.
                // nodeValue == 0 means Euler circuit must end at the start node.
                return (mark == -1) && (nodeValue == 0);
            }
            else
            {
                // Mark this node.
                mark |= checkMark;
                // Check which path can form Euler circuit.
                if (FindEulerCircuit64(node->exit0, mark, offsetInNumber - 1, counts, ref indexesNumber) ||
                    FindEulerCircuit64(node->exit1, mark, offsetInNumber - 1, counts, ref indexesNumber))
                {
                    // Apply to magicNumber.
                    indexesNumber |= (((long)nodeValue >> 5) << offsetInNumber);
                    // Save to values.
                    int trai0Count = 63 - offsetInNumber;
                    counts[nodeValue] = trai0Count;
                    return true;
                }
                // No Euler circuit founded.
                return false;
            }
        }

        private void Print64(long indexesNumber, int* values)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < 64; i++)
            {
                if (i % 16 == 0)
                {
                    sb.Append(Environment.NewLine);
                }
                sb.Append($"{*(values + i)} ,");
            }
            UnityEngine.Debug.Log($"64bits IndexesNumber: {indexesNumber} \n Zeros Counts: {sb.ToString()}");
        }
    }
}