using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Collections.Extensions.SlotMaps
{
    public partial class SlotMap<TValue> : ISlotMap<TValue>
    {
        private static readonly string s_name = $"{nameof(SlotMap<TValue>)}<{typeof(TValue).Name}>";
        private static readonly bool s_valueIsUnmanaged = RuntimeHelpers.IsReferenceOrContainsReferences<TValue>();

        private readonly uint _pageSize;
        private readonly uint _freeIndicesLimit;
        private readonly uint _maxPageCount;

        private readonly Queue<SlotKey> _freeKeys = new();

        private Page[] _pages = Array.Empty<Page>();
        private uint _slotCount;
        private uint _tombstoneCount;
        private int _version;

        /// <summary></summary>
        /// <param name="pageSize">
        /// <para>The maximum number of slots that can be stored in a page.</para>
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
            Checks.Require(pageSize > 0, $"`{nameof(pageSize)}` must be greater than 0. Value: {pageSize}.");

            _pageSize = (uint)Math.Clamp(pageSize, 0, (int)PowerOfTwo.x1_073_741_824);
            _freeIndicesLimit = (uint)Math.Clamp(freeIndicesLimit, 0, pageSize);

            Checks.Require(
                  Utils.IsPowerOfTwo(_pageSize)
                , $"`{nameof(pageSize)}` must be a power of two. Value: {_pageSize}."
            );

            Checks.Warning(
                  _freeIndicesLimit <= _pageSize
                , $"`{nameof(freeIndicesLimit)}` should be lesser than "
                + $"or equal to `{nameof(pageSize)}: {_pageSize}`, "
                + $"or it would be clamped to `{nameof(_pageSize)}`. "
                + $"Value: {_freeIndicesLimit}."
            );

            _maxPageCount = Utils.GetMaxPageCount(_pageSize);
            _slotCount = 0;
            _tombstoneCount = 0;

            TryAddPage();

            _version = 0;
        }

        /// <summary></summary>
        /// <param name="pageSize">
        /// <para>The maximum number of slots that can be stored in a page.</para>
        /// <para>Must be a power of two.</para>
        /// </param>
        /// <param name="freeIndicesLimit">
        /// <para>The maximum number of indices that was removed and can be free.</para>
        /// <para>Free indices will be reused when their total count exceeds this threshold.</para>
        /// </param>
        public SlotMap(
              PowerOfTwo pageSize
            , int freeIndicesLimit = (int)PowerOfTwo.x32
        )
            : this((int)pageSize, freeIndicesLimit)
        { }

        /// <summary>
        /// The maximum number of slots that can be stored in a page.
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
        /// The number of stored slots.
        /// </summary>
        public uint SlotCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _slotCount;
        }

        /// <summary>
        /// The number of slots that are tombstone and cannot be used anymore.
        /// </summary>
        public uint TombstoneCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _tombstoneCount;
        }

        public ReadOnlyMemory<Page> Pages
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _pages;
        }

        public TValue Get(SlotKey key)
        {
            Checks.Require(key.IsValid, $"Key `{key}` is invalid.");

            if (Utils.FindPagedAddress(_pages.Length, _pageSize, key, out var address) == false)
            {
                throw new SlotMapException($"Cannot find address for `{key}`.");
            }

            ref var page = ref _pages[address.PageIndex];
            return page.GetRef(address.SlotIndex, key);
        }

        public void GetRange(
              in ReadOnlySpan<SlotKey> keys
            , Span<TValue> returnValues
        )
        {
            Checks.Require(
                  returnValues.Length >= keys.Length
                , $"The length `{nameof(returnValues)}` must be greater than "
                + $"or equal to the length of `{nameof(keys)}`."
            );

            var pages = _pages;
            var pageLength = pages.Length;
            var pageSize = _pageSize;
            var length = keys.Length;

            for (var i = 0; i < length; i++)
            {
                ref readonly var key = ref keys[i];

                Checks.Require(key.IsValid, $"Key `{key}` is invalid.");

                if (Utils.FindPagedAddress(pageLength, pageSize, key, out var address) == false)
                {
                    Checks.Require(false, $"Cannot find address for `{key}` at index {i}.");
                    continue;
                }

                ref var page = ref pages[address.PageIndex];
                returnValues[i] = page.GetRef(address.SlotIndex, key);
            }
        }

        public ref readonly TValue GetRef(SlotKey key)
        {
            Checks.Require(key.IsValid, $"Key `{key}` is invalid.");

            if (Utils.FindPagedAddress(_pages.Length, _pageSize, key, out var address) == false)
            {
                Checks.Require(false, $"Cannot find address for `{key}`.");
                return ref Unsafe.NullRef<TValue>();
            }

            ref var page = ref _pages[address.PageIndex];
            return ref page.GetRef(address.SlotIndex, key);
        }

        public ref readonly TValue GetRefNotThrow(SlotKey key)
        {
            if (key.IsValid == false)
            {
                Checks.Warning(false, $"Key `{key}` is invalid.");
                return ref Unsafe.NullRef<TValue>();
            }

            if (Utils.FindPagedAddress(_pages.Length, _pageSize, key, out var address) == false)
            {
                Checks.Warning(false, $"Cannot find address for `{key}`.");
                return ref Unsafe.NullRef<TValue>();
            }

            ref var page = ref _pages[address.PageIndex];
            return ref page.GetRefNotThrow(address.SlotIndex, key);
        }

        public bool TryGet(SlotKey key, out TValue value)
        {
            if (key.IsValid == false)
            {
                Checks.Warning(false, $"Key `{key}` is invalid.");
                value = default;
                return false;
            }

            if (Utils.FindPagedAddress(_pages.Length, _pageSize, key, out var address) == false)
            {
                Checks.Warning(false, $"Cannot find address for `{key}`.");
                value = default;
                return false;
            }

            ref var page = ref _pages[address.PageIndex];
            ref var valueRef = ref page.GetRefNotThrow(address.SlotIndex, key);

            if (Unsafe.IsNullRef<TValue>(ref valueRef))
            {
                value = default;
                return false;
            }

            value = valueRef;
            return true;
        }

        public bool TryGetRange(
              in ReadOnlySpan<SlotKey> keys
            , Span<SlotKey> returnKeys
            , Span<TValue> returnValues
            , out uint returnValuesCount
        )
        {
            if (returnKeys.Length < keys.Length)
            {
                Checks.Warning(false
                    , $"The length `{nameof(returnKeys)}` must be greater than "
                    + $"or equal to the length of `{nameof(keys)}`."
                );

                returnValuesCount = 0;
                return false;
            }

            if (returnValues.Length < keys.Length)
            {
                Checks.Require(false
                    , $"The length `{nameof(returnValues)}` must be greater than "
                    + $"or equal to the length of `{nameof(keys)}`."
                );

                returnValuesCount = 0;
                return false;
            }

            var pages = _pages;
            var pageLength = pages.Length;
            var pageSize = _pageSize;
            var length = keys.Length;
            var destIndex = 0;

            for (var i = 0; i < length; i++)
            {
                ref readonly var key = ref keys[i];

                if (key.IsValid == false)
                {
                    Checks.Warning(false, $"Key `{key}` is invalid.");
                    continue;
                }

                if (Utils.FindPagedAddress(pageLength, pageSize, key, out var address) == false)
                {
                    continue;
                }

                ref var page = ref pages[address.PageIndex];
                ref var valueRef = ref page.GetRefNotThrow(address.SlotIndex, key);

                if (Unsafe.IsNullRef<TValue>(ref valueRef))
                {
                    continue;
                }

                returnKeys[destIndex] = key;
                returnValues[destIndex] = valueRef;
                destIndex++;
            }

            returnValuesCount = (uint)destIndex;
            return true;
        }

        public SlotKey Add(TValue value)
        {
            _version++;

            if (TryGetNewKey(out var key, out var address) == false)
            {
                Checks.Require(false, $"Cannot add `{value} ` to {s_name}.");
                return default;
            }

            ref var page = ref _pages[address.PageIndex];
            page.Add(address.SlotIndex, key, value);
            _slotCount++;
            return key;
        }

        public void AddRange(
              in ReadOnlySpan<TValue> values
            , Span<SlotKey> returnKeys
        )
        {
            Checks.Require(
                  returnKeys.Length >= values.Length
                , $"The length `{nameof(returnKeys)}` must be greater than "
                + $"or equal to the length of `{nameof(values)}`."
            );

            _version++;

            var pages = _pages;
            var length = values.Length;

            ref var slotCount = ref _slotCount;

            for (var i = 0; i < length; i++)
            {
                ref readonly var value = ref values[i];

                if (TryGetNewKey(out var key, out var address) == false)
                {
                    Checks.Require(false, $"Cannot add `{value}` to {s_name} at index {i}.");
                    continue;
                }

                ref var page = ref pages[address.PageIndex];
                page.Add(address.SlotIndex, key, value);
                slotCount++;
                returnKeys[i] = key;
            }
        }

        public bool TryAdd(TValue value, out SlotKey key)
        {
            _version++;

            if (TryGetNewKey(out key, out var address) == false)
            {
                Checks.Warning(false, $"Cannot add `{value}` to {s_name}.");
                return false;
            }

            ref var page = ref _pages[address.PageIndex];

            if (page.TryAdd(address.SlotIndex, key, value))
            {
                _slotCount++;
                return true;
            }

            return false;
        }

        public bool TryAddRange(
              in ReadOnlySpan<TValue> values
            , Span<SlotKey> returnKeys
            , out uint returnKeyCount
        )
        {
            _version++;

            if (returnKeys.Length < values.Length)
            {
                Checks.Warning(false
                    , $"The length `{nameof(returnKeys)}` must be greater than "
                    + $"or equal to the length of `{nameof(values)}`."
                );

                returnKeyCount = 0;
                return false;
            }

            var pages = _pages;
            var length = values.Length;
            var resultIndex = 0;

            ref var slotCount = ref _slotCount;

            for (var i = 0; i < length; i++)
            {
                ref readonly var value = ref values[i];

                if (TryGetNewKey(out var key, out var address) == false)
                {
                    Checks.Warning(false
                        , $"Cannot add `{value}` to {s_name} at index {i}."
                    );
                    continue;
                }

                ref var page = ref pages[address.PageIndex];

                if (page.TryAdd(address.SlotIndex, key, value))
                {
                    returnKeys[resultIndex] = key;

                    slotCount++;
                    resultIndex++;
                }
            }

            returnKeyCount = (uint)resultIndex;
            return true;
        }

        public SlotKey Replace(SlotKey key, TValue value)
        {
            _version++;

            Checks.Require(key.IsValid, $"Key `{key}` is invalid.");

            if (Utils.FindPagedAddress(_pages.Length, _pageSize, key, out var address) == false)
            {
                Checks.Require(false, $"Cannot replace `{value}` in {s_name}.");
                return default;
            }

            ref var page = ref _pages[address.PageIndex];
            return page.Replace(address.SlotIndex, key, value);
        }

        public bool TryReplace(SlotKey key, TValue value, out SlotKey newKey)
        {
            _version++;

            if (key.IsValid == false)
            {
                Checks.Warning(key.IsValid, $"Key `{key}` is invalid.");
                newKey = key;
                return false;
            }

            if (Utils.FindPagedAddress(_pages.Length, _pageSize, key, out var address) == false)
            {
                newKey = key;
                return false;
            }

            ref var page = ref _pages[address.PageIndex];
            return page.TryReplace(address.SlotIndex, key, value, out newKey);
        }

        public bool Remove(SlotKey key)
        {
            _version++;

            if (key.IsValid == false)
            {
                Checks.Warning(key.IsValid, $"Key `{key}` is invalid.");
                return false;
            }

            var pages = _pages;
            var pageLength = pages.Length;

            if (Utils.FindPagedAddress(pageLength, _pageSize, key, out var address) == false)
            {
                return false;
            }

            ref var page = ref pages[address.PageIndex];

            if (page.Remove(address.SlotIndex, key) == false)
            {
                return false;
            }

            _slotCount--;

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
            _version++;

            var pages = _pages;
            var pageLength = pages.Length;
            var pageSize = _pageSize;
            var freeKeys = _freeKeys;
            var length = keys.Length;

            ref var slotCount = ref _slotCount;
            ref var tombstoneCount = ref _tombstoneCount;

            for (var i = 0; i < length; i++)
            {
                ref readonly var key = ref keys[i];

                if (key.IsValid == false)
                {
                    Checks.Warning(key.IsValid, $"Key `{key}` is invalid.");
                    continue;
                }

                if (Utils.FindPagedAddress(pageLength, pageSize, key, out var address) == false)
                {
                    continue;
                }

                ref var page = ref pages[address.PageIndex];

                if (page.Remove(address.SlotIndex, key) == false)
                {
                    continue;
                }

                slotCount--;

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
            if (key.IsValid == false)
            {
                Checks.Warning(false, $"Key `{key}` is invalid.");
                return false;
            }

            if (Utils.FindPagedAddress(_pages.Length, _pageSize, key, out var address) == false)
            {
                return false;
            }

            ref var page = ref _pages[address.PageIndex];
            return page.Contains(address.SlotIndex, key);
        }

        public SlotKey UpdateVersion(SlotKey key)
        {
            Checks.Require(key.IsValid, $"Key `{key}` is invalid.");

            if (Utils.FindPagedAddress(_pages.Length, _pageSize, key, out var address) == false)
            {
                Checks.Require(false, $"Cannot update version for `{key}`.");
                return default;
            }

            ref var page = ref _pages[address.PageIndex];
            return page.UpdateVersion(address.SlotIndex, key);
        }

        public bool TryUpdateVersion(SlotKey key, out SlotKey newKey)
        {
            if (key.IsValid == false)
            {
                Checks.Warning(false, $"Key `{key}` is invalid.");
                newKey = key;
                return false;
            }

            if (Utils.FindPagedAddress(_pages.Length, _pageSize, key, out var address) == false)
            {
                Checks.Warning(false, $"Cannot update version for `{key}`.");
                newKey = key;
                return false;
            }

            ref var page = ref _pages[address.PageIndex];
            return page.TryUpdateVersion(address.SlotIndex, key, out newKey);
        }

        /// <summary>
        /// Clear the first page, but remove every other pages.
        /// </summary>
        public void Reset()
        {
            _version++;

            ref var pages = ref _pages;
            var length = (uint)pages.Length;

            if (length > 0)
            {
                ref var firstPage = ref pages[0];
                firstPage.Clear();

                pages = new Page[1] {
                    firstPage
                };
            }

            _freeKeys.Clear();
            _slotCount = 0;
            _tombstoneCount = 0;
        }

        private bool TryGetNewKey(out SlotKey key, out PagedAddress address)
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
            var lastPageSlotCount = lastPage.Count;

            // If the last page is full, try adding a new page
            if (lastPageSlotCount >= pageSize)
            {
                if (TryAddPage() == false)
                {
                    SetDefault(out key, out address);
                    return false;
                }

                lastPageIndex += 1;
                lastPageSlotCount = 0;
            }

            address = new(lastPageIndex, lastPageSlotCount);
            key = new SlotKey(address.ToIndex(_pageSize));
            return true;
        }

        private bool TryReuseFreeKey(out SlotKey key, out PagedAddress address)
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
            address = PagedAddress.FromIndex(key.Index, pageSize);
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
        private static void SetDefault(out SlotKey key, out PagedAddress address)
        {
            key = default;
            address = default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Enumerator GetEnumerator()
            => new Enumerator(this);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        IEnumerator<KeyValuePair<SlotKey, TValue>> IEnumerable<KeyValuePair<SlotKey, TValue>>.GetEnumerator()
            => new Enumerator(this);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        IEnumerator IEnumerable.GetEnumerator()
            => new Enumerator(this);
    }
}
