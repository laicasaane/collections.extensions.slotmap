using System;
using System.Runtime.CompilerServices;

namespace Collections.Extensions.SlotMap
{
    partial struct SlotMapKey32
    {
        /// <summary>
        /// Represents a 2 bits tag.
        /// </summary>
        public readonly struct KeyTag : IEquatable<KeyTag>
        {
            private const uint MASK = 0x_C0_00_00_00;
            private const int BITS_SHIFT = 30;

            private const byte MIN = 0x_00;
            private const byte MAX = 0x_03;

            public static readonly KeyTag MinValue = new(MIN);
            public static readonly KeyTag MaxValue = new(MAX);

            private readonly byte _raw;

            public KeyTag(byte value)
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
            public override int GetHashCode()
                => _raw.GetHashCode();

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override string ToString()
                => _raw.ToString();

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static KeyTag ToTag(uint raw)
                => (byte)((raw & MASK) >> BITS_SHIFT);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static uint ClearTag(uint raw)
                => raw & (~MASK);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static implicit operator byte(KeyTag value)
                => value._raw;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static implicit operator KeyTag(byte value)
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

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool operator ==(KeyTag lhs, byte rhs)
                => lhs._raw == rhs;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool operator !=(KeyTag lhs, byte rhs)
                => lhs._raw != rhs;
        }
    }
}
