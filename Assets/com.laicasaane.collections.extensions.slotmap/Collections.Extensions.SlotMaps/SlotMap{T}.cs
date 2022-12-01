using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Collections.Extensions.SlotMaps
{
    public partial class SlotMap<T> : ISlotMap<T>
    {
        private static readonly string s_name = $"{nameof(SlotMap<T>)}<{typeof(T).Name}>";
        private static readonly bool s_itemIsUnmanaged = RuntimeHelpers.IsReferenceOrContainsReferences<T>();

        private readonly uint _pageSize;
        private readonly uint _freeIndicesLimit;
        private readonly uint _maxPageCount;

        private readonly Queue<SlotKey> _freeKeys = new();

        private Page[] _pages = Array.Empty<Page>();
        private uint _itemCount;
        private uint _tombstoneCount;

        /// <summary></summary>
        /// <param name="pageSize">
        /// <para>The maximum number of items that can be stored in a page.</para>
        /// <para>Must be a power of two.</para>
        /// </param>
        /// <param name="freeIndicesLimit">
        /// <para>The maximum number of indices that was removed and can be free.</para>
        /// <para>Free indices will be reused when their total count exceeds this threshold.</para>
        /// </param>
        public SlotMap(
              int pageSize = (int)PowerOfTwo.x1024
            , int freeIndicesLimit = (int)PowerOfTwo.x32
        )
        {
            Checks.Require(pageSize > 0, $"`{nameof(pageSize)}` must be greater than 0. Page size value: {pageSize}.");

            _pageSize = (uint)Math.Clamp(pageSize, 0, (int)PowerOfTwo.x1_073_741_824);
            _freeIndicesLimit = (uint)Math.Clamp(freeIndicesLimit, 0, pageSize);

            Checks.Require(
                  Utilities.IsPowerOfTwo(_pageSize)
                , $"`{nameof(pageSize)}` must be a power of two. Page size value: {_pageSize}."
            );

            Checks.Warning(
                  _freeIndicesLimit <= _pageSize
                , $"`{nameof(freeIndicesLimit)}` should be lesser than "
                + $"or equal to `{nameof(pageSize)}: {_pageSize}`, "
                + $"or it would be clamped to `{nameof(_pageSize)}`. "
                + $"Free indices limit value: {_freeIndicesLimit}."
            );

            _maxPageCount = Utilities.GetMaxPageCount(_pageSize);
            _itemCount = 0;
            _tombstoneCount = 0;

            TryAddPage();
        }

        /// <summary></summary>
        /// <param name="pageSize">
        /// <para>The maximum number of items that can be stored in a page.</para>
        /// <para>Must be a power of two.</para>
        /// </param>
        /// <param name="freeIndicesLimit">
        /// <para>The maximum number of indices that was removed and can be free.</para>
        /// <para>Free indices will be reused when their total count exceeds this threshold.</para>
        /// </param>
        public SlotMap(
              PowerOfTwo pageSize = PowerOfTwo.x1024
            , int freeIndicesLimit = (int)PowerOfTwo.x32
        )
            : this((int)pageSize, freeIndicesLimit)
        { }

        /// <summary>
        /// The maximum number of items that can be stored in a page.
        /// </summary>
        public uint PageSize
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _pageSize;
        }

        /// <summary>
        /// <para>The maximum number of indices that was removed and can be free.</para>
        /// <para>Free indices will be reused when their total count exceeds this threshold.</para>
        /// </summary>
        public uint FreeIndicesLimit
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _freeIndicesLimit;
        }

        /// <summary>
        /// The number of pages.
        /// </summary>
        public int PageCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _pages.Length;
        }

        /// <summary>
        /// The number of stored items.
        /// </summary>
        public uint ItemCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _itemCount;
        }

        /// <summary>
        /// The number of slots that are tombstone and cannot be used anymore.
        /// </summary>
        public uint TombstoneCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _tombstoneCount;
        }

        public T Get(SlotKey key)
        {
            if (Utilities.FindAddress(_pages.Length, _pageSize, key, out var address))
            {
                ref var page = ref _pages[address.PageIndex];
                return page.GetRef(address.ItemIndex, key);
            }

            throw new SlotMapException($"Cannot find address for `{nameof(key)}`. Key value: {key}.");
        }

        public void GetRange(
              in ReadOnlySpan<SlotKey> keys
            , in Span<T> returnItems
        )
        {
            Checks.Require(
                  returnItems.Length >= keys.Length
                , $"The length `{nameof(returnItems)}` must be greater than "
                + $"or equal to the length of `{nameof(keys)}`."
            );

            var pages = _pages;
            var pageLength = pages.Length;
            var pageSize = _pageSize;
            var length = keys.Length;

            for (var i = 0; i < length; i++)
            {
                ref readonly var key = ref keys[i];

                if (Utilities.FindAddress(pageLength, pageSize, key, out var address))
                {
                    ref var page = ref pages[address.PageIndex];
                    returnItems[i] = page.GetRef(address.ItemIndex, key);
                }
                else
                {
                    throw new SlotMapException(
                        $"Cannot find address for `{nameof(key)}` at index {i}. Key value: {key}."
                    );
                }
            }
        }

        public ref readonly T GetRef(SlotKey key)
        {
            if (Utilities.FindAddress(_pages.Length, _pageSize, key, out var address))
            {
                ref var page = ref _pages[address.PageIndex];
                return ref page.GetRef(address.ItemIndex, key);
            }

            throw new SlotMapException($"Cannot find address for `{nameof(key)}`. Key value: {key}.");
        }

        public ref readonly T GetRefNotThrow(SlotKey key)
        {
            if (Utilities.FindAddress(_pages.Length, _pageSize, key, out var address))
            {
                ref var page = ref _pages[address.PageIndex];
                return ref page.GetRefNotThrow(address.ItemIndex, key);
            }

            return ref Unsafe.NullRef<T>();
        }

        public bool TryGet(SlotKey key, out T item)
        {
            if (Utilities.FindAddress(_pages.Length, _pageSize, key, out var address) == false)
            {
                item = default;
                return false;
            }

            ref var page = ref _pages[address.PageIndex];
            ref var itemRef = ref page.GetRefNotThrow(address.ItemIndex, key);

            if (Unsafe.IsNullRef<T>(ref itemRef))
            {
                item = default;
                return false;
            }

            item = itemRef;
            return true;
        }

        public void TryGetRange(
              in ReadOnlySpan<SlotKey> keys
            , in Span<SlotKey> returnKeys
            , in Span<T> returnItems
            , out uint returnCount
        )
        {
            Checks.Require(
                  returnKeys.Length >= keys.Length
                , $"The length `{nameof(returnKeys)}` must be greater than "
                + $"or equal to the length of `{nameof(keys)}`."
            );

            Checks.Require(
                  returnItems.Length >= keys.Length
                , $"The length `{nameof(returnItems)}` must be greater than "
                + $"or equal to the length of `{nameof(keys)}`."
            );

            var pages = _pages;
            var pageLength = pages.Length;
            var pageSize = _pageSize;
            var length = keys.Length;
            var destIndex = 0;

            for (var i = 0; i < length; i++)
            {
                ref readonly var key = ref keys[i];

                if (Utilities.FindAddress(pageLength, pageSize, key, out var address) == false)
                {
                    continue;
                }

                ref var page = ref pages[address.PageIndex];
                ref var itemRef = ref page.GetRefNotThrow(address.ItemIndex, key);

                if (Unsafe.IsNullRef<T>(ref itemRef))
                {
                    continue;
                }

                returnKeys[destIndex] = key;
                returnItems[destIndex] = itemRef;
                destIndex++;
            }

            returnCount = (uint)destIndex;
        }

        public SlotKey Add(T item)
        {
            if (TryGetNewKey(out var key, out var address))
            {
                ref var page = ref _pages[address.PageIndex];
                page.Add(address.ItemIndex, key, item);

                _itemCount++;
                return key;
            }

            throw new SlotMapException($"Cannot add `{nameof(item)}` to {s_name}. Item value: {item}.");
        }

        public void AddRange(
              in ReadOnlySpan<T> items
            , in Span<SlotKey> returnKeys
        )
        {
            Checks.Require(
                  returnKeys.Length >= items.Length
                , $"The length `{nameof(returnKeys)}` must be greater than "
                + $"or equal to the length of `{nameof(items)}`."
            );

            var pages = _pages;
            var length = items.Length;

            ref var itemCount = ref _itemCount;

            for (var i = 0; i < length; i++)
            {
                ref readonly var item = ref items[i];

                if (TryGetNewKey(out var key, out var address))
                {
                    ref var page = ref pages[address.PageIndex];
                    page.Add(address.ItemIndex, key, item);

                    itemCount++;

                    returnKeys[i] = key;
                }
                else
                {
                    throw new SlotMapException(
                        $"Cannot add `{nameof(item)}` to {s_name} at index {i}. Item value: {item}."
                    );
                }
            }
        }

        public bool TryAdd(T item, out SlotKey key)
        {
            if (TryGetNewKey(out key, out var address) == false)
            {
                Checks.Warning(false, $"Cannot add `{nameof(item)}` to {s_name}. Item value: {item}.");
                return false;
            }

            ref var page = ref _pages[address.PageIndex];

            if (page.TryAdd(address.ItemIndex, key, item))
            {
                _itemCount++;
                return true;
            }

            return false;
        }

        public void TryAddRange(
              in ReadOnlySpan<T> items
            , in Span<SlotKey> returnKeys
            , out uint returnCount
        )
        {
            Checks.Require(
                  returnKeys.Length >= items.Length
                , $"The length `{nameof(returnKeys)}` must be greater than "
                + $"or equal to the length of `{nameof(items)}`."
            );

            var pages = _pages;
            var length = items.Length;
            var resultIndex = 0;

            ref var itemCount = ref _itemCount;

            for (var i = 0; i < length; i++)
            {
                ref readonly var item = ref items[i];

                if (TryGetNewKey(out var key, out var address) == false)
                {
                    Checks.Warning(false
                        , $"Cannot add `{nameof(item)}` to {s_name} at index {i}. Item value: {item}."
                    );
                    continue;
                }

                ref var page = ref pages[address.PageIndex];

                if (page.TryAdd(address.ItemIndex, key, item))
                {
                    itemCount++;

                    returnKeys[resultIndex] = key;
                    resultIndex++;
                }
            }

            returnCount = (uint)resultIndex;
        }

        public SlotKey Replace(SlotKey key, T item)
        {
            if (Utilities.FindAddress(_pages.Length, _pageSize, key, out var address))
            {
                ref var page = ref _pages[address.PageIndex];

                if (page.TryReplace(address.ItemIndex, key, item, out var newKey))
                {
                    return newKey;
                }
            }

            throw new SlotMapException($"Cannot replace `{nameof(item)}` in {s_name}. Item value: {item}.");
        }

        public bool TryReplace(SlotKey key, T item, out SlotKey newKey)
        {
            if (Utilities.FindAddress(_pages.Length, _pageSize, key, out var address) == false)
            {
                newKey = key;
                return false;
            }

            ref var page = ref _pages[address.PageIndex];
            return page.TryReplace(address.ItemIndex, key, item, out newKey);
        }

        public bool Remove(SlotKey key)
        {
            var pages = _pages;
            var pageLength = pages.Length;

            if (Utilities.FindAddress(pageLength, _pageSize, key, out var address) == false)
            {
                return false;
            }

            ref var page = ref pages[address.PageIndex];

            if (page.TryRemove(address.ItemIndex, key) == false)
            {
                return false;
            }

            _itemCount--;

            if (key.Version < SlotVersion.MaxValue)
            {
                _freeKeys.Enqueue(key);
            }
            else
            {
                _tombstoneCount++;
            }

            return true;
        }

        public void RemoveRange(in ReadOnlySpan<SlotKey> keys)
        {
            var pages = _pages;
            var pageLength = pages.Length;
            var pageSize = _pageSize;
            var freeKeys = _freeKeys;
            var length = keys.Length;

            ref var itemCount = ref _itemCount;
            ref var tombstoneCount = ref _tombstoneCount;

            for (var i = 0; i < length; i++)
            {
                ref readonly var key = ref keys[i];

                if (Utilities.FindAddress(pageLength, pageSize, key, out var address) == false)
                {
                    continue;
                }

                ref var page = ref pages[address.PageIndex];

                if (page.TryRemove(address.ItemIndex, key) == false)
                {
                    continue;
                }

                itemCount--;

                if (key.Version < SlotVersion.MaxValue)
                {
                    freeKeys.Enqueue(key);
                }
                else
                {
                    tombstoneCount++;
                }
            }
        }

        public bool Contains(SlotKey key)
        {
            if (Utilities.FindAddress(_pages.Length, _pageSize, key, out var address) == false)
            {
                return false;
            }

            ref var page = ref _pages[address.PageIndex];
            return page.Contains(address.ItemIndex, key);
        }

        /// <summary>
        /// Clear the first page, but remove every other pages.
        /// </summary>
        public void Reset()
        {
            var pages = _pages;
            var length = (uint)pages.Length;

            if (length > 0)
            {
                pages[0].Clear();
                _pages = new Page[1] {
                    pages[0]
                };
            }

            _itemCount = 0;
            _tombstoneCount = 0;
        }

        private bool TryGetNewKey(out SlotKey key, out SlotAddress address)
        {
            if (TryReuseFreeKey(out key, out address))
            {
                return true;
            }

            var pageSize = _pageSize;
            var pages = _pages;
            var numberOfPages = (uint)pages.Length;
            var lastPageIndex = numberOfPages - 1;

            ref var lastPage = ref pages[lastPageIndex];
            var lastPageItemCount = lastPage.Count;

            // If the last page is full, try adding a new page
            if (lastPageItemCount >= pageSize)
            {
                if (TryAddPage() == false)
                {
                    SetDefault(out key, out address);
                    return false;
                }

                lastPageIndex += 1;
                lastPageItemCount = 0;
            }

            address = new(lastPageIndex, lastPageItemCount);
            key = new SlotKey(address.ToIndex(_pageSize));
            return true;
        }

        private bool TryReuseFreeKey(
              out SlotKey key
            , out SlotAddress address
        )
        {
            var pageSize = _pageSize;
            var freeKeys = _freeKeys;

            if (freeKeys.Count <= _freeIndicesLimit)
            {
                SetDefault(out key, out address);
                return false;
            }

            var oldKey = freeKeys.Dequeue();
            key = oldKey.WithVersion(oldKey.Version + 1);
            address = SlotAddress.FromIndex(key.Index, pageSize);
            return true;
        }

        private bool TryAddPage()
        {
            var newPageIndex = _pages.Length;

            if (newPageIndex >= _maxPageCount)
            {
                Checks.Warning(false,
                      $"Cannot add new page because it has reached "
                    + $"the maximum limit of {_maxPageCount} pages."
                );

                return false;
            }

            Array.Resize(ref _pages, newPageIndex + 1);
            _pages[newPageIndex] = new Page(_pageSize);

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetDefault(out SlotKey key, out SlotAddress address)
        {
            key = default;
            address = default;
        }
    }
}
