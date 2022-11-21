using System.Runtime.CompilerServices;

namespace Collections.Extensions.SlotMap
{
    partial class SlotMap<T>
    {
        private readonly struct Address
        {
            public readonly uint PageIndex;
            public readonly uint ItemIndex;

            public Address(uint pageIndex, uint itemIndex)
            {
                this.PageIndex = pageIndex;
                this.ItemIndex = itemIndex;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Deconstruct(out uint pageIndex, out uint itemIndex)
            {
                pageIndex = this.PageIndex;
                itemIndex = this.ItemIndex;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public uint ToIndex(uint pageSize)
                => (this.PageIndex * pageSize) + this.ItemIndex;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static Address FromIndex(uint index, uint pageSize)
                => new(index / pageSize, index % pageSize);
        }
    }
}
