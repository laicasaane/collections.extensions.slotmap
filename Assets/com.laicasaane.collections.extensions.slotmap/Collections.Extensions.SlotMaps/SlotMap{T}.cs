using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Collections.Extensions.SlotMaps
{
    public partial class SlotMap<T>
    {
        public const int DEFAULT_PAGE_SIZE = 1024;
        public const int DEFAULT_FREE_INDICES_LITMIT = 32;

        private static readonly string s_name = $"{nameof(SlotMap<T>)}<{typeof(T).Name}>";
        private static readonly bool s_itemIsUnmanaged = RuntimeHelpers.IsReferenceOrContainsReferences<T>();

        private readonly uint _pageSize;
        private readonly uint _freeIndicesLimit;
        private readonly uint _maxPageCount;

        private readonly Queue<SlotKey> _freeKeys = new();

        private Page[] _pages = Array.Empty<Page>();
        private uint _itemCount;
        private uint _tombstoneCount;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pageSize">The maximum number of items that can be stored in a page.</param>
        /// <param name="freeIndicesLimit">
        /// <para>The maximum number of indices that was removed and can be free.</para>
        /// <para>Free indices will be reused when their total count exceeds this threshold.</para>
        /// </param>
        public SlotMap(int pageSize = DEFAULT_PAGE_SIZE, int freeIndicesLimit = DEFAULT_FREE_INDICES_LITMIT)
        {
            Checks.Require(pageSize > 0, $"`{nameof(pageSize)}` must be greater than 0. Page size value: {pageSize}.");

            _pageSize = (uint)Math.Clamp(pageSize, 0, int.MaxValue);
            _freeIndicesLimit = (uint)Math.Clamp(freeIndicesLimit, 0, pageSize);

            Checks.Require(
                  IsPowerOfTwo(_pageSize)
                , $"`{nameof(pageSize)}` must be a power of two. Page size value: {_pageSize}."
            );

            Checks.Suggest(
                  _freeIndicesLimit <= _pageSize
                , $"`{nameof(freeIndicesLimit)}` should be lesser than "
                + $"or equal to `{nameof(pageSize)}: {_pageSize}`, "
                + $"or it would be clamped to `{nameof(_pageSize)}`. "
                + $"Free indices limit value: {_freeIndicesLimit}."
            );

            _maxPageCount = GetMaxPageCount(_pageSize);
            _itemCount = 0;
            _tombstoneCount = 0;

            TryCreatePage();
        }

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
            if (FindAddress(_pages, _pageSize, key, out var address))
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
            var pageSize = _pageSize;
            var length = keys.Length;

            for (var i = 0; i < length; i++)
            {
                ref readonly var key = ref keys[i];

                if (FindAddress(pages, pageSize, key, out var address))
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
            if (FindAddress(_pages, _pageSize, key, out var address))
            {
                ref var page = ref _pages[address.PageIndex];
                return ref page.GetRef(address.ItemIndex, key);
            }

            throw new SlotMapException($"Cannot find address for `{nameof(key)}`. Key value: {key}.");
        }

        public ref readonly T GetRefNotThrow(SlotKey key)
        {
            if (FindAddress(_pages, _pageSize, key, out var address))
            {
                ref var page = ref _pages[address.PageIndex];
                return ref page.GetRefNotThrow(address.ItemIndex, key);
            }

            return ref Unsafe.NullRef<T>();
        }

        public bool TryGet(SlotKey key, out T item)
        {
            if (FindAddress(_pages, _pageSize, key, out var address) == false)
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
            var pageSize = _pageSize;
            var length = keys.Length;
            var destIndex = 0;

            for (var i = 0; i < length; i++)
            {
                ref readonly var key = ref keys[i];

                if (FindAddress(pages, pageSize, key, out var address) == false)
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
                Checks.Suggest(false, $"Cannot add `{nameof(item)}` to {s_name}. Item value: {item}.");
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
                    Checks.Suggest(false
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
            if (FindAddress(_pages, _pageSize, key, out var address))
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
            if (FindAddress(_pages, _pageSize, key, out var address) == false)
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

            if (FindAddress(pages, _pageSize, key, out var address) == false)
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
            var pageSize = _pageSize;
            var freeKeys = _freeKeys;
            var length = keys.Length;

            ref var itemCount = ref _itemCount;
            ref var tombstoneCount = ref _tombstoneCount;

            for (var i = 0; i < length; i++)
            {
                ref readonly var key = ref keys[i];

                if (FindAddress(pages, pageSize, key, out var address) == false)
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
            if (FindAddress(_pages, _pageSize, key, out var address) == false)
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
            var pageSize = _pageSize;
            var freeKeys = _freeKeys;

            if (freeKeys.Count > _freeIndicesLimit)
            {
                var oldKey = freeKeys.Dequeue();

#if ENABLE_SLOTMAP_KEY_TAG
                key = oldKey.WithVersion(oldKey.Version + 1).WithTag(default);
#else
                key = oldKey.WithVersion(oldKey.Version + 1);
#endif

                address = SlotAddress.FromIndex(key.Index, pageSize);
                return true;
            }

            var pages = _pages;
            var pageCount = (uint)pages.Length;
            var lastPageIndex = pageCount - 1;

            ref var lastPage = ref pages[lastPageIndex];
            var lastPageCount = lastPage.Count;

            if (lastPageCount >= pageSize)
            {
                if (pageCount >= _maxPageCount || TryCreatePage() == false)
                {
                    key = default;
                    address = default;
                    return false;
                }

                lastPageIndex += 1;
                lastPageCount = 0;
            }

            address = new(lastPageIndex, lastPageCount);
            key = new SlotKey(address.ToIndex(_pageSize));
            return true;
        }

        private bool TryCreatePage()
        {
            var oldPages = _pages;
            var oldLength = oldPages.Length;
            var maxPageCount = _maxPageCount;

            if (oldLength >= maxPageCount)
            {
                return false;
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

            return true;
        }

        private static bool FindAddress(
              Page[] pages
            , uint pageSize
            , SlotKey key
            , out SlotAddress address
        )
        {
            if (key.IsValid == false)
            {
                Checks.Suggest(false, $"`{nameof(key)}` is invalid. Key value: {key}.");

                address = default;
                return false;
            }

            address = SlotAddress.FromIndex(key.Index, pageSize);
            var pageCount = (uint)pages.Length;

            if (address.PageIndex >= pageCount)
            {
                Checks.Suggest(false
                    , $"`{nameof(key)}.{nameof(SlotKey.Index)}` is out of range. Key value: {key}."
                );

                address = default;
                return false;
            }

            return true;
        }

        /// <summary>
        /// <para>Because <see cref="PageSize"/> must be a power of two, its minimum value would be 2.</para>
        /// <para>Thus the highest page count possible would always be <see cref="int"/>.<see cref="int.MaxValue"/>.</para>
        /// <para>Thus <see cref="uint"/> overflow would never occur.</para>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint GetMaxPageCount(uint pageSize)
        {
            const uint MAX_INDEX = uint.MaxValue;
            var result = MAX_INDEX / pageSize;
            return (MAX_INDEX % pageSize == 0) ? result : result + 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsPowerOfTwo(uint x)
            => (x != 0) && ((x & (x - 1)) == 0);
    }
}
