using NUnit.Framework;
using System;
using System.Text;
using Unity.Burst;
using Unity.Jobs;

namespace E.Collections.Test
{
    public unsafe class TestBitUtility
    {
        [Test]
        public void TestTrailingZerosCount32()
        {
            Assert.AreEqual(0, BitUtility.GetTrailingZerosCount(0));
            for (int i = 0; i < 32; i++)
            {
                int n = 1 << i;
                Assert.AreEqual(i, BitUtility.GetTrailingZerosCount(n));
            }
        }

        [Test]
        public void TestTrailingZerosCount64()
        {
            Assert.AreEqual(0L, BitUtility.GetTrailingZerosCount(0L));
            for (int i = 0; i < 64; i++)
            {
                long n = 1L << i;
                Assert.AreEqual(i, BitUtility.GetTrailingZerosCount(n));
            }
        }

        #region 32

        private struct Node32
        {
            public int value;
            public Node32* exit0;
            public Node32* exit1;
        }

        [Test]
        public void Find32BitIndexesNumber()
        {
            Node32* nodes = stackalloc Node32[32];
            CreateNode32s(nodes);
            int* counts = stackalloc int[32];
            int indexesNumber = 0;
            if (FindEulerCircuit32(nodes, 0, 31, counts, ref indexesNumber))
            {
                Print32(indexesNumber, counts);
            }
        }

        private void CreateNode32s(Node32* nodes)
        {
            int mask = 0b01111;
            for (int value = 0; value < 32; value++)
            {
                int exit0 = (value & mask) << 1;
                int exit1 = exit0 | 0b00001;
                *(nodes + value) = new Node32()
                {
                    value = value,
                    exit0 = nodes + exit0,
                    exit1 = nodes + exit1
                };
            }
        }

        private bool FindEulerCircuit32(Node32* node, int mark, int offsetInNumber, int* counts, ref int indexesNumber)
        {
            int nodeValue = node->value;
            int checkMark = 1 << nodeValue;
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
                if (FindEulerCircuit32(node->exit0, mark, offsetInNumber - 1, counts, ref indexesNumber) ||
                    FindEulerCircuit32(node->exit1, mark, offsetInNumber - 1, counts, ref indexesNumber))
                {
                    // Apply to magicNumber.
                    indexesNumber |= ((nodeValue >> 4) << offsetInNumber);
                    // Save to values.
                    int trai0Count = 31 - offsetInNumber;
                    counts[nodeValue] = trai0Count;
                    return true;
                }
                // No Euler circuit founded.
                return false;
            }
        }

        private void Print32(long indexesNumber, int* values)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < 32; i++)
            {
                if (i % 16 == 0)
                {
                    sb.Append(Environment.NewLine);
                }
                sb.Append($"{*(values + i)} ,");
            }
            UnityEngine.Debug.Log($"32bits IndexesNumber: {indexesNumber} \n Zeros Counts: {sb}");
        }

        #endregion

        #region 64

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
            UnityEngine.Debug.Log($"64bits IndexesNumber: {indexesNumber} \n Zeros Counts: {sb}");
        }

        #endregion
    }
}