using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Collections.Extensions.SlotMaps
{
    /// <summary>
    /// A Slot Map is a high-performance associative container
    /// with persistent unique keys to access stored values.
    /// <br/>
    /// Upon insertion, a key is returned that can be used to later access or remove the values.
    /// <br/>
    /// Insertion, removal, and access are all guaranteed to take <c>O(1)</c> time (best, worst, and average case).
    /// <br/>
    /// Great for storing collections of objects that need stable, safe references but have no clear ownership.
    /// </summary>
    /// <remarks>
    /// <typeparamref name="TValue"/> are stored in pages to optimize the memory allocation.
    /// </remarks>
    public partial class SlotMap<TValue> : IPagedSlotMap<TValue>
    {
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
            Checks.Require(pageSize > 0, $"{nameof(pageSize)} must be greater than 0.");

            _pageSize = (uint)Math.Clamp(pageSize, 0, (int)PowerOfTwo.x1_073_741_824);
            _freeIndicesLimit = (uint)Math.Clamp(freeIndicesLimit, 0, pageSize);

            Checks.Require(
                  Utils.IsPowerOfTwo(_pageSize)
                , $"{nameof(pageSize)} must be a power of two."
            );

            Checks.Warning(
                  _freeIndicesLimit <= _pageSize
                , $"{nameof(freeIndicesLimit)} should be lesser than or equal to {_pageSize}."
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
        /// The maximum number of pages that can be allocated.
        /// </summary>
        public uint MaxPageCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _maxPageCount;
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

        public TValue Get(in SlotKey key)
        {
            Checks.Require(key.IsValid, $"Key {key} is invalid.");

            if (Utils.FindPagedAddress(_pages.Length, _pageSize, key, out var address) == false)
            {
                throw new SlotMapException($"Cannot find address for {key}.");
            }

            ref var page = ref _pages[address.PageIndex];
            return page.GetRef(address.SlotIndex, key);
        }

        public void GetRange(
              in ReadOnlySpan<SlotKey> keys
            , in Span<TValue> returnValues
        )
        {
            Checks.Require(
                  returnValues.Length >= keys.Length
                , $"{nameof(returnValues)}.Length must be greater than or equal to {nameof(keys)}.Length."
            );

            var pages = _pages;
            var pageLength = pages.Length;
            var pageSize = _pageSize;
            var length = keys.Length;

            for (var i = 0; i < length; i++)
            {
                ref readonly var key = ref keys[i];

                Checks.Require(key.IsValid, $"Key {key} is invalid.");

                if (Utils.FindPagedAddress(pageLength, pageSize, key, out var address) == false)
                {
                    Checks.Require(false, $"Cannot find address for {key}.");
                    continue;
                }

                ref var page = ref pages[address.PageIndex];
                returnValues[i] = page.GetRef(address.SlotIndex, key);
            }
        }

        public ref readonly TValue GetRef(in SlotKey key)
        {
            Checks.Require(key.IsValid, $"Key {key} is invalid.");

            if (Utils.FindPagedAddress(_pages.Length, _pageSize, key, out var address) == false)
            {
                Checks.Require(false, $"Cannot find address for {key}.");
                return ref Unsafe.NullRef<TValue>();
            }

            ref var page = ref _pages[address.PageIndex];
            return ref page.GetRef(address.SlotIndex, key);
        }

        public ref readonly TValue GetRefNotThrow(in SlotKey key)
        {
            if (key.IsValid == false)
            {
                Checks.Warning(false, $"Key {key} is invalid.");
                return ref Unsafe.NullRef<TValue>();
            }

            if (Utils.FindPagedAddress(_pages.Length, _pageSize, key, out var address) == false)
            {
                Checks.Warning(false, $"Cannot find address for {key}.");
                return ref Unsafe.NullRef<TValue>();
            }

            ref var page = ref _pages[address.PageIndex];
            return ref page.GetRefNotThrow(address.SlotIndex, key);
        }

        public bool TryGet(in SlotKey key, out TValue value)
        {
            if (key.IsValid == false)
            {
                Checks.Warning(false, $"Key {key} is invalid.");
                value = default;
                return false;
            }

            if (Utils.FindPagedAddress(_pages.Length, _pageSize, key, out var address) == false)
            {
                Checks.Warning(false, $"Cannot find address for {key}.");
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
            , in Span<SlotKey> returnKeys
            , in Span<TValue> returnValues
            , out uint returnValuesCount
        )
        {
            if (returnKeys.Length < keys.Length)
            {
                Checks.Warning(false
                    , $"{nameof(returnKeys)}.Length must be greater than or equal to {nameof(keys)}.Length."
                );

                returnValuesCount = 0;
                return false;
            }

            if (returnValues.Length < keys.Length)
            {
                Checks.Require(false
                    , $"{nameof(returnValues)}.Length must be greater than or equal to {nameof(keys)}.Length."
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
                    Checks.Warning(false, $"Key {key} is invalid.");
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

            var resultGetNewKey = TryGetNewKey(out var key, out var address);
            Checks.Require(resultGetNewKey, $"Cannot add {value}.");

            ref var page = ref _pages[address.PageIndex];
            page.Add(address.SlotIndex, key, value);
            _slotCount++;
            return key;
        }

        public void AddRange(
              in ReadOnlySpan<TValue> values
            , in Span<SlotKey> returnKeys
        )
        {
            Checks.Require(
                  returnKeys.Length >= values.Length
                , $"{nameof(returnKeys)}.Length must be greater than or equal to {nameof(values)}.Length."
            );

            _version++;

            var pages = _pages;
            var length = values.Length;

            ref var slotCount = ref _slotCount;

            SetCapacity(slotCount + (uint)length);

            for (var i = 0; i < length; i++)
            {
                ref readonly var value = ref values[i];

                var resultGetNewKey = TryGetNewKey(out var key, out var address);
                Checks.Require(resultGetNewKey, $"Cannot add {value}.");

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
                Checks.Warning(false, $"Cannot add {value}.");
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
            , in Span<SlotKey> returnKeys
            , out uint returnKeyCount
        )
        {
            _version++;

            if (returnKeys.Length < values.Length)
            {
                Checks.Warning(false
                    , $"{nameof(returnKeys)}.Length must be greater than or equal to {nameof(values)}.Length."
                );

                returnKeyCount = 0;
                return false;
            }

            var pages = _pages;
            var length = values.Length;
            var resultIndex = 0;

            ref var slotCount = ref _slotCount;

            if (TrySetCapacity(slotCount + (uint)length) == false)
            {
                returnKeyCount = 0;
                return false;
            }

            for (var i = 0; i < length; i++)
            {
                ref readonly var value = ref values[i];

                if (TryGetNewKey(out var key, out var address) == false)
                {
                    Checks.Warning(false, $"Cannot add {value}.");
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

        public SlotKey Replace(in SlotKey key, TValue value)
        {
            _version++;

            Checks.Require(key.IsValid, $"Key {key} is invalid.");

            if (Utils.FindPagedAddress(_pages.Length, _pageSize, key, out var address) == false)
            {
                Checks.Require(false, $"Cannot replace {value}.");
                return default;
            }

            ref var page = ref _pages[address.PageIndex];
            return page.Replace(address.SlotIndex, key, value);
        }

        public bool TryReplace(in SlotKey key, TValue value, out SlotKey newKey)
        {
            _version++;

            if (key.IsValid == false)
            {
                Checks.Warning(key.IsValid, $"Key {key} is invalid.");
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

        public bool Remove(in SlotKey key)
        {
            _version++;

            if (key.IsValid == false)
            {
                Checks.Warning(key.IsValid, $"Key {key} is invalid.");
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
                    Checks.Warning(key.IsValid, $"Key {key} is invalid.");
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

        public bool Contains(in SlotKey key)
        {
            if (key.IsValid == false)
            {
                Checks.Warning(false, $"Key {key} is invalid.");
                return false;
            }

            if (Utils.FindPagedAddress(_pages.Length, _pageSize, key, out var address) == false)
            {
                return false;
            }

            ref var page = ref _pages[address.PageIndex];
            return page.Contains(address.SlotIndex, key);
        }

        public SlotKey UpdateVersion(in SlotKey key)
        {
            Checks.Require(key.IsValid, $"Key {key} is invalid.");

            if (Utils.FindPagedAddress(_pages.Length, _pageSize, key, out var address) == false)
            {
                Checks.Require(false, $"Cannot update version for {key}.");
                return default;
            }

            ref var page = ref _pages[address.PageIndex];
            return page.UpdateVersion(address.SlotIndex, key);
        }

        public bool TryUpdateVersion(in SlotKey key, out SlotKey newKey)
        {
            if (key.IsValid == false)
            {
                Checks.Warning(false, $"Key {key} is invalid.");
                newKey = key;
                return false;
            }

            if (Utils.FindPagedAddress(_pages.Length, _pageSize, key, out var address) == false)
            {
                Checks.Warning(false, $"Cannot update version for {key}.");
                newKey = key;
                return false;
            }

            ref var page = ref _pages[address.PageIndex];
            return page.TryUpdateVersion(address.SlotIndex, key, out newKey);
        }

        public void SetCapacity(uint newSlotCount)
        {
            Checks.Require(newSlotCount > _slotCount
                , $"New slot count must be greater than the current slot count."
            );

            var newPageCount = CalculateNewPageCount(newSlotCount);

            Checks.Require(newPageCount <= _maxPageCount
                , $"Exceeding the maximum of {_maxPageCount} pages."
            );

            AddPage(newPageCount);
        }

        public bool TrySetCapacity(uint newSlotCount)
        {
            if (newSlotCount <= _slotCount)
            {
                Checks.Warning(false
                    , $"New slot count must be greater than the current slot count."
                );

                return false;
            }

            var newPageCount = CalculateNewPageCount(newSlotCount);

            if (newPageCount > _maxPageCount)
            {
                Checks.Warning(false
                    , $"Exceeding the maximum of {_maxPageCount} pages."
                );

                return false;
            }

            AddPage(newPageCount);
            return true;
        }

        /// <summary>
        /// Clear everything and re-allocate to <paramref name="newSlotCount"/>.
        /// </summary>
        public void Reset(uint newSlotCount)
        {
            _version++;

            var newPageCount = CalculateNewPageCount(newSlotCount);
            _pages = new Page[newPageCount];

            for (var i = 0; i < newPageCount; i++)
            {
                _pages[i] = new Page(_pageSize);
            }

            _freeKeys.Clear();
            _slotCount = 0;
            _tombstoneCount = 0;
        }

        /// <summary>
        /// Clear everything and re-allocate 1 page.
        /// </summary>
        public void Reset()
        {
            _version++;

            _pages = new Page[1] { new Page(_pageSize) };
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
                Checks.Warning(false
                    , $"Cannot allocate more because it is limited {_maxPageCount} pages."
                );

                return false;
            }

            Array.Resize(ref _pages, newPageIndex + 1);

            _pages[newPageIndex] = new Page(_pageSize);

            return true;
        }

        private uint CalculateNewPageCount(uint newSlotCount)
        {
            var newPageCount = newSlotCount / _pageSize;
            var redundant = newSlotCount - (newPageCount * _pageSize);

            if (redundant > 0 || newPageCount == 0)
            {
                newPageCount += 1;
            }

            return newPageCount;
        }

        private void AddPage(uint newPageCount)
        {
            var index = _pages.Length;

            Array.Resize(ref _pages, (int)newPageCount);

            for (; index < newPageCount; index++)
            {
                _pages[index] = new Page(_pageSize);
            }
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
