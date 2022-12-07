using System;
using System.Runtime.CompilerServices;

namespace Collections.Extensions.SlotMaps
{
    public readonly partial struct SlotMeta : IEquatable<SlotMeta>
    {
        public static readonly SlotMeta InvalidValue = default;
        public static readonly SlotMeta MinValue = new(SlotVersion.MinValue, SlotState.Empty);
        public static readonly SlotMeta MaxValue = new(SlotVersion.MaxValue, SlotState.Empty);

        private readonly uint _raw;

        public SlotMeta(SlotVersion version, SlotState state)
        {
            _raw = state.ShiftToUInt32() | version;
        }

        public SlotMeta(SlotMeta meta, SlotVersion version)
        {
            _raw = meta.State.ShiftToUInt32() | version;
        }

        public SlotMeta(SlotMeta meta, SlotState state)
        {
            _raw = state.ShiftToUInt32() | meta.Version;
        }

        public bool IsValid
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Version.IsValid;
        }

        public SlotVersion Version
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => SlotVersion.Convert(_raw);
        }

        public SlotState State
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => SlotState.Convert(_raw);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(SlotMeta other)
            => _raw == other._raw;

        public override bool Equals(object obj)
            => obj is SlotMeta other && _raw == other._raw;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
            => _raw.GetHashCode();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString()
            => $"({Version}, {State})";

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

            if (Version.TryFormat(destination, out var indexCharsWritten, format, provider) == false)
            {
                charsWritten = 0;
                return false;
            }

            destination[indexCharsWritten++] = ',';
            destination[indexCharsWritten++] = ' ';

            destination = destination[indexCharsWritten..];

            if (State.TryFormat(destination, out var versionCharsWritten, format, provider) == false)
            {
                charsWritten = 0;
                return false;
            }

            destination[versionCharsWritten++] = ')';

            charsWritten = openQuoteCharsWritten + indexCharsWritten + versionCharsWritten;

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(SlotMeta lhs, SlotMeta rhs)
            => lhs._raw == rhs._raw;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(SlotMeta lhs, SlotMeta rhs)
            => lhs._raw != rhs._raw;
    }
}
