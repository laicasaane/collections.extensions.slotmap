#if __SLOTMAP_UNITY_COLLECTIONS__

using System.Collections;
using System.Collections.Generic;

namespace Collections.Extensions.SlotMaps
{
    partial struct NativeSparseSlotMap<TValue>
    {
        public struct Enumerator : IEnumerator<KeyValuePair<SlotKey, TValue>>
        {
            private readonly NativeSparseSlotMap<TValue> _slotmap;
            private readonly int _version;

            private KeyValuePair<SlotKey, TValue> _current;
            private int _valueIndex;

            public Enumerator(ref NativeSparseSlotMap<TValue> slotmap)
            {
                _slotmap = slotmap;
                _version = slotmap._version.IsCreated ? slotmap._version.Value : -1;
                _current = default;
                _valueIndex = 0;
            }

            public KeyValuePair<SlotKey, TValue> Current
            {
                get
                {
                    if (_valueIndex == 0)
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
                    if (_valueIndex == 0)
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
                var lastValueIndex = slotmap._lastValueIndex.IsCreated ? slotmap._lastValueIndex.Value : -1;
                var valueIndex = _valueIndex;

                if (version.IsCreated && _version == version.Value
                    && lastValueIndex >= 0
                    && valueIndex <= lastValueIndex
                )
                {
                    var metaIndex = slotmap._metaIndices[valueIndex];
                    var meta = slotmap._metas[metaIndex];

                    _current = new(new((uint)metaIndex, meta.Version), slotmap.Values[valueIndex]);
                    _valueIndex++;
                    return true;
                }

                return MoveNextRare(
                      version.IsCreated ? version.Value : null
                    , lastValueIndex
                );
            }

            private bool MoveNextRare(in int? version, int lastValue)
            {
                if (version.HasValue == false || _version != version.Value)
                {
                    ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumFailedVersion();
                }

                _valueIndex = lastValue + 1;
                _current = default;
                return false;
            }

            public void Reset()
            {
                _current = default;
                _valueIndex = 0;
            }

            public void Dispose() { }
        }
    }
}

#endif
