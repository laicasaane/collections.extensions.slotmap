using System.Collections;
using System.Collections.Generic;

namespace Collections.Extensions.SlotMaps
{
    partial class SlotMap<T>
    {
        public struct Enumerator : IEnumerator<KeyValuePair<SlotKey, T>>
        {
            private readonly SlotMap<T> _slotmap;
            private readonly int _version;
            private KeyValuePair<SlotKey, T> _current;
            private uint _pageIndex;
            private uint _itemIndex;

            public Enumerator(SlotMap<T> slotmap)
            {
                _slotmap = slotmap;
                _version = slotmap._version;
                _current = default;
                _pageIndex = 0;
                _itemIndex = 0;
            }

            public KeyValuePair<SlotKey, T> Current
            {
                get
                {
                    if (_pageIndex == 0 && _itemIndex == 0)
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
                    if (_pageIndex == 0 && _itemIndex == 0)
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
                var itemIndex = _itemIndex;

                for (var iPage = _pageIndex; iPage < pagesLength; iPage++)
                {
                    ref var page = ref pages[iPage];
                    var metas = page._metas;
                    var items = page._items;

                    for (var iItem = itemIndex; iItem < pageSize; iItem++)
                    {
                        var meta = metas[iItem];

                        if (meta.IsValid && meta.State == SlotState.Occupied)
                        {
                            var address = new SlotAddress(iPage, iItem);
                            var index = address.ToIndex(pageSize);
                            _current = new(new(index, meta.Version), items[iItem]);
                            _pageIndex = iPage;
                            _itemIndex = iItem + 1;
                            return true;
                        }
                    }

                    itemIndex = 0;
                }

                _pageIndex = pagesLength + 1;
                _itemIndex = pageSize + 1;
                _current = default;
                return false;
            }

            public void Reset()
            {
                _current = default;
                _pageIndex = 0;
                _itemIndex = 0;
            }

            public void Dispose() { }
        }
    }
}