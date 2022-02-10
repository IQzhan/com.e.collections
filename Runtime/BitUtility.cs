namespace E.Collections
{
    public static class BitUtility
    {
        /// <summary>
        /// 0000 0100 0110 0101 0011 1010 1101 1111
        /// </summary>
        private const int Indexes32 = 0x04653ADF;

        /// <summary>
        /// 0000 0010 0001 1000 1010 0011 1001 0010
        /// 1100 1101 0011 1101 0101 1101 1011 1111
        /// </summary>
        private const long Indexes64 = 0x0218A392CD3D5DBF;

        private static readonly int[] TrailingZerosCount32 = new int[32]
        {
            0 ,1 ,2 ,6 ,3 ,11 ,7 ,16 ,4 ,14 ,12 ,21 ,8 ,23 ,17 ,26 ,
            31 ,5 ,10 ,15 ,13 ,20 ,22 ,25 ,30 ,9 ,19 ,24 ,29 ,18 ,28 ,27
        };

        private static readonly int[] TrailingZerosCount64 = new int[64]
        {
            0 ,1 ,2 ,7 ,3 ,13 ,8 ,19 ,4 ,25 ,14 ,28 ,9 ,34 ,20 ,40 ,
            5 ,17 ,26 ,38 ,15 ,46 ,29 ,48 ,10 ,31 ,35 ,54 ,21 ,50 ,41 ,57 ,
            63 ,6 ,12 ,18 ,24 ,27 ,33 ,39 ,16 ,37 ,45 ,47 ,30 ,53 ,49 ,56 ,
            62 ,11 ,23 ,32 ,36 ,44 ,52 ,55 ,61 ,22 ,43 ,51 ,60 ,42 ,59 ,58
        };

        /// <summary>
        /// Count of trailing zeros in 32-bit.
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        public static int GetTrailingZerosCount(int v)
            => TrailingZerosCount32[((uint)((v & -v) * Indexes32)) >> 27];

        /// <summary>
        /// Count of trailing zeros in 64-bit.
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        public static long GetTrailingZerosCount(long v)
            => TrailingZerosCount64[((ulong)((v & -v) * Indexes64)) >> 58];

        public static int GetLowestOne(int v)
            => v & -v;

        public static long GetLowestOne(long v)
            => v & -v;

        public static int RemoveLowestOne(int v)
            => v & (v - 1);

        public static long RemoveLowestOne(long v)
            => v & (v - 1);
    }
}