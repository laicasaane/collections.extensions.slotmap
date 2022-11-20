using System;
using System.Runtime.CompilerServices;

namespace Collections.Extensions.SlotMap
{
    partial struct SlotMapKey64
    {
        /// <summary>
        /// Represents a 20 bits version.
        /// </summary>
        public readonly struct KeyVersion : IEquatable<KeyVersion>, IComparable<KeyVersion>
        {
            private const ulong MASK = 0x_00_0F_FF_FF_00_00_00_00;
            private const int BITS_SHIFT = 32;

            private const uint INVALID = 0x_00_00_00_00;
            private const uint MIN     = 0x_00_00_00_01;
            private const uint MAX     = 0x_00_0F_FF_FF;

            public static readonly KeyVersion InvalidValue = default;
            public static readonly KeyVersion MinValue = new(MIN);
            public static readonly KeyVersion MaxValue = new(MAX);

            private readonly uint _raw;

            public KeyVersion(uint value)
            {
                Checks.Require(value != INVALID, $"Version must be greater than or equal to {MIN}");
                Checks.Suggest(value <= MAX, $"Version should be lesser than or equal to {MAX}");

                _raw = Math.Clamp(value, MIN, MAX);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static KeyVersion Convert(ulong raw)
                => (uint)((raw & MASK) >> BITS_SHIFT);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static ulong ClearVersion(ulong raw)
                => raw & (~MASK);

            public bool IsValid
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _raw != INVALID;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal ulong ShiftAndMask()
                => (_raw << BITS_SHIFT) & MASK;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Equals(KeyVersion other)
                => _raw == other._raw;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int CompareTo(KeyVersion other)
                => _raw.CompareTo(other._raw);

            public override bool Equals(object obj)
                => obj is KeyVersion other && _raw == other._raw;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override int GetHashCode()
                => _raw.GetHashCode();

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override string ToString()
                => _raw.ToString();

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static implicit operator uint(KeyVersion value)
                => value._raw;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static implicit operator KeyVersion(uint value)
                => new(value);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool operator ==(KeyVersion lhs, KeyVersion rhs)
                => lhs._raw == rhs._raw;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool operator !=(KeyVersion lhs, KeyVersion rhs)
                => lhs._raw != rhs._raw;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool operator <(KeyVersion lhs, KeyVersion rhs)
                => lhs._raw < rhs._raw;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool operator >(KeyVersion lhs, KeyVersion rhs)
                => lhs._raw > rhs._raw;
        }
    }
}
