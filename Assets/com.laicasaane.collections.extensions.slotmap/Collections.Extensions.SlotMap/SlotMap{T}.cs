using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Collections.Extensions.SlotMap
{
    public partial class SlotMap<T>
    {
        private static readonly string s_name = $"{nameof(SlotMap<T>)}<{typeof(T).Name}>";
        private const uint MAX_VALID_INDEX = uint.MaxValue;

        private readonly uint _pageSize;
        private readonly uint _freeIndicesLimit;
        private readonly uint _maxPageCount;

        private readonly Queue<SlotKey> _freeKeys = new();

        private Page[] _pages = Array.Empty<Page>();

        public SlotMap(uint pageSize = 1024, uint freeIndicesLimit = 32)
        {
            Checks.Require(IsPowerOfTwo(pageSize), $"`{nameof(pageSize)}` must be a power of two. Argument value: {pageSize}.");
            Checks.Suggest(freeIndicesLimit <= pageSize, $"`{nameof(freeIndicesLimit)}` should be lesser than or equal to `{nameof(pageSize)}: {pageSize}`, or it would be clamped to `{nameof(pageSize)}`. Argument value: {freeIndicesLimit}.");

            _pageSize = pageSize;
            _freeIndicesLimit = Math.Clamp(freeIndicesLimit, 0, pageSize);
            _maxPageCount = GetMaxPageCount(MAX_VALID_INDEX, pageSize);

            MakeNewPage();
        }

        public bool TryAdd(T item, out SlotKey key)
        {
            if (TryMakeNewKey(out key, out var address) == false)
            {
                Checks.Suggest(false, $"Cannot add more item to {s_name}>");
                return false;
            }

            _pages[address.PageIndex].Add(address.ItemIndex, item);
            return true;
        }

        private bool TryMakeNewKey(out SlotKey key, out Address address)
        {
            var freeKeys = _freeKeys;

            if (freeKeys.Count <= _freeIndicesLimit)
            {
                key = freeKeys.Dequeue();
                address = GetAddress(key.Index, _pageSize);
                return true;
            }

            var pages = _pages;
            var pageCount = (uint)pages.LongLength;
            var lastPageIndex = pageCount - 1;
            var lastPageCount = pages[lastPageIndex].Count;

            if (lastPageCount >= _pageSize)
            {
                if (pageCount >= _maxPageCount)
                {
                    key = default;
                    address = default;
                    return false;
                }

                MakeNewPage();

                lastPageIndex += 1;
                lastPageCount = 0;
            }

            address = new(lastPageIndex, lastPageCount);
            key = new SlotKey(ToIndex(address, _pageSize));
            return true;
        }

        private void MakeNewPage()
        {
            var oldPages = _pages;
            var oldLength = oldPages.Length;

            if (oldLength >= _maxPageCount)
            {
                Checks.Suggest(false, $"Cannot add more page to {s_name}: the limit of {_maxPageCount} pages has been reached.");
                return;
            }

            var newPages = new Page[oldLength + 1];

            if (oldLength > 0)
            {
                Array.Copy(oldPages, newPages, oldLength);
            }

            newPages[oldLength] = new Page(_pageSize);
            _pages = newPages;

            if (oldLength > 0)
            {
                Array.Clear(oldPages, 0, oldLength);
            }
        }

        private void DeletePage(uint pageIndex)
        {
            var oldPages = _pages;
            var oldLength = oldPages.Length;

            if (oldLength < 1)
            {
                Checks.Suggest(false, $"Cannot delete page from SlotMap: 0 page count.");
                return;
            }


        }

        private static uint GetMaxPageCount(uint maxIndex, uint pageSize)
        {
            var result = maxIndex / pageSize;
            return (maxIndex % pageSize == 0) ? result : result + 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsPowerOfTwo(uint x)
            => (x != 0) && ((x & (x - 1)) == 0);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Address GetAddress(uint index, uint pageSize)
            => new(index / pageSize, index % pageSize);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ToIndex(Address address, uint pageSize)
            => (address.PageIndex * pageSize) + address.ItemIndex;
    }
}
