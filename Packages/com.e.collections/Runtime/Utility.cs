namespace E.Collections
{
    public static class Utility
    {
        /// <summary>
        /// 00000111011111001011010100110001
        /// </summary>
        private const int Indexes32 = 0x077CB531;

        /// <summary>
        /// 0000001000011000101000111001001011001101001111010101110110111111
        /// </summary>
        private const long Indexes64 = 0x0218A392CD3D5DBF;

        private static readonly int[] Counts32 = new int[32]
        {
            0, 1, 28, 2, 29, 14, 24, 3, 30, 22, 20, 15, 25, 17, 4, 8,
            31, 27, 13, 23, 21, 19, 16, 7, 26, 12, 18, 6, 11, 5, 10, 9
        };

        private static readonly int[] Counts64 = new int[64]
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
        public static int TrailingZerosCount(int v)
            => Counts32[((uint)((v & -v) * Indexes32)) >> 27];

        /// <summary>
        /// Count of trailing zeros in 64-bit.
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        public static long TrailingZerosCount(long v)
            => Counts64[((ulong)((v & -v) * Indexes64)) >> 58];
    }
}