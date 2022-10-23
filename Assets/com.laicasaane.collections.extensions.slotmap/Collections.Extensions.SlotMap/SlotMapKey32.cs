using System;
using System.Runtime.CompilerServices;

namespace Collections.Extensions.SlotMap
{
    using id_type = UInt32;
    using version_t = UInt16;
    using index_t = UInt32;
    using tag_t = Byte;

    /// <summary>
    /// Represents a 32-bit key of the SlotMap32 data structure.
    /// </summary>
    public partial struct SlotMapKey32 : IEquatable<SlotMapKey32>
    {
        private const uint INVALID = 0x_00_00_00_00;

        public static readonly SlotMapKey32 Invalid = default;
        public static readonly SlotMapKey32 MinValue = new(KeyIndex.MinValue, KeyVersion.MinValue);
        public static readonly SlotMapKey32 MaxValue = new(KeyIndex.MaxValue, KeyVersion.MaxValue);

        private uint _raw;

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
            get => _raw != INVALID;
        }

        public KeyIndex Index
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => KeyIndex.ToIndex(_raw);
        }

        public KeyVersion Version
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => KeyVersion.ToVersion(_raw);
        }

        public KeyTag Tag
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => KeyTag.ToTag(_raw);
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
