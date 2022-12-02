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
            private long _denseIndex;

            public Enumerator(SparseSlotMap<TValue> slotmap)
            {
                _slotmap = slotmap;
                _version = slotmap._version;
                _current = default;
                _denseIndex = 0;
            }
            
            public KeyValuePair<SlotKey, TValue> Current
            {
                get
                {
                    if (_denseIndex == 0)
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
                    if (_denseIndex == 0)
                    {
                        ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumOpCantHappen();
                    }

                    return _current;
                }
            }

            public bool MoveNext()
            {
                var slotmap = _slotmap;
                var lastDenseIndex = slotmap._lastDenseIndex;

                if (_version == slotmap._version
                    && lastDenseIndex >= 0
                    && _denseIndex <= lastDenseIndex
                )
                {
                    var pageSize = slotmap._pageSize;
                    var denseAddress = SlotAddress.FromIndex(_denseIndex, pageSize);
                    var densePage = slotmap._densePages[denseAddress.PageIndex];
                    var sparseIndex = densePage._sparseIndices[denseAddress.SlotIndex];
                    var sparseAddress = SlotAddress.FromIndex(sparseIndex, pageSize);
                    var sparsePage = slotmap._sparsePages[sparseAddress.PageIndex];
                    ref var meta = ref sparsePage._metas[sparseAddress.SlotIndex];

                    _current = new(new(sparseIndex, meta.Version), densePage._values[denseAddress.SlotIndex]);
                    _denseIndex++;
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

                _denseIndex = _slotmap._lastDenseIndex + 1;
                _current = default;
                return false;
            }

            public void Reset()
            {
                _current = default;
                _denseIndex = 0;
            }

            public void Dispose() { }
        }
    }
}