using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace E.Collections.Unsafe
{
    public static unsafe class Memory
    {
        private static readonly int m_PtrSize;

        public static int PtrSize => m_PtrSize;

        static Memory()
        {
            byte** b = default;
            m_PtrSize = (int)((long)(b + 1) - (long)b);
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public static void CheckType(Type type)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (type == null)
            {
                throw new ArgumentNullException("type", "can not be null.");
            }
            if (!UnsafeUtility.IsBlittable(type))
            {
                throw new ArgumentException($"{type} must be blittable.");
            }
#endif
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public static void CheckType<T>()
            where T : struct
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!UnsafeUtility.IsBlittable<T>())
            {
                throw new ArgumentException($"{typeof(T)} must be a blittable.");
            }
#endif
        }

        public static void* Malloc<T>(long count, Allocator allocator) where T : struct
        {
            return UnsafeUtility.Malloc(SizeOf<T>() * count, AlignOf<T>(), allocator);
        }

        public static void* Malloc(Type type, long count, Allocator allocator)
        {
            return UnsafeUtility.Malloc(SizeOf(type) * count, AlignOf(type), allocator);
        }

        public static void* Malloc(long size, int alignment, Allocator allocator)
        {
            return UnsafeUtility.Malloc(size, alignment, allocator);
        }

        public static void Free(void* ptr, Allocator allocator)
        {
            UnsafeUtility.Free(ptr, allocator);
        }

        public static int SizeOf<T>() where T : struct
        {
            return Marshal.SizeOf<T>();
        }

        public static int SizeOf(Type type)
        {
            return Marshal.SizeOf(type);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AlignOfHelper<T> where T : struct
        {
            public byte dummy;
            public T data;
        }

        private static readonly Type alignOfHelperType = typeof(AlignOfHelper<>);

        public static int AlignOf<T>() where T : struct
        {
            return Marshal.SizeOf<AlignOfHelper<T>>() - Marshal.SizeOf<T>();
        }

        public static int AlignOf(Type type)
        {
            Type helper = alignOfHelperType.MakeGenericType(type);
            return Marshal.SizeOf(helper) - Marshal.SizeOf(type);
        }

        public static object PtrToStructure(void* ptr, Type structureType)
        {
            return Marshal.PtrToStructure((IntPtr)ptr, structureType);
        }

        public static object PtrToStructure(IntPtr ptr, Type structureType)
        {
            return Marshal.PtrToStructure(ptr, structureType);
        }

        public static T PtrToStructure<T>(void* ptr)
        {
            return Marshal.PtrToStructure<T>((IntPtr)ptr);
        }

        public static T PtrToStructure<T>(IntPtr ptr)
        {
            return Marshal.PtrToStructure<T>(ptr);
        }

        public static void StructureToPtr(object structure, void* ptr, bool fDeleteOld)
        {
            Marshal.StructureToPtr(structure, (IntPtr)ptr, fDeleteOld);
        }

        public static void StructureToPtr(object structure, IntPtr ptr, bool fDeleteOld)
        {
            Marshal.StructureToPtr(structure, ptr, fDeleteOld);
        }

        public static void StructureToPtr<T>(T structure, void* ptr, bool fDeleteOld)
        {
            Marshal.StructureToPtr(structure, (IntPtr)ptr, fDeleteOld);
        }

        public static void StructureToPtr<T>(T structure, IntPtr ptr, bool fDeleteOld)
        {
            Marshal.StructureToPtr(structure, ptr, fDeleteOld);
        }
    }
}