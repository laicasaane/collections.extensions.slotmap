#if UNITY_2021_1_OR_NEWER
#define __UNITY_ENGINE__
#endif

using System;
using System.Diagnostics;

namespace Collections.Extensions.SlotMaps
{
    internal static class Checks
    {
#if DISABLE_SLOTMAP_CHECKS
        [Conditional("__SLOTMAP_CHECKS_NEVER_DEFINED__")]
#endif
        [StackTraceHidden]
        public static void Require(bool assertion, string message)
        {
            if (assertion == false)
                throw new SlotMapException(message);
        }

#if DISABLE_SLOTMAP_CHECKS
        [Conditional("__SLOTMAP_CHECKS_NEVER_DEFINED__")]
#endif
        [StackTraceHidden]
        public static void Require(bool assertion, string message, System.Exception inner)
        {
            if (assertion == false)
                throw new SlotMapException(message, inner);
        }

#if DISABLE_SLOTMAP_CHECKS
        [Conditional("__SLOTMAP_CHECKS_NEVER_DEFINED__")]
#endif
        [StackTraceHidden]
        public static void Warning(bool assertion, string message)
        {
#if __UNITY_ENGINE__
            if (assertion == false)
                UnityEngine.Debug.LogWarning(message);
#else
            if (assertion == false)
                System.Diagnostics.Debug.WriteLine(message);
#endif
        }

#if DISABLE_SLOTMAP_CHECKS
        [Conditional("__SLOTMAP_CHECKS_NEVER_DEFINED__")]
#endif
        [StackTraceHidden]
        public static void RequireOrWarning(bool required, bool assertion, string message)
        {
#if __UNITY_ENGINE__
            if (assertion == false)
            {
                if (required)
                    throw new SlotMapException(message);
                else
                    UnityEngine.Debug.LogWarning(message);
            }
#else
            if (assertion == false)
                System.Diagnostics.Debug.WriteLine(message);
#endif
        }

#if DISABLE_SLOTMAP_CHECKS
        [Conditional("__SLOTMAP_CHECKS_NEVER_DEFINED__")]
#endif
        [StackTraceHidden]
        public static void RequireOrWarning(bool required, bool assertion, string message, System.Exception inner)
        {
#if __UNITY_ENGINE__
            if (assertion == false)
            {
                if (required)
                    throw new SlotMapException(message, inner);
                else
                    UnityEngine.Debug.LogWarning(message);
            }
#else
            if (assertion == false)
                System.Diagnostics.Debug.WriteLine(message);
#endif
        }
    }

    public class SlotMapException : Exception
    {
        public SlotMapException()
        {
        }

        public SlotMapException(string message) : base(message)
        {
        }

        public SlotMapException(string message, System.Exception inner) : base(message, inner)
        {
        }
    }
}
