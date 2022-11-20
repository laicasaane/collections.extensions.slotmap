using System;
using System.Runtime.CompilerServices;

namespace Collections.Extensions.SlotMap
{
    /// <summary>
    /// Represents a 16 bits version in the range [0..65,535].
    /// </summary>
    public readonly struct SlotVersion
        : IEquatable<SlotVersion>
        , IComparable<SlotVersion>
    {
        private const ushort INVALID = 0x_00_00;
        private const ushort MIN     = 0x_00_01;
        private const ushort MAX     = 0x_FF_FF;

        public static readonly SlotVersion InvalidValue = default;
        public static readonly SlotVersion MinValue = new(MIN);
        public static readonly SlotVersion MaxValue = new(MAX);

        private readonly ushort _raw;

        public SlotVersion(ushort value)
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
        public ushort ToUInt16()
            => _raw;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint ToUInt32()
            => _raw;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong ToUInt64()
            => _raw;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ushort(SlotVersion value)
            => value._raw;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator SlotVersion(short value)
            => new((ushort)value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator SlotVersion(ushort value)
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
        public static SlotVersion operator +(SlotVersion lhs, ushort rhs)
            => (ushort)(lhs._raw + rhs);
    }
}
