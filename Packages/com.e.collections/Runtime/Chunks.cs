namespace E.Collections.Unsafe
{
    public unsafe struct Chunk
    {
        internal struct Head
        {
            

        }
        //usedMark

        internal Head* m_Head;

        public struct Single
        {

        }

    }

    public unsafe struct UsedMarks
    {


        public static int GetStructSize(int count)
        {
            int a = (count / 64);
            return default;
        }

        public void Initialize(byte* ptrStart, int count)
        {

        }

        public void Copy(UsedMarks from)
        {

        }

        public void Set(int index, bool value)
        {

        }

        public bool TryGetUnused(out int index)
        {
            index = default;
            return default;
        }

        public bool Get(int index)
        {
            return default;
        }

        public void Clear()
        {

        }
    }

    public unsafe struct Chunks
    {
        internal struct Head
        {
            
        }

        internal Head* m_Head;

    }
}