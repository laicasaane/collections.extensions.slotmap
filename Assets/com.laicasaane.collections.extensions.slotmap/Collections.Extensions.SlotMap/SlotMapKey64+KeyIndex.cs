using System;
using System.Runtime.CompilerServices;

namespace Collections.Extensions.SlotMap
{
    partial struct SlotMapKey64
    {
        /// <summary>
        /// Represents a 32 bits index.
        /// </summary>
        public readonly struct KeyIndex : IEquatable<KeyIndex>, IComparable<KeyIndex>
        {
            private const ulong MASK = 0x_00_00_00_00_FF_FF_FF_FF;

            private const ulong INVALID = 0x_00_00_00_00_00_00_00_00;
            private const ulong MIN     = 0x_00_00_00_00_00_00_00_01;
            private const ulong MAX     = 0x_00_00_00_00_FF_FF_FF_FF;

            public static readonly KeyIndex InvalidValue = default;
            public static readonly KeyIndex MinValue = new(MIN);
            public static readonly KeyIndex MaxValue = new(MAX);

            private readonly ulong _raw;

            public KeyIndex(ulong value)
            {
                Checks.Require(value != INVALID, $"Index must be greater than or equal to {MIN}");
                Checks.Suggest(value <= MAX, $"Index should be lesser than or equal to {MAX}");

                _raw = Math.Clamp(value, MIN, MAX);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static KeyIndex Convert(ulong raw)
                => raw & MASK;

            public bool IsValid
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _raw != INVALID;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Equals(KeyIndex other)
                => _raw == other._raw;

            public override bool Equals(object obj)
                => obj is KeyIndex other && _raw == other._raw;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override int GetHashCode()
                => _raw.GetHashCode();

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override string ToString()
                => _raw.ToString();

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int CompareTo(KeyIndex other)
                => _raw.CompareTo(other._raw);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static implicit operator ulong(KeyIndex value)
                => value._raw;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static implicit operator KeyIndex(int value)
                => new((ulong)value);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static implicit operator KeyIndex(ulong value)
                => new(value);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool operator ==(KeyIndex lhs, KeyIndex rhs)
                => lhs._raw == rhs._raw;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool operator !=(KeyIndex lhs, KeyIndex rhs)
                => lhs._raw != rhs._raw;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool operator <(KeyIndex lhs, KeyIndex rhs)
                => lhs._raw < rhs._raw;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool operator >(KeyIndex lhs, KeyIndex rhs)
                => lhs._raw > rhs._raw;
        }
    }
}
