using System.Runtime.CompilerServices;

namespace Collections.Extensions.SlotMaps
{
    public static class Utils
    {
        /// <summary>
        /// Gets the maximum number of elements that may be contained in an array.
        /// </summary>
        /// <returns>The maximum count of elements allowed in any array.</returns>
        /// <remarks>
        /// <para>
        /// This property represents a runtime limitation, the maximum number of elements (not bytes)
        /// the runtime will allow in an array. There is no guarantee that an allocation under this length
        /// will succeed, but all attempts to allocate a larger array will fail.
        /// </para>
        /// <para>This property only applies to single-dimension, zero-bound (SZ) arrays.</para>
        /// <para>Source: <seealso href="https://github.com/dotnet/runtime/blob/77de7840c7ad806cd5ad5dc9555ad3d3bed2a6e5/src/libraries/System.Private.CoreLib/src/System/Array.cs#L2067">Array.Length</seealso></para>
        /// </remarks>
        public const uint ARRAY_MAX_LENGTH = 0X7FFFFFC7;

        public const uint MAX_SLOT_INDEX = uint.MaxValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsPowerOfTwo(uint x)
            => (x != 0) && ((x & (x - 1)) == 0);

        public static uint GetMaxPageCount(uint pageSize)
        {
            var maxPageCount = MAX_SLOT_INDEX / pageSize;

            if (MAX_SLOT_INDEX % pageSize != 0)
                maxPageCount += 1;

            return (maxPageCount < ARRAY_MAX_LENGTH)
                ? maxPageCount
                : ARRAY_MAX_LENGTH;
        }

        internal static bool FindPagedAddress(
              int pageLength
            , uint pageSize
            , in SlotKey key
            , out PagedAddress address
        )
        {
            if (key.IsValid == false)
            {
                Checks.Warning(false, $"Key {key} is invalid");

                address = default;
                return false;
            }

            address = PagedAddress.FromIndex(key.Index, pageSize);
            var pageCount = (uint)pageLength;

            if (address.PageIndex >= pageCount)
            {
                Checks.Warning(false
                    , $"Key index {key.Index} is out of range."
                );

                address = default;
                return false;
            }

            return true;
        }
    }
}
