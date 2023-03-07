#if __SLOTMAP_UNITY_COLLECTIONS__

using System.Collections;
using System.Collections.Generic;

namespace Collections.Extensions.SlotMaps
{
    partial struct NativeSlotMap<TValue>
    {
        public struct Enumerator : IEnumerator<KeyValuePair<SlotKey, TValue>>
        {
            private readonly NativeSlotMap<TValue> _slotmap;
            private readonly int _version;

            private KeyValuePair<SlotKey, TValue> _current;
            private uint _slotIndex;

            public Enumerator(ref NativeSlotMap<TValue> slotmap)
            {
                _slotmap = slotmap;
                _version = slotmap._version.IsCreated ? slotmap._version.Value : -1;
                _current = default;
                _slotIndex = 0;
            }

            public KeyValuePair<SlotKey, TValue> Current
            {
                get
                {
                    if (_slotIndex == 0)
                    {
                        ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumOpCantHappen();
                    }

                    return _current;
                }
            }

            object IEnumerator.Current
            {
                get
                {
                    if (_slotIndex == 0)
                    {
                        ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumOpCantHappen();
                    }

                    return _current;
                }
            }

            public bool MoveNext()
            {
                ref readonly var slotmap = ref _slotmap;
                ref readonly var version = ref slotmap._version;

                if (version.IsCreated == false || _version != version.Value)
                {
                    ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumFailedVersion();
                }

                ref readonly var metas = ref slotmap._metas;
                ref readonly var values = ref slotmap._values;
                var length = metas.Length;

                for (var i = _slotIndex; i < length; i++)
                {
                    var meta = metas[(int)i];

                    if (meta.IsValid && meta.State == SlotState.Occupied)
                    {
                        _current = new KeyValuePair<SlotKey, TValue>(new SlotKey(i, meta.Version), values[(int)i]);
                        _slotIndex = i + 1;
                        return true;
                    }
                }

                _slotIndex = (uint)length + 1;
                _current = default;
                return false;
            }

            public void Reset()
            {
                _current = default;
                _slotIndex = 0;
            }

            public void Dispose() { }
        }
    }
}

#endif
