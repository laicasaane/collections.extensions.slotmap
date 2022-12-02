using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Collections.Extensions.SlotMaps
{
    [StackTraceHidden]
    internal static class ThrowHelper
    {
        [DoesNotReturn]
        internal static void ThrowInvalidOperationException_InvalidOperation_EnumFailedVersion()
        {
            throw new InvalidOperationException(SR.InvalidOperation_EnumFailedVersion);
        }

        [DoesNotReturn]
        internal static void ThrowInvalidOperationException_InvalidOperation_EnumOpCantHappen()
        {
            throw new InvalidOperationException(SR.InvalidOperation_EnumOpCantHappen);
        }

#pragma warning disable IDE1006 // Naming Styles
        private static class SR
        {
            public const string InvalidOperation_EnumFailedVersion
                = "Collection was modified after the enumerator was instantiated.";

            public const string InvalidOperation_EnumOpCantHappen
                = "Enumeration has either not started or has already finished.";
        }
#pragma warning restore IDE1006 // Naming Styles
    }
}
