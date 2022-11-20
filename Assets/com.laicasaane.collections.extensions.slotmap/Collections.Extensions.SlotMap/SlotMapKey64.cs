using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Collections.Extensions.SlotMap
{
    /// <summary>
    /// Represents a 64-bit key of the SlotMap64 data structure.
    /// <para>Components layout:</para>
    /// <para>1. Tag: 16 bits [0 .. 65_535]</para>
    /// <para>2. Version: 16 bits [1 .. 65_535]</para>
    /// <para>3. Index: 32 bits [0 .. 4_294_967_295]</para>
    /// </summary>
    /// <remarks>If version equals to zero (0), it is invalid.</remarks>
    [StructLayout(LayoutKind.Explicit)]
    public readonly partial struct SlotMapKey64 : IEquatable<SlotMapKey64>
    {
        public static readonly SlotMapKey64 InvalidValue = default;
        public static readonly SlotMapKey64 MinValue = new(KeyIndex.MinValue, KeyVersion.MinValue);
        public static readonly SlotMapKey64 MaxValue = new(KeyIndex.MaxValue, KeyVersion.MaxValue);

        [FieldOffset(0)]
        private readonly ulong _raw;

        [FieldOffset(0)]
        public readonly KeyTag Tag;

        [FieldOffset(4)]
        public readonly KeyVersion Version;

        [FieldOffset(8)]
        public readonly KeyIndex Index;

        public SlotMapKey64(KeyIndex index) : this()
        {
            Version = KeyVersion.MinValue;
            Index = index;
        }

        public SlotMapKey64(KeyIndex index, KeyTag tag) : this()
        {
            Tag = tag;
            Version = KeyVersion.MinValue;
            Index = index;
        }

        public SlotMapKey64(KeyIndex index, KeyVersion version) : this()
        {
            Checks.Require(version.IsValid, $"`{nameof(version)}` is invalid");

            Version = version;
            Index = index;
        }

        public SlotMapKey64(KeyIndex index, KeyVersion version, KeyTag tag) : this()
        {
            Checks.Require(version.IsValid, $"`{nameof(version)}` is invalid");

            Tag = tag;
            Version = version;
            Index = index;
        }

#if DISABLE_SLOTMAP_CHECKS
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public SlotMapKey64 WithVersion(KeyVersion version)
        {
            Checks.Require(version.IsValid, $"`{nameof(version)}` is invalid");

            return new(Index, version, Tag);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SlotMapKey64 WithTag(KeyTag tag)
            => new(Index, Version, tag);

        public void Deconstruct(out KeyIndex index, out KeyVersion version, out KeyTag tag)
        {
            index = Index;
            version = Version;
            tag = Tag;
        }

        /// <summary>
        /// The key is only valid if its <see cref="Version"/> is valid.
        /// </summary>
        public bool IsValid
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Version.IsValid;
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
        public static implicit operator ulong(SlotMapKey64 value)
            => value._raw;

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
