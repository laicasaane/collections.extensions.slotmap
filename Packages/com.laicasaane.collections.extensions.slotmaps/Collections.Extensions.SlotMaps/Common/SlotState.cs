using System;
using System.Runtime.CompilerServices;

namespace Collections.Extensions.SlotMaps
{
    /// <summary>
    /// Represents a 2 bits state in the range [0..3].
    /// </summary>
    public readonly struct SlotState
        : IEquatable<SlotState>
        , IComparable<SlotState>
    {
        private const uint MASK = 0x_C0_00_00_00;
        private const int BITS_SHIFT = 30;

        private const byte MIN = 0x0;
        private const byte MAX = 0x3;

        private const byte EMPTY     = 0x_0;
        private const byte OCCUPIED  = 0x_1;
        private const byte TOMBSTONE = 0x_2;

        public static readonly SlotState MinValue = new(MIN);
        public static readonly SlotState MaxValue = new(MAX);

        public static readonly SlotState Empty = new(EMPTY);
        public static readonly SlotState Occupied = new(OCCUPIED);
        public static readonly SlotState Tombstone = new(TOMBSTONE);

        private readonly byte _raw;

        public SlotState(byte value)
        {
            Checks.Warning(value <= MAX, $"{nameof(value)} should be lesser than or equal to {MAX}. Value: {value}.");

            if (value > MAX)
                _raw = MAX;
            else
                _raw = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static SlotState Convert(uint raw)
            => (byte)((raw & MASK) >> BITS_SHIFT);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint ClearState(uint raw)
            => raw & (~MASK);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal uint ShiftToUInt32()
            => (uint)_raw << BITS_SHIFT;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(SlotState other)
            => _raw == other._raw;

        public override bool Equals(object obj)
            => obj is SlotState other && _raw == other._raw;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(SlotState other)
            => _raw.CompareTo(other._raw);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
            => _raw.GetHashCode();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString()
        {
            return _raw switch {
                EMPTY => nameof(Empty),
                OCCUPIED => nameof(Occupied),
                TOMBSTONE => nameof(Tombstone),
                _ => _raw.ToString(),
            };
        }

        public bool TryFormat(
              Span<char> destination
            , out int charsWritten
            , ReadOnlySpan<char> format = default
            , IFormatProvider provider = null
        )
        {
            var valueStr = _raw switch {
                EMPTY => nameof(Empty),
                OCCUPIED => nameof(Occupied),
                TOMBSTONE => nameof(Tombstone),
                _ => string.Empty,
            };

            var valueSpan = valueStr.AsSpan();
            var length = valueSpan.Length;

            if (length <= 0)
            {
                return _raw.TryFormat(destination, out charsWritten, format, provider);
            }

            try
            {
                valueSpan.CopyTo(destination[..length]);
                charsWritten = length;
                return true;
            }
            catch
            {
                charsWritten = 0;
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator byte(SlotState value)
            => value._raw;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator SlotState(byte value)
            => new(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator SlotState(sbyte value)
            => new((byte)value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(SlotState lhs, SlotState rhs)
            => lhs._raw == rhs._raw;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(SlotState lhs, SlotState rhs)
            => lhs._raw != rhs._raw;
    }
}
