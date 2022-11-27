using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Collections.Extensions.SlotMap
{
    public partial class SlotMap<T>
    {
        private static readonly string s_name = $"{nameof(SlotMap<T>)}<{typeof(T).Name}>";
        private static readonly bool s_itemIsUnmanaged = RuntimeHelpers.IsReferenceOrContainsReferences<T>();

        private readonly uint _pageSize;
        private readonly uint _freeIndicesLimit;
        private readonly uint _maxPageCount;

        private readonly Queue<SlotKey> _freeKeys = new();

        private Page[] _pages = Array.Empty<Page>();
        private uint _count;
        private uint _tombstoneCount;

        public SlotMap(uint pageSize = 1024, uint freeIndicesLimit = 32)
        {
            Checks.Require(
                  IsPowerOfTwo(pageSize)
                , $"`{nameof(pageSize)}` must be a power of two. Page size value: {pageSize}."
            );

            Checks.Suggest(
                  freeIndicesLimit <= pageSize
                , $"`{nameof(freeIndicesLimit)}` should be lesser than "
                + $"or equal to `{nameof(pageSize)}: {pageSize}`, "
                + $"or it would be clamped to `{nameof(pageSize)}`. "
                + $"Free indices limit value: {freeIndicesLimit}."
            );

            _pageSize = pageSize;
            _freeIndicesLimit = Math.Clamp(freeIndicesLimit, 0, pageSize);
            _maxPageCount = GetMaxPageCount(pageSize);
            _count = 0;
            _tombstoneCount = 0;

            TryCreatePage();
        }

        public uint PageSize
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _pageSize;
        }

        public uint Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _count;
        }

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

        public SlotKey Add(T item)
        {
            if (TryGetNewKey(out var key, out var address))
            {
                ref var page = ref _pages[address.PageIndex];
                page.Add(address.ItemIndex, key, item);

                _count++;
                return key;
            }

            throw new SlotMapException($"Cannot add `{nameof(item)}` to {s_name}. Item value: {item}.");
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
                _count++;
                return true;
            }

            return false;
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
            if (FindAddress(_pages, _pageSize, key, out var address) == false)
            {
                return false;
            }

            ref var page = ref _pages[address.PageIndex];

            if (page.TryRemove(address.ItemIndex, key) == false)
            {
                return false;
            }

            _count--;

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

        public bool Contains(SlotKey key)
        {
            if (FindAddress(_pages, _pageSize, key, out var address) == false)
            {
                return false;
            }

            ref var page = ref _pages[address.PageIndex];
            return page.Contains(address.ItemIndex, key);
        }

        public void Clear()
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

            _count = 0;
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
              Page[] pages, uint pageSize
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
