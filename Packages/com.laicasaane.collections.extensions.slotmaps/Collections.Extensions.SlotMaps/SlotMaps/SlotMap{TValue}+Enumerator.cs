using System.Collections;
using System.Collections.Generic;

namespace Collections.Extensions.SlotMaps
{
    partial class SlotMap<TValue>
    {
        public struct Enumerator : IEnumerator<KeyValuePair<SlotKey, TValue>>
        {
            private readonly SlotMap<TValue> _slotmap;
            private readonly int _version;
            private KeyValuePair<SlotKey, TValue> _current;
            private uint _pageIndex;
            private uint _slotIndex;

            public Enumerator(SlotMap<TValue> slotmap)
            {
                _slotmap = slotmap;
                _version = slotmap._version;
                _current = default;
                _pageIndex = 0;
                _slotIndex = 0;
            }

            public KeyValuePair<SlotKey, TValue> Current
            {
                get
                {
                    if (_pageIndex == 0 && _slotIndex == 0)
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
                    if (_pageIndex == 0 && _slotIndex == 0)
                    {
                        ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumOpCantHappen();
                    }

                    return _current;
                }
            }

            public bool MoveNext()
            {
                var slotmap = _slotmap;

                if (_version != slotmap._version)
                {
                    ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumFailedVersion();
                }

                var pages = slotmap._pages;
                var pagesLength = (uint)pages.Length;
                var pageSize = slotmap._pageSize;
                var slotIndex = _slotIndex;

                for (var iPage = _pageIndex; iPage < pagesLength; iPage++)
                {
                    ref var page = ref pages[iPage];
                    var metas = page._metas;
                    var values = page._values;

                    for (var iValue = slotIndex; iValue < pageSize; iValue++)
                    {
                        var meta = metas[iValue];

                        if (meta.IsValid && meta.State == SlotState.Occupied)
                        {
                            var address = new PagedAddress(iPage, iValue);
                            var index = address.ToIndex(pageSize);
                            _current = new(new(index, meta.Version), values[iValue]);
                            _pageIndex = iPage;
                            _slotIndex = iValue + 1;
                            return true;
                        }
                    }

                    slotIndex = 0;
                }

                _pageIndex = pagesLength + 1;
                _slotIndex = pageSize + 1;
                _current = default;
                return false;
            }

            public void Reset()
            {
                _current = default;
                _pageIndex = 0;
                _slotIndex = 0;
            }

            public void Dispose() { }
        }
    }
}