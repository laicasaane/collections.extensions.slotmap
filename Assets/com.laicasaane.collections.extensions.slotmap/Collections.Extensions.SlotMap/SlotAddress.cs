using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Collections.Extensions.SlotMap
{
    [StructLayout(LayoutKind.Explicit)]
    public readonly struct SlotAddress : IEquatable<SlotAddress>
    {
        [FieldOffset(0)]
        private readonly ulong _raw;

        [FieldOffset(0)]
        private readonly uint _pageIndex;

        [FieldOffset(4)]
        private readonly uint _itemIndex;

        public SlotAddress(uint pageIndex, uint itemIndex) : this()
        {
            _pageIndex = pageIndex;
            _itemIndex = itemIndex;
        }

        public ulong Raw
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _raw;
        }

        public uint PageIndex
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _pageIndex;
        }

        public uint ItemIndex
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _itemIndex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out uint pageIndex, out uint itemIndex)
        {
            pageIndex = _pageIndex;
            itemIndex = _itemIndex;
        }

        public override string ToString()
            => $"({_pageIndex}, {_itemIndex})";

        public bool Equals(SlotAddress other)
            => _pageIndex == other._pageIndex && _itemIndex == other._itemIndex;

        public override bool Equals(object obj)
            => obj is SlotAddress other && _pageIndex == other._pageIndex && _itemIndex == other._itemIndex;

        public override int GetHashCode()
            => _raw.GetHashCode();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint ToIndex(uint pageSize)
            => (_pageIndex * pageSize) + _itemIndex;

        public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider provider = null)
        {
            var openQuoteCharsWritten = 0;
            destination[openQuoteCharsWritten++] = '(';

            destination = destination[openQuoteCharsWritten..];

            if (_pageIndex.TryFormat(destination, out var pageIndexCharsWritten, format, provider) == false)
            {
                charsWritten = 0;
                return false;
            }

            destination[pageIndexCharsWritten++] = ',';
            destination[pageIndexCharsWritten++] = ' ';

            destination = destination[pageIndexCharsWritten..];

            if (_itemIndex.TryFormat(destination, out var itemIndexCharsWritten, format, provider) == false)
            {
                charsWritten = 0;
                return false;
            }

            destination[itemIndexCharsWritten++] = ')';

            charsWritten = openQuoteCharsWritten + pageIndexCharsWritten + itemIndexCharsWritten;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SlotAddress FromIndex(uint index, uint pageSize)
            => new(index / pageSize, index % pageSize);
    }
}
