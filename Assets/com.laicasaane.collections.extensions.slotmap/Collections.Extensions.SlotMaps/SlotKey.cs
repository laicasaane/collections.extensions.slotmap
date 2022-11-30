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

        [FieldOffset(0)]
        private readonly SlotVersion _version;

        [FieldOffset(4)]
        private readonly uint _index;

        public SlotKey(uint index) : this()
        {
            _index = index;
            _version = SlotVersion.MinValue;
        }

        public SlotKey(uint index, SlotVersion version) : this()
        {
            Checks.Require(version.IsValid, $"`{nameof(version)}` is invalid.");

            _index = index;
            _version = version;
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

            return new(_index, version);
        }

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

        public override string ToString()
            => $"({_index}, {_version})";

        public bool TryFormat(
              Span<char> destination
            , out int charsWritten
            , ReadOnlySpan<char> format = default
            , IFormatProvider provider = null
        )
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

            destination[versionCharsWritten++] = ')';

            charsWritten = openQuoteCharsWritten + indexCharsWritten + versionCharsWritten;

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
