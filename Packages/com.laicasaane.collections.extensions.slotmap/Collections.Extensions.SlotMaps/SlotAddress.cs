﻿using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Collections.Extensions.SlotMaps
{
    [StructLayout(LayoutKind.Explicit)]
    public readonly struct SlotAddress : IEquatable<SlotAddress>
    {
        public static readonly SlotAddress MinValue = new(uint.MinValue, uint.MinValue);
        public static readonly SlotAddress MaxValue = new(uint.MaxValue, uint.MaxValue);

        [FieldOffset(0)]
        private readonly ulong _raw;

        [FieldOffset(0)]
        private readonly uint _slotIndex;

        [FieldOffset(4)]
        private readonly uint _pageIndex;

        public SlotAddress(uint pageIndex, uint slotIndex) : this()
        {
            _pageIndex = pageIndex;
            _slotIndex = slotIndex;
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

        public uint SlotIndex
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _slotIndex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out uint pageIndex, out uint slotIndex)
        {
            pageIndex = _pageIndex;
            slotIndex = _slotIndex;
        }

        public override string ToString()
            => $"({_pageIndex}, {_slotIndex})";

        public bool Equals(SlotAddress other)
            => _raw == other._raw;

        public override bool Equals(object obj)
            => obj is SlotAddress other && _raw == other._raw;

        public override int GetHashCode()
            => _raw.GetHashCode();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint ToIndex(uint pageSize)
            => (_pageIndex * pageSize) + _slotIndex;

        public bool TryFormat(
              Span<char> destination
            , out int charsWritten
            , ReadOnlySpan<char> format = default
            , IFormatProvider provider = null
        )
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

            if (_slotIndex.TryFormat(destination, out var itemIndexCharsWritten, format, provider) == false)
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SlotAddress FromIndex(long index, uint pageSize)
            => new((uint)(index / pageSize), (uint)(index % pageSize));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ulong(SlotAddress value)
            => value._raw;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(SlotAddress lhs, SlotAddress rhs)
            => lhs._raw == rhs._raw;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(SlotAddress lhs, SlotAddress rhs)
            => lhs._raw != rhs._raw;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator <(SlotAddress lhs, SlotAddress rhs)
            => lhs._raw < rhs._raw;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator >(SlotAddress lhs, SlotAddress rhs)
            => lhs._raw > rhs._raw;
    }
}
