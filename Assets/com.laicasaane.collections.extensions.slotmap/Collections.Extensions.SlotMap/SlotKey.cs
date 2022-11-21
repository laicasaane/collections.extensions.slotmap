using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Collections.Extensions.SlotMap
{
    /// <summary>
    /// Represents a key of the <see cref="SlotMap{T}"/> data structure.
    /// </summary>
    /// <remarks>If version equals to zero (0), it is an invalid key.</remarks>
    [StructLayout(LayoutKind.Explicit)]
    public readonly struct SlotKey : IEquatable<SlotKey>
    {
        public static readonly SlotKey InvalidValue = default;
        public static readonly SlotKey MinValue = new(0, SlotVersion.MinValue);
        public static readonly SlotKey MaxValue = new(0, SlotVersion.MaxValue);

        [FieldOffset(0)]
        private readonly ulong _raw;

        [FieldOffset(0)]
        private readonly uint _index;

        [FieldOffset(4)]
        private readonly SlotVersion _version;

        [FieldOffset(6)]
        private readonly ushort _tag;

        public SlotKey(uint index) : this()
        {
            _index = index;
            _version = SlotVersion.MinValue;
        }

        public SlotKey(uint index, ushort tag) : this()
        {
            _index = index;
            _version = SlotVersion.MinValue;
            _tag = tag;
        }

        public SlotKey(uint index, SlotVersion version) : this()
        {
            Checks.Require(version.IsValid, $"`{nameof(version)}` is invalid.");

            _index = index;
            _version = version;
        }

        public SlotKey(uint index, SlotVersion version, ushort tag) : this()
        {
            Checks.Require(version.IsValid, $"`{nameof(version)}` is invalid.");

            _index = index;
            _version = version;
            _tag = tag;
        }

        public ulong Raw
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _raw;
        }

        public uint Index
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _index;
        }

        public SlotVersion Version
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _version;
        }

        public ushort Tag
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _tag;
        }

        /// <summary>
        /// The key is only valid if its <see cref="_version"/> is valid.
        /// </summary>
        public bool IsValid
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _version.IsValid;
        }

#if DISABLE_SLOTMAP_CHECKS
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public SlotKey WithVersion(SlotVersion version)
        {
            Checks.Require(version.IsValid, $"`{nameof(version)}` is invalid.");

            return new(_index, version, _tag);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SlotKey WithTag(ushort tag)
            => new(_index, _version, tag);

        public void Deconstruct(out uint index, out SlotVersion version, out ushort tag)
        {
            index = _index;
            version = _version;
            tag = _tag;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(SlotKey other)
            => _raw == other._raw;

        public override bool Equals(object obj)
            => obj is SlotKey other && _raw == other._raw;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
            => _raw.GetHashCode();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ulong(SlotKey value)
            => value._raw;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(SlotKey lhs, SlotKey rhs)
            => lhs._raw == rhs._raw;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(SlotKey lhs, SlotKey rhs)
            => lhs._raw != rhs._raw;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator <(SlotKey lhs, SlotKey rhs)
            => lhs._raw < rhs._raw;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator >(SlotKey lhs, SlotKey rhs)
            => lhs._raw > rhs._raw;
    }
}
