﻿using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Collections.Extensions.SlotMap
{
    public partial class SlotMap<T>
    {
        private static readonly string s_name = $"{nameof(SlotMap<T>)}<{typeof(T).Name}>";
        private static readonly bool s_itemIsUnmanaged = RuntimeHelpers.IsReferenceOrContainsReferences<T>();

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

        public T Get(SlotKey key)
        {
            ref var page = ref GetPage(_pages, _pageSize, key, out var itemIndex);
            return page.Get(itemIndex, key.Version);
        }

        public ref readonly T GetRef(SlotKey key)
        {
            ref var page = ref GetPage(_pages, _pageSize, key, out var itemIndex);
            return ref page.GetRef(itemIndex, key.Version);
        }

        public bool TryGet(SlotKey key, out T item)
        {
            ref var page = ref GetPage(_pages, _pageSize, key, out var itemIndex);
            return page.TryGet(itemIndex, key.Version, out item);
        }

        public SlotKey Add(T item)
        {
            if (TryAdd(item, out var key))
            {
                return key;
            }

            throw new SlotMapException($"Cannot add `{nameof(item)}` to {s_name}. Argument value: {item}.");
        }

        public bool TryAdd(T item, out SlotKey key)
        {
            if (TryGetNewKey(out key, out var address) == false)
            {
                Checks.Suggest(false, $"Cannot add `{nameof(item)}` to {s_name}. Argument value: {item}.");
                return false;
            }

            ref var page = ref _pages[address.PageIndex];
            return page.TryAdd(address.ItemIndex, key.Version, item);
        }

        public SlotKey Replace(SlotKey key, T item)
        {
            ref var page = ref GetPage(_pages, _pageSize, key, out var itemIndex);

            if (page.TryReplace(itemIndex, key.Version, item, out var newVersion))
            {
                return key.WithVersion(newVersion);
            }

            throw new SlotMapException($"Cannot replace `{nameof(item)}` in {s_name}. Argument value: {item}");
        }

        public bool TryReplace(SlotKey key, T item, out SlotKey newKey)
        {
            ref var page = ref GetPage(_pages, _pageSize, key, out var itemIndex);

            if (page.TryReplace(itemIndex, key.Version, item, out var newVersion))
            {
                newKey = key.WithVersion(newVersion);
                return true;
            }

            newKey = key;
            return false;
        }

        public bool Remove(SlotKey key)
        {
            ref var page = ref GetPage(_pages, _pageSize, key, out var itemIndex);
            var result = page.TryRemove(itemIndex, key.Version);

            if (result && key.Version < SlotVersion.MaxValue)
            {
                _freeKeys.Enqueue(key);
            }

            return result;
        }

        private bool TryGetNewKey(out SlotKey key, out Address address)
        {
            var pageSize = _pageSize;
            var freeKeys = _freeKeys;

            if (freeKeys.Count > _freeIndicesLimit)
            {
                var oldKey = freeKeys.Dequeue();
                key = oldKey.WithVersion(oldKey.Version + 1).WithTag(default);
                address = Address.FromIndex(key.Index, pageSize);
                return true;
            }

            var pages = _pages;
            var pageCount = (uint)pages.LongLength;
            var lastPageIndex = pageCount - 1;

            ref var lastPage = ref pages[lastPageIndex];
            var lastPageCount = lastPage.Count;

            if (lastPageCount >= pageSize)
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
            key = new SlotKey(address.ToIndex(_pageSize));
            return true;
        }

        private void MakeNewPage()
        {
            var oldPages = _pages;
            var oldLength = oldPages.Length;
            var maxPageCount = _maxPageCount;

            if (oldLength >= maxPageCount)
            {
                Checks.Suggest(false, $"Cannot add more page to {s_name}: the limit of {maxPageCount} pages has been reached.");
                return;
            }

            var newPages = new Page[oldLength + 1];

            if (oldLength > 0)
            {
                Array.Copy(oldPages, newPages, oldLength);
            }

            newPages[oldLength] = new Page(_pageSize);
            _pages = newPages;

            if (s_itemIsUnmanaged && oldLength > 0)
            {
                Array.Clear(oldPages, 0, oldLength);
            }
        }

        private static ref Page GetPage(Page[] pages, uint pageSize, SlotKey key, out uint itemIndex)
        {
            Checks.Require(key.IsValid, $"`{nameof(key)}` is invalid.");

            var address = Address.FromIndex(key.Index, pageSize);
            var pageCount = (uint)pages.LongLength;

            Checks.Require(address.PageIndex < pageCount, $"`{nameof(key)}.{nameof(SlotKey.Index)}` is out of range. Argument value: {key.Index}.");

            itemIndex = address.ItemIndex;
            return ref pages[address.PageIndex];
        }

        private static uint GetMaxPageCount(uint maxIndex, uint pageSize)
        {
            var result = maxIndex / pageSize;
            return (maxIndex % pageSize == 0) ? result : result + 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsPowerOfTwo(uint x)
            => (x != 0) && ((x & (x - 1)) == 0);
    }
}
