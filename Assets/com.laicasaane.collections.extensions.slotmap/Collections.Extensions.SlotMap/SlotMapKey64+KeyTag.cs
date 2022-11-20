using System;
using System.Runtime.CompilerServices;

namespace Collections.Extensions.SlotMap
{
    partial struct SlotMapKey64
    {
        /// <summary>
        /// Represents a 16 bits tag.
        /// </summary>
        public readonly struct KeyTag : IEquatable<KeyTag>, IComparable<KeyTag>
        {
            private const ushort MIN = 0x_00_00;
            private const ushort MAX = 0x_00_FF;

            public static readonly KeyTag MinValue = new(MIN);
            public static readonly KeyTag MaxValue = new(MAX);

            private readonly ushort _raw;

            public KeyTag(ushort value)
            {
                Checks.Suggest(value <= MAX, $"Tag should be lesser than or equal to {MAX}");

                _raw = Math.Clamp(value, MIN, MAX);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Equals(KeyTag other)
                => _raw == other._raw;

            public override bool Equals(object obj)
                => obj is KeyTag other && _raw == other._raw;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int CompareTo(KeyTag other)
                => _raw.CompareTo(other._raw);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override int GetHashCode()
                => _raw.GetHashCode();

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override string ToString()
                => _raw.ToString();

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static implicit operator ushort(KeyTag value)
                => value._raw;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static implicit operator KeyTag(short value)
                => new((ushort)value);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static implicit operator KeyTag(ushort value)
                => new(value);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool operator ==(KeyTag lhs, KeyTag rhs)
                => lhs._raw == rhs._raw;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool operator !=(KeyTag lhs, KeyTag rhs)
                => lhs._raw != rhs._raw;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool operator <(KeyTag lhs, KeyTag rhs)
                => lhs._raw < rhs._raw;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool operator >(KeyTag lhs, KeyTag rhs)
                => lhs._raw > rhs._raw;
        }
    }
}
