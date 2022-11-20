using System;
using System.Runtime.CompilerServices;

namespace Collections.Extensions.SlotMap
{
    /// <summary>
    /// Represents a 32-bit key of the SlotMap32 data structure.
    /// <para>Components layout:</para>
    /// <para>1. Tag: 2 bits [0 .. 3]</para>
    /// <para>2. Version: 10 bits [1 .. 1023]</para>
    /// <para>3. Index: 20 bits [1 .. 1_048_576]</para>
    /// </summary>
    /// <remarks>If version or index equals to zero (0), it is invalid.</remarks>
    public readonly partial struct SlotMapKey32 : IEquatable<SlotMapKey32>
    {
        public static readonly SlotMapKey32 InvalidValue = default;
        public static readonly SlotMapKey32 MinValue = new(KeyIndex.MinValue, KeyVersion.MinValue);
        public static readonly SlotMapKey32 MaxValue = new(KeyIndex.MaxValue, KeyVersion.MaxValue);

        private readonly uint _raw;

        public SlotMapKey32(KeyIndex index, KeyVersion version)
        {
            Checks.Require(index.IsValid, $"`{nameof(index)}` is invalid");
            Checks.Require(version.IsValid, $"`{nameof(version)}` is invalid");

            _raw = version | index;
        }

        public SlotMapKey32(KeyIndex index, KeyVersion version, KeyTag tag)
        {
            Checks.Require(index.IsValid, $"`{nameof(index)}` is invalid");
            Checks.Require(version.IsValid, $"`{nameof(version)}` is invalid");

            _raw = tag | (version | index);
        }

        public SlotMapKey32(SlotMapKey32 key, KeyVersion version)
        {
            var index = key.Index;

            Checks.Require(index.IsValid, $"`{nameof(index)}` is invalid");
            Checks.Require(version.IsValid, $"`{nameof(version)}` is invalid");

            _raw = version | index;
        }

        public SlotMapKey32(SlotMapKey32 key, KeyVersion version, KeyTag tag)
        {
            var index = key.Index;

            Checks.Require(index.IsValid, $"`{nameof(index)}` is invalid");
            Checks.Require(version.IsValid, $"`{nameof(version)}` is invalid");

            _raw = tag | (version | index);
        }

        public bool IsValid
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                Checks.Require(Index.IsValid, $"`{nameof(Index)}` is invalid");
                Checks.Require(Version.IsValid, $"`{nameof(Version)}` is invalid");

                return Index.IsValid && Version.IsValid;
            }
        }

        public KeyIndex Index
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => KeyIndex.Convert(_raw);
        }

        public KeyVersion Version
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => KeyVersion.Convert(_raw);
        }

        public KeyTag Tag
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => KeyTag.Convert(_raw);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(SlotMapKey32 other)
            => _raw == other._raw;

        public override bool Equals(object obj)
            => obj is SlotMapKey32 other && _raw == other._raw;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
            => _raw.GetHashCode();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(SlotMapKey32 lhs, SlotMapKey32 rhs)
            => lhs._raw == rhs._raw;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(SlotMapKey32 lhs, SlotMapKey32 rhs)
            => lhs._raw != rhs._raw;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator <(SlotMapKey32 lhs, SlotMapKey32 rhs)
            => lhs._raw < rhs._raw;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator >(SlotMapKey32 lhs, SlotMapKey32 rhs)
            => lhs._raw > rhs._raw;
    }
}
