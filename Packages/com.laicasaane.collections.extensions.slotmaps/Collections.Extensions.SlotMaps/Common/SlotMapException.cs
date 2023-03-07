using System;

#if __SLOTMAP_UNITY_COLLECTIONS__
using Unity.Collections;
#endif

namespace Collections.Extensions.SlotMaps
{
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

#if __SLOTMAP_UNITY_COLLECTIONS__
        public SlotMapException(FixedString128Bytes message) : base(message.ToString())
        {
        }

        public SlotMapException(FixedString128Bytes message, System.Exception inner) : base(message.ToString(), inner)
        {
        }
#endif
    }
}
