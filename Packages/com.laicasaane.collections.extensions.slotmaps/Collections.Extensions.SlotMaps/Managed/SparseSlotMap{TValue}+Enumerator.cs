using System.Collections;
using System.Collections.Generic;

namespace Collections.Extensions.SlotMaps
{
    partial class SparseSlotMap<TValue>
    {
        public struct Enumerator : IEnumerator<KeyValuePair<SlotKey, TValue>>
        {
            private readonly SparseSlotMap<TValue> _slotmap;
            private readonly int _version;
            private KeyValuePair<SlotKey, TValue> _current;
            private long _valueIndex;

            public Enumerator(SparseSlotMap<TValue> slotmap)
            {
                _slotmap = slotmap;
                _version = slotmap._version;
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
                var slotmap = _slotmap;
                var lastValueIndex = slotmap._lastValueIndex;

                if (_version == slotmap._version
                    && lastValueIndex >= 0
                    && _valueIndex <= lastValueIndex
                )
                {
                    var pageSize = slotmap._pageSize;
                    var valueAddress = PagedAddress.FromIndex(_valueIndex, pageSize);
                    var valuePage = slotmap._valuePages[valueAddress.PageIndex];
                    var metaIndex = valuePage._metaIndices[valueAddress.SlotIndex];
                    var metaAddress = PagedAddress.FromIndex(metaIndex, pageSize);
                    var metaPage = slotmap._metaPages[metaAddress.PageIndex];
                    ref var meta = ref metaPage._metas[metaAddress.SlotIndex];

                    _current = new(new(metaIndex, meta.Version), valuePage._values[valueAddress.SlotIndex]);
                    _valueIndex++;
                    return true;
                }

                return MoveNextRare();
            }

            private bool MoveNextRare()
            {
                if (_version != _slotmap._version)
                {
                    ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumFailedVersion();
                }

                _valueIndex = _slotmap._lastValueIndex + 1;
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