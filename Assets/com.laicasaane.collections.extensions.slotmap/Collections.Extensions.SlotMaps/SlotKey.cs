using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Collections.Extensions.SlotMaps
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

#if ENABLE_SLOTMAP_KEY_TAG
        [FieldOffset(0)]
        private readonly ushort _tag;
#endif

#if ENABLE_SLOTMAP_KEY_TAG
        [FieldOffset(2)]
#else
        [FieldOffset(0)]
#endif
        private readonly SlotVersion _version;

        [FieldOffset(4)]
        private readonly uint _index;

        public SlotKey(uint index) : this()
        {
            _index = index;
            _version = SlotVersion.MinValue;
        }

#if ENABLE_SLOTMAP_KEY_TAG
        public SlotKey(uint index, ushort tag) : this()
        {
            _index = index;
            _version = SlotVersion.MinValue;
            _tag = tag;
        }
#endif

        public SlotKey(uint index, SlotVersion version) : this()
        {
            Checks.Require(version.IsValid, $"`{nameof(version)}` is invalid.");

            _index = index;
            _version = version;
        }

#if ENABLE_SLOTMAP_KEY_TAG
        public SlotKey(uint index, SlotVersion version, ushort tag) : this()
        {
            Checks.Require(version.IsValid, $"`{nameof(version)}` is invalid.");

            _index = index;
            _version = version;
            _tag = tag;
        }
#endif

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

#if ENABLE_SLOTMAP_KEY_TAG
        public ushort Tag
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _tag;
        }
#endif

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

#if ENABLE_SLOTMAP_KEY_TAG
            return new(_index, version, _tag);
#else
            return new(_index, version);
#endif
        }

#if ENABLE_SLOTMAP_KEY_TAG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SlotKey WithTag(ushort tag)
            => new(_index, _version, tag);

        public void Deconstruct(out uint index, out SlotVersion version, out ushort tag)
        {
            index = _index;
            version = _version;
            tag = _tag;
        }
#endif

        public void Deconstruct(out uint index, out SlotVersion version)
        {
            index = _index;
            version = _version;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(SlotKey other)
            => _raw == other._raw;

        public override bool Equals(object obj)
            => obj is SlotKey other && _raw == other._raw;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
            => _raw.GetHashCode();

#if ENABLE_SLOTMAP_KEY_TAG
        public override string ToString()
            => $"({_index}, {_version}, {_tag})";
#else
        public override string ToString()
            => $"({_index}, {_version})";
#endif

        public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider provider = null)
        {
            var openQuoteCharsWritten = 0;
            destination[openQuoteCharsWritten++] = '(';

            destination = destination[openQuoteCharsWritten..];

            if (_index.TryFormat(destination, out var indexCharsWritten, format, provider) == false)
            {
                charsWritten = 0;
                return false;
            }

            destination[indexCharsWritten++] = ',';
            destination[indexCharsWritten++] = ' ';

            destination = destination[indexCharsWritten..];

            if (_version.TryFormat(destination, out var versionCharsWritten, format, provider) == false)
            {
                charsWritten = 0;
                return false;
            }

#if ENABLE_SLOTMAP_KEY_TAG

            destination[versionCharsWritten++] = ',';
            destination[versionCharsWritten++] = ' ';

            destination = destination[versionCharsWritten..];

            if (_tag.TryFormat(destination, out var tagCharsWritten, format, provider) == false)
            {
                charsWritten = 0;
                return false;
            }

            destination[tagCharsWritten++] = ')';

            charsWritten = openQuoteCharsWritten + indexCharsWritten + versionCharsWritten + tagCharsWritten;

#else

            destination[versionCharsWritten++] = ')';

            charsWritten = openQuoteCharsWritten + indexCharsWritten + versionCharsWritten;

#endif

            return true;
        }

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
