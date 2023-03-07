#if __SLOTMAP_UNITY_COLLECTIONS__

using System.Diagnostics;
using Unity.Collections;

namespace Collections.Extensions.SlotMaps
{
    public static class NativeChecks
    {
#if DISABLE_SLOTMAP_CHECKS
        [Conditional("__SLOTMAP_CHECKS_NEVER_DEFINED__")]
#endif
        [StackTraceHidden]
        public static void Require(
              bool assertion
            , in FixedString128Bytes message
        )
        {
            if (assertion == false)
            {
                throw new SlotMapException(message);
            }
        }

#if DISABLE_SLOTMAP_CHECKS
        [Conditional("__SLOTMAP_CHECKS_NEVER_DEFINED__")]
#endif
        [StackTraceHidden]
        public static void Require(
              bool assertion
            , in FixedString128Bytes message
            , System.Exception inner
        )
        {
            if (assertion == false)
            {
                throw new SlotMapException(message, inner);
            }
        }

#if DISABLE_SLOTMAP_CHECKS
        [Conditional("__SLOTMAP_CHECKS_NEVER_DEFINED__")]
#endif
        [StackTraceHidden]
        public static void Warning(
              bool assertion
            , in FixedString128Bytes message
        )
        {
#if __SLOTMAP_UNITY_LOGGING__
            if (assertion == false)
                Unity.Logging.Log.Warning(message);
#else
            if (assertion == false)
                UnityEngine.Debug.LogWarning(message);
#endif
        }

#if DISABLE_SLOTMAP_CHECKS
        [Conditional("__SLOTMAP_CHECKS_NEVER_DEFINED__")]
#endif
        [StackTraceHidden]
        public static void RequireOrWarning(
              bool required
            , bool assertion
            , in FixedString128Bytes message
        )
        {
            if (assertion == false)
            {
                if (required)
                {
                    throw new SlotMapException(message);
                }
                else
                {
#if __SLOTMAP_UNITY_LOGGING__
                    Unity.Logging.Log.Warning(message);
#else
                    UnityEngine.Debug.LogWarning(message);
#endif
                }
            }
        }

#if DISABLE_SLOTMAP_CHECKS
        [Conditional("__SLOTMAP_CHECKS_NEVER_DEFINED__")]
#endif
        [StackTraceHidden]
        public static void RequireOrWarning(
              bool required
            , bool assertion
            , in FixedString128Bytes message
            , System.Exception inner
        )
        {
            if (assertion == false)
            {
                if (required)
                {
                    throw new SlotMapException(message, inner);
                }
                else
                {
#if __SLOTMAP_UNITY_LOGGING__
                    Unity.Logging.Log.Warning(message);
#else
                    UnityEngine.Debug.LogWarning(message);
#endif
                }
            }
        }
    }
}

#endif
