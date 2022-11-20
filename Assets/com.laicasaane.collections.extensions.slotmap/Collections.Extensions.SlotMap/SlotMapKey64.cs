using System;
using System.Runtime.CompilerServices;

namespace Collections.Extensions.SlotMap
{
    /// <summary>
    /// Represents a 64-bit key of the SlotMap64 data structure.
    /// <para>Components layout:</para>
    /// <para>1. Tag: 12 bits [0 .. 4095]</para>
    /// <para>2. Version: 20 bits [1 .. 1_048_575]</para>
    /// <para>3. Index: 32 bits [1 .. 4_294_967_295]</para>
    /// </summary>
    /// <remarks>If version or index equals to zero (0), it is invalid.</remarks>
    public readonly partial struct SlotMapKey64 : IEquatable<SlotMapKey64>
    {
        public static readonly SlotMapKey64 InvalidValue = default;
        public static readonly SlotMapKey64 MinValue = new(KeyIndex.MinValue, KeyVersion.MinValue);
        public static readonly SlotMapKey64 MaxValue = new(KeyIndex.MaxValue, KeyVersion.MaxValue);

        private readonly ulong _raw;

        public SlotMapKey64(KeyIndex index, KeyVersion version)
        {
            Checks.Require(index.IsValid, $"`{nameof(index)}` is invalid");
            Checks.Require(version.IsValid, $"`{nameof(version)}` is invalid");

            _raw = version | index;
        }

        public SlotMapKey64(KeyIndex index, KeyVersion version, KeyTag tag)
        {
            Checks.Require(index.IsValid, $"`{nameof(index)}` is invalid");
            Checks.Require(version.IsValid, $"`{nameof(version)}` is invalid");

            _raw = tag | (version | index);
        }

        public SlotMapKey64(SlotMapKey64 key, KeyVersion version)
        {
            var index = key.Index;

            Checks.Require(index.IsValid, $"`{nameof(index)}` is invalid");
            Checks.Require(version.IsValid, $"`{nameof(version)}` is invalid");

            _raw = version | index;
        }

        public SlotMapKey64(SlotMapKey64 key, KeyVersion version, KeyTag tag)
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
        public bool Equals(SlotMapKey64 other)
            => _raw == other._raw;

        public override bool Equals(object obj)
            => obj is SlotMapKey64 other && _raw == other._raw;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
            => _raw.GetHashCode();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(SlotMapKey64 lhs, SlotMapKey64 rhs)
            => lhs._raw == rhs._raw;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(SlotMapKey64 lhs, SlotMapKey64 rhs)
            => lhs._raw != rhs._raw;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator <(SlotMapKey64 lhs, SlotMapKey64 rhs)
            => lhs._raw < rhs._raw;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator >(SlotMapKey64 lhs, SlotMapKey64 rhs)
            => lhs._raw > rhs._raw;
    }
}
