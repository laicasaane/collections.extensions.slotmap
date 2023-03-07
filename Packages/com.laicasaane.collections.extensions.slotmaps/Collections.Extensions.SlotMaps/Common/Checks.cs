#if UNITY_2021_1_OR_NEWER
#define __UNITY_ENGINE__
#endif

#if __SLOTMAP_UNITY_BURST__
#if __SLOTMAP_UNITY_COLLECTIONS__
#if __SLOTMAP_UNITY_LOGGING__
#define __USE_UNITY_LOGGING__
#endif
#endif
#endif

using System;
using System.Diagnostics;

#if __USE_UNITY_LOGGING__
using Unity.Collections;
#endif

namespace Collections.Extensions.SlotMaps
{
    public static class Checks
    {
#if DISABLE_SLOTMAP_CHECKS
        [Conditional("__SLOTMAP_CHECKS_NEVER_DEFINED__")]
#endif
        [StackTraceHidden]
        public static void Require(
              bool assertion
#if __USE_UNITY_LOGGING__
            , in FixedString128Bytes message
#else
            , string message
#endif
        )
        {
            if (assertion == false)
            {
#if __USE_UNITY_LOGGING__
                throw new SlotMapException(message.ToString());
#else
                throw new SlotMapException(message);
#endif
            }
        }

#if DISABLE_SLOTMAP_CHECKS
        [Conditional("__SLOTMAP_CHECKS_NEVER_DEFINED__")]
#endif
        [StackTraceHidden]
        public static void Require(
              bool assertion
#if __USE_UNITY_LOGGING__
            , in FixedString128Bytes message
#else
            , string message
#endif
            , System.Exception inner
        )
        {
            if (assertion == false)
            {
#if __USE_UNITY_LOGGING__
                throw new SlotMapException(message.ToString(), inner);
#else
                throw new SlotMapException(message, inner);
#endif
            }
        }

#if DISABLE_SLOTMAP_CHECKS
        [Conditional("__SLOTMAP_CHECKS_NEVER_DEFINED__")]
#endif
        [StackTraceHidden]
        public static void Warning(
              bool assertion
#if __USE_UNITY_LOGGING__
            , in FixedString128Bytes message
#else
            , string message
#endif
        )
        {
#if __UNITY_ENGINE__
#if __USE_UNITY_LOGGING__
            if (assertion == false)
                Unity.Logging.Log.Warning(message);
#else
            if (assertion == false)
                UnityEngine.Debug.LogWarning(message);
#endif
#else
            if (assertion == false)
                System.Diagnostics.Debug.WriteLine(message);
#endif
        }

#if DISABLE_SLOTMAP_CHECKS
        [Conditional("__SLOTMAP_CHECKS_NEVER_DEFINED__")]
#endif
        [StackTraceHidden]
        public static void RequireOrWarning(
              bool required
            , bool assertion
#if __USE_UNITY_LOGGING__
            , in FixedString128Bytes message
#else
            , string message
#endif
        )
        {
#if __UNITY_ENGINE__
            if (assertion == false)
            {
                if (required)
                {
#if __USE_UNITY_LOGGING__
                    throw new SlotMapException(message.ToString());
#else
                    throw new SlotMapException(message);
#endif
                }
                else
                {
#if __USE_UNITY_LOGGING__
                    Unity.Logging.Log.Warning(message);
#else
                    UnityEngine.Debug.LogWarning(message);
#endif
                }
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
        public static void RequireOrWarning(
              bool required
            , bool assertion
#if __USE_UNITY_LOGGING__
            , in FixedString128Bytes message
#else
            , string message
#endif
            , System.Exception inner
        )
        {
#if __UNITY_ENGINE__
            if (assertion == false)
            {
                if (required)
                {
#if __USE_UNITY_LOGGING__
                    throw new SlotMapException(message.ToString(), inner);
#else
                    throw new SlotMapException(message, inner);
#endif
                }
                else
                {
#if __USE_UNITY_LOGGING__
                    Unity.Logging.Log.Warning(message);
#else
                    UnityEngine.Debug.LogWarning(message);
#endif
                }
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
