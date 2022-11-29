using System;
using System.Runtime.CompilerServices;

namespace Collections.Extensions.SlotMaps
{
    /// <summary>
    /// Represents a 30 bits version in the range [0..1,073,741,823].
    /// </summary>
    public readonly struct SlotVersion
        : IEquatable<SlotVersion>
        , IComparable<SlotVersion>
    {
        private const uint INVALID    = 0x_00_00_00_00;
        private const uint MIN        = 0x_00_00_00_01;
        private const uint MAX        = 0x_3F_FF_FF_FF;

        public static readonly SlotVersion InvalidValue = default;
        public static readonly SlotVersion MinValue = new(MIN);
        public static readonly SlotVersion MaxValue = new(MAX);

        private readonly uint _raw;

        public SlotVersion(uint value)
        {
            Checks.Require(value != INVALID, $"`{nameof(value)}` must be greater than or equal to {MIN}. Value: {value}.");
            Checks.Warning(value <= MAX, $"`{nameof(value)}` should be lesser than or equal to {MAX}. Value: {value}.");

            _raw = Math.Clamp(value, MIN, MAX);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static SlotVersion Convert(uint raw)
            => raw & MAX;

        public bool IsValid
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _raw != INVALID;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(SlotVersion other)
            => _raw == other._raw;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(SlotVersion other)
            => _raw.CompareTo(other._raw);

        public override bool Equals(object obj)
            => obj is SlotVersion other && _raw == other._raw;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
            => _raw.GetHashCode();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString()
            => _raw.ToString();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ToByte()
            => (byte)_raw;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint ToUInt16()
            => _raw;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint ToUInt32()
            => _raw;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong ToUInt64()
            => _raw;

        public bool TryFormat(
              Span<char> destination
            , out int charsWritten
            , ReadOnlySpan<char> format = default
            , IFormatProvider provider = null
        )
            => _raw.TryFormat(destination, out charsWritten, format, provider);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator uint(SlotVersion value)
            => value._raw;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator SlotVersion(int value)
            => new((uint)value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator SlotVersion(uint value)
            => new(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(SlotVersion lhs, SlotVersion rhs)
            => lhs._raw == rhs._raw;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(SlotVersion lhs, SlotVersion rhs)
            => lhs._raw != rhs._raw;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator <(SlotVersion lhs, SlotVersion rhs)
            => lhs._raw < rhs._raw;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator >(SlotVersion lhs, SlotVersion rhs)
            => lhs._raw > rhs._raw;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SlotVersion operator +(SlotVersion lhs, uint rhs)
            => (uint)(lhs._raw + rhs);
    }
}
