#if UNITY_2021_1_OR_NEWER
#define __UNITY_ENGINE__
#endif

using System;

namespace Collections.Extensions.SlotMap
{
    internal static class Checks
    {
#if DISABLE_SLOTMAP_CHECKS
        [Conditional("__SLOTMAP_CHECKS_NEVER_DEFINED__")]
#endif
        public static void Require(bool assertion, string message)
        {
            if (assertion == false)
                throw new SlotMapException(message);
        }

#if DISABLE_SLOTMAP_CHECKS
        [Conditional("__SLOTMAP_CHECKS_NEVER_DEFINED__")]
#endif
        public static void Require(bool assertion, string message, System.Exception inner)
        {
            if (assertion == false)
                throw new SlotMapException(message, inner);
        }

#if DISABLE_SLOTMAP_CHECKS
        [Conditional("__SLOTMAP_CHECKS_NEVER_DEFINED__")]
#endif
        public static void Suggest(bool assertion, string message)
        {
#if __UNITY_ENGINE__
            if (assertion == false)
                UnityEngine.Debug.LogWarning(message);
#else
            if (assertion == false)
                throw new SlotMapException(message);
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
