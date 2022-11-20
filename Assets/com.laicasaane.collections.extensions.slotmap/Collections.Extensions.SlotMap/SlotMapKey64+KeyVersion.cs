using System;
using System.Runtime.CompilerServices;

namespace Collections.Extensions.SlotMap
{
    partial struct SlotMapKey64
    {
        /// <summary>
        /// Represents a 16 bits version.
        /// </summary>
        public readonly struct KeyVersion : IEquatable<KeyVersion>, IComparable<KeyVersion>
        {
            private const ushort INVALID = 0x_00_00;
            private const ushort MIN     = 0x_00_01;
            private const ushort MAX     = 0x_FF_FF;

            public static readonly KeyVersion InvalidValue = default;
            public static readonly KeyVersion MinValue = new(MIN);
            public static readonly KeyVersion MaxValue = new(MAX);

            private readonly ushort _raw;

            public KeyVersion(ushort value)
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
            public static implicit operator ushort(KeyVersion value)
                => value._raw;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static implicit operator KeyVersion(short value)
                => new((ushort)value);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static implicit operator KeyVersion(ushort value)
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
