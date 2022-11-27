using System;
using System.Runtime.CompilerServices;

#if ENABLE_SLOTMAP_KEY_TAG
using s_version = System.Int16;
using u_version = System.UInt16;
#else
using s_version = System.Int32;
using u_version = System.UInt32;
#endif

namespace Collections.Extensions.SlotMaps
{
#if ENABLE_SLOTMAP_KEY_TAG
    /// <summary>
    /// Represents a 16 bits version in the range [0..65,535].
    /// </summary>
#else
    /// <summary>
    /// Represents a 32 bits version in the range [0..4,294,967,295].
    /// </summary>
#endif
    public readonly struct SlotVersion
        : IEquatable<SlotVersion>
        , IComparable<SlotVersion>
    {
#if ENABLE_SLOTMAP_KEY_TAG
        private const u_version INVALID = 0x_00_00;
        private const u_version MIN     = 0x_00_01;
        private const u_version MAX     = 0x_FF_FF;
#else
        private const u_version INVALID = 0x_00_00_00_00;
        private const u_version MIN     = 0x_00_00_00_01;
        private const u_version MAX     = 0x_FF_FF_FF_FF;
#endif

        public static readonly SlotVersion InvalidValue = default;
        public static readonly SlotVersion MinValue = new(MIN);
        public static readonly SlotVersion MaxValue = new(MAX);

        private readonly u_version _raw;

        public SlotVersion(u_version value)
        {
            Checks.Require(value != INVALID, $"Version must be greater than or equal to {MIN}");
            Checks.Suggest(value <= MAX, $"Version should be lesser than or equal to {MAX}");

            _raw = Math.Clamp(value, MIN, MAX);
        }

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
        public u_version ToUInt16()
            => _raw;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint ToUInt32()
            => _raw;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong ToUInt64()
            => _raw;

        public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider provider = null)
            => _raw.TryFormat(destination, out charsWritten, format, provider);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator u_version(SlotVersion value)
            => value._raw;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator SlotVersion(s_version value)
            => new((u_version)value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator SlotVersion(u_version value)
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
        public static SlotVersion operator +(SlotVersion lhs, u_version rhs)
            => (u_version)(lhs._raw + rhs);
    }
}
