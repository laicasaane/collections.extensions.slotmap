#if __SLOTMAP_UNITY_COLLECTIONS__

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Collections.Extensions.SlotMaps
{
    internal static class NativeArrayUtils
    {
        public static void Grow<T>(
              ref this NativeArray<T> array
            , uint amount
            , Allocator allocator
            , NativeArrayOptions options = NativeArrayOptions.ClearMemory
        )
            where T : struct
        {
            var newArray = new NativeArray<T>((int)(array.Length + amount), allocator, options);
            NativeArray<T>.Copy(array, 0, newArray, 0, array.Length);
            array.Dispose();
            array = newArray;
        }

        public unsafe static ref T GetElementAsRef<T>(this NativeArray<T> array, int index)
            where T : struct
        {
            var buffer = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(array);
            return ref UnsafeUtility.ArrayElementAsRef<T>(buffer, index);
        }
    }
}

#endif
