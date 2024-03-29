﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Collections.Extensions.SlotMaps
{
    /// <summary>
    /// A Sparse Slot Map is a high-performance associative container
    /// with persistent unique keys to access stored values.
    /// <br/>
    /// Upon insertion, a key is returned that can be used to later access or remove the values.
    /// <br/>
    /// Insertion, removal, and access are all guaranteed to take <c>O(1)</c> time (best, worst, and average case).
    /// <br/>
    /// Great for storing collections of objects that need stable, safe references but have no clear ownership.
    /// </summary>
    /// <remarks>
    /// The public APIs of <see cref="SparseSlotMap{TValue}"/> are similar to
    /// <see cref="SlotMap{TValue}"/>, however its internal implementation is based on Sparse Set.
    /// <br/>
    /// <typeparamref name="TValue"/> are stored in pages to optimize the memory allocation.
    /// </remarks>
    public partial class SparseSlotMap<TValue> : IPagedSlotMap<TValue>
    {
        private static readonly bool s_valueIsUnmanaged = RuntimeHelpers.IsReferenceOrContainsReferences<TValue>();

        private readonly uint _pageSize;
        private readonly uint _freeIndicesLimit;
        private readonly uint _maxPageCount;

        private readonly Queue<SlotKey> _freeKeys = new();

        private MetaPage[] _metaPages = Array.Empty<MetaPage>();
        private ValuePage[] _valuePages = Array.Empty<ValuePage>();
        private uint _slotCount;
        private uint _tombstoneCount;
        private long _lastValueIndex;
        private int _version;

        /// <summary></summary>
        /// <param name="pageSize">
        /// <para>The maximum number of values that can be stored in a page.</para>
        /// <para>Must be a power of two.</para>
        /// </param>
        /// <param name="freeIndicesLimit">
        /// <para>The maximum number of indices that was removed and can be free.</para>
        /// <para>Free indices will be reused when their total count exceeds this threshold.</para>
        /// </param>
        public SparseSlotMap(
              int pageSize = (int)PowerOfTwo.x1024
            , int freeIndicesLimit = (int)PowerOfTwo.x32
        )
        {
            Checks.Require(pageSize > 0, $"{nameof(pageSize)} must be greater than 0.");

            _pageSize = (uint)Math.Clamp(pageSize, 0, (int)PowerOfTwo.x1_073_741_824);
            _freeIndicesLimit = (uint)Math.Clamp(freeIndicesLimit, 0, pageSize);

            Checks.Require(
                  Utils.IsPowerOfTwo(_pageSize)
                , $"{nameof(pageSize)} must be a power of two"
            );

            Checks.Warning(
                  _freeIndicesLimit <= _pageSize
                , $"{nameof(freeIndicesLimit)} should be lesser than or equal to {_pageSize}."
            );

            _maxPageCount = Utils.GetMaxPageCount(_pageSize);
            _slotCount = 0;
            _tombstoneCount = 0;
            _lastValueIndex = -1;
            _version = 0;

            TryAddPage();
        }

        /// <summary></summary>
        /// <param name="pageSize">
        /// <para>The maximum number of values that can be stored in a page.</para>
        /// <para>Must be a power of two.</para>
        /// </param>
        /// <param name="freeIndicesLimit">
        /// <para>The maximum number of indices that was removed and can be free.</para>
        /// <para>Free indices will be reused when their total count exceeds this threshold.</para>
        /// </param>
        public SparseSlotMap(
              PowerOfTwo pageSize
            , int freeIndicesLimit = (int)PowerOfTwo.x32
        )
            : this((int)pageSize, freeIndicesLimit)
        { }

        /// <summary>
        /// The maximum number of values that can be stored in a page.
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
            get => _metaPages.Length;
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
        /// The number of stored values.
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

        public ReadOnlyMemory<MetaPage> MetaPages
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _metaPages;
        }

        public ReadOnlyMemory<ValuePage> ValuePages
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _valuePages;
        }

        public TValue Get(in SlotKey key)
        {
            Checks.Require(key.IsValid, $"Key {key} is invalid.");

            var pageSize = _pageSize;
            var metaPages = _metaPages;

            if (Utils.FindPagedAddress(metaPages.Length, pageSize, key, out var metaAddress) == false)
            {
                throw new SlotMapException($"Cannot find address for {key}");
            }

            ref var metaPage = ref metaPages[metaAddress.PageIndex];
            var valueIndex = metaPage.GetValueIndex(metaAddress.SlotIndex, key);
            var valueAddress = PagedAddress.FromIndex(valueIndex, pageSize);
            return _valuePages[valueAddress.PageIndex].GetRef(valueAddress.SlotIndex);
        }

        public ref readonly TValue GetRef(in SlotKey key)
        {
            Checks.Require(key.IsValid, $"Key {key} is invalid.");

            var pageSize = _pageSize;
            var metaPages = _metaPages;

            if (Utils.FindPagedAddress(metaPages.Length, pageSize, key, out var metaAddress) == false)
            {
                Checks.Require(false, $"Cannot find address for {key}.");
                return ref Unsafe.NullRef<TValue>();
            }

            ref var metaPage = ref metaPages[metaAddress.PageIndex];
            var valueIndex = metaPage.GetValueIndex(metaAddress.SlotIndex, key);
            var valueAddress = PagedAddress.FromIndex(valueIndex, pageSize);
            return ref _valuePages[valueAddress.PageIndex].GetRef(valueAddress.SlotIndex);
        }

        public ref readonly TValue GetRefNotThrow(in SlotKey key)
        {
            if (key.IsValid == false)
            {
                Checks.Warning(false, $"Key {key} is invalid.");
                return ref Unsafe.NullRef<TValue>();
            }

            var pageSize = _pageSize;
            var metaPages = _metaPages;

            if (Utils.FindPagedAddress(metaPages.Length, pageSize, key, out var metaAddress) == false)
            {
                Checks.Warning(false, $"Cannot find address for {key}.");
                return ref Unsafe.NullRef<TValue>();
            }

            ref var metaPage = ref metaPages[metaAddress.PageIndex];

            if (metaPage.TryGetValueIndex(metaAddress.SlotIndex, key, out var valueIndex) == false)
            {
                return ref Unsafe.NullRef<TValue>();
            }

            var valueAddress = PagedAddress.FromIndex(valueIndex, pageSize);
            return ref _valuePages[valueAddress.PageIndex].GetRef(valueAddress.SlotIndex);
        }

        public bool TryGet(in SlotKey key, out TValue value)
        {
            if (key.IsValid == false)
            {
                Checks.Warning(false, $"Key {key} is invalid.");
                value = default;
                return false;
            }

            var pageSize = _pageSize;
            var metaPages = _metaPages;

            if (Utils.FindPagedAddress(metaPages.Length, pageSize, key, out var metaAddress) == false)
            {
                Checks.Warning(false, $"Cannot find address for {key} .");
                value = default;
                return false;
            }

            ref var metaPage = ref metaPages[metaAddress.PageIndex];

            if (metaPage.TryGetValueIndex(metaAddress.SlotIndex, key, out var valueIndex) == false)
            {
                value = default;
                return false;
            }

            var valueAddress = PagedAddress.FromIndex(valueIndex, pageSize);
            value = _valuePages[valueAddress.PageIndex].GetRef(valueAddress.SlotIndex);
            return true;
        }

        public SlotKey Add(TValue value)
        {
            _version++;

            var resultGetNewKey = TryGetNewKey(out var key, out var metaAddress, out var valueIndex);
            Checks.Require(resultGetNewKey, $"Cannot add {value}.");

            ref var metaPage = ref _metaPages[metaAddress.PageIndex];
            metaPage.Add(metaAddress.SlotIndex, key, valueIndex);

            var pageSize = _pageSize;
            var valueAddress = PagedAddress.FromIndex(valueIndex, pageSize);
            ref var valuePage = ref _valuePages[valueAddress.PageIndex];
            valuePage.Add(valueAddress.SlotIndex, metaAddress.ToIndex(pageSize), value);

            _lastValueIndex++;
            _slotCount++;
            return key;
        }

        public bool TryAdd(TValue value, out SlotKey key)
        {
            _version++;

            if (TryGetNewKey(out key, out var metaAddress, out var valueIndex) == false)
            {
                Checks.Warning(false, $"Cannot add {value}.");
                return false;
            }

            ref var metaPage = ref _metaPages[metaAddress.PageIndex];

            var pageSize = _pageSize;
            var valueAddress = PagedAddress.FromIndex(valueIndex, _pageSize);
            ref var valuePage = ref _valuePages[valueAddress.PageIndex];

            if (metaPage.TryAdd(metaAddress.SlotIndex, key, valueIndex) == false)
            {
                return false;
            }

#if !DISABLE_SLOTMAP_CHECKS
            try
#endif
            {
                valuePage.Add(valueAddress.SlotIndex, metaAddress.ToIndex(pageSize), value);

                _slotCount++;
                _lastValueIndex++;
                return true;
            }
#if !DISABLE_SLOTMAP_CHECKS
            catch (Exception ex)
            {
                Checks.Require(false, $"The slot map is unexpectedly corrupted.", ex);
                return false;
            }
#endif
        }

        public SlotKey Replace(in SlotKey key, TValue value)
        {
            _version++;

            Checks.Require(key.IsValid, $"Key {key} is invalid.");

            var metaPages = _metaPages;
            var pageSize = _pageSize;
            var pageLength = metaPages.Length;

            if (Utils.FindPagedAddress(pageLength, pageSize, key, out var metaAddress) == false)
            {
                Checks.Require(false, $"Cannot replace {value}.");
                return default;
            }

            ref var metaPage = ref metaPages[metaAddress.PageIndex];
            var valueIndex = metaPage.GetValueIndex(metaAddress.SlotIndex, key);

            var newKey = metaPage.Replace(metaAddress.SlotIndex, key, valueIndex);
            var valueAddress = PagedAddress.FromIndex(valueIndex, pageSize);

            ref var valuePage = ref _valuePages[valueAddress.PageIndex];
            valuePage.Replace(valueAddress.SlotIndex, metaAddress.ToIndex(pageSize), value);

            return newKey;
        }

        public bool TryReplace(in SlotKey key, TValue value, out SlotKey newKey)
        {
            _version++;

            if (key.IsValid == false)
            {
                Checks.Warning(false, $"Key {key} is invalid.");
                newKey = key;
                return false;
            }

            var metaPages = _metaPages;
            var pageSize = _pageSize;
            var pageLength = metaPages.Length;

            if (Utils.FindPagedAddress(pageLength, pageSize, key, out var metaAddress) == false)
            {
                newKey = key;
                return false;
            }

            ref var metaPage = ref metaPages[metaAddress.PageIndex];
            
            if (metaPage.TryGetValueIndex(metaAddress.SlotIndex, key, out var valueIndex) == false)
            {
                newKey = key;
                return false;
            }

            if (metaPage.TryReplace(metaAddress.SlotIndex, key, valueIndex, out newKey) == false)
            {
                return false;
            }

#if !DISABLE_SLOTMAP_CHECKS
            try
#endif
            {
                var valueAddress = PagedAddress.FromIndex(valueIndex, pageSize);
                ref var valuePage = ref _valuePages[valueAddress.PageIndex];
                valuePage.Replace(valueAddress.SlotIndex, metaAddress.ToIndex(pageSize), value);
                return true;
            }
#if !DISABLE_SLOTMAP_CHECKS
            catch (Exception ex)
            {
                Checks.Require(false, $"The slot map is unexpectedly corrupted.", ex);
                return false;
            }
#endif
        }

        public bool Remove(in SlotKey key)
        {
            _version++;

            if (key.IsValid == false)
            {
                Checks.Warning(false, $"Key {key} is invalid.");
                return false;
            }

            var metaPages = _metaPages;
            var pageLength = metaPages.Length;
            var pageSize = _pageSize;

            if (Utils.FindPagedAddress(pageLength, pageSize, key, out var metaAddressToRemove) == false)
            {
                return false;
            }

            ref var metaPage = ref metaPages[metaAddressToRemove.PageIndex];

            if (metaPage.Remove(metaAddressToRemove.SlotIndex, key, out var valueIndexToRemove) == false)
            {
                return false;
            }

#if !DISABLE_SLOTMAP_CHECKS
            try
#endif
            {
                // Swap last slot pointed by _lastValueIndex
                // to the slot pointed by valueIndexToRemove

                var indexDest = valueIndexToRemove;
                var indexSrc = _lastValueIndex;
                var valuePages = _valuePages;

                var addressDest = PagedAddress.FromIndex(indexDest, pageSize);
                var addressSrc = PagedAddress.FromIndex(indexSrc, pageSize);

                ref var pageDest = ref valuePages[addressDest.PageIndex];
                ref var pageSrc = ref valuePages[addressSrc.PageIndex];

                pageSrc.Remove(addressSrc.SlotIndex, out var metaIndexToReplace, out var value);
                pageDest.Replace(addressDest.SlotIndex, metaIndexToReplace, value);

                var metaAddressToReplace = PagedAddress.FromIndex(metaIndexToReplace, pageSize);
                ref var metaPageToReplace = ref metaPages[metaAddressToReplace.PageIndex];
                metaPageToReplace.ReplaceValueIndexUnsafe(metaAddressToReplace.SlotIndex, indexDest);

                _lastValueIndex--;
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
#if !DISABLE_SLOTMAP_CHECKS
            catch (Exception ex)
            {
                Checks.Require(false, $"The slot map is unexpectedly corrupted.", ex);
                return false;
            }
#endif
        }

        public bool Contains(in SlotKey key)
        {
            if (key.IsValid == false)
            {
                Checks.Warning(false, $"Key {key} is invalid.");
                return false;
            }

            if (Utils.FindPagedAddress(_metaPages.Length, _pageSize, key, out var address) == false)
            {
                return false;
            }

            ref var metaPage = ref _metaPages[address.PageIndex];
            return metaPage.Contains(address.SlotIndex, key);
        }

        public SlotKey UpdateVersion(in SlotKey key)
        {
            Checks.Require(key.IsValid, $"Key {key} is invalid.");

            if (Utils.FindPagedAddress(_metaPages.Length, _pageSize, key, out var address) == false)
            {
                Checks.Require(false, $"Cannot update version for {key}.");
                return default;
            }

            ref var metaPage = ref _metaPages[address.PageIndex];
            return metaPage.UpdateVersion(address.SlotIndex, key);
        }

        public bool TryUpdateVersion(in SlotKey key, out SlotKey newKey)
        {
            if (key.IsValid == false)
            {
                Checks.Warning(false, $"Key {key} is invalid.");
                newKey = key;
                return false;
            }

            if (Utils.FindPagedAddress(_metaPages.Length, _pageSize, key, out var address) == false)
            {
                Checks.Warning(false, $"Cannot update version for {key}.");
                newKey = key;
                return false;
            }

            ref var metaPage = ref _metaPages[address.PageIndex];
            return metaPage.TryUpdateVersion(address.SlotIndex, key, out newKey);
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
            _metaPages = new MetaPage[newPageCount];
            _valuePages = new ValuePage[newPageCount];

            var pageSize = _pageSize;
            var metaPages = _metaPages;
            var valuePages = _valuePages;

            for (var i = 0; i < newPageCount; i++)
            {
                metaPages[i] = new MetaPage(pageSize);
                valuePages[i] = new ValuePage(pageSize);
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

            _metaPages = new MetaPage[1] { new MetaPage(_pageSize) };
            _valuePages = new ValuePage[1] { new ValuePage(_pageSize) };
            _freeKeys.Clear();
            _slotCount = 0;
            _tombstoneCount = 0;
            _lastValueIndex = -1;
        }

        private bool TryGetNewKey(
              out SlotKey key
            , out PagedAddress metaAddress
            , out uint valueIndex
        )
        {
            if (TryReuseFreeKey(out key, out metaAddress, out valueIndex))
            {
                return true;
            }

            var pageSize = _pageSize;
            var pages = _metaPages;
            var numberOfPages = (uint)pages.Length;
            var lastPageIndex = numberOfPages - 1;

            ref var lastPage = ref pages[lastPageIndex];
            var lastPageSlotCount = lastPage.Count;

            // If the last page is full, try adding a new page
            if (lastPageSlotCount >= pageSize)
            {
                if (TryAddPage() == false)
                {
                    SetDefault(out key, out metaAddress, out valueIndex);
                    return false;
                }

                lastPageIndex += 1;
                lastPageSlotCount = 0;
            }

            metaAddress = new(lastPageIndex, lastPageSlotCount);
            key = new SlotKey(metaAddress.ToIndex(_pageSize));
            valueIndex = (uint)(_lastValueIndex + 1);
            return true;
        }

        private bool TryReuseFreeKey(
              out SlotKey key
            , out PagedAddress metaAddress
            , out uint valueIndex
        )
        {
            var pageSize = _pageSize;
            var freeKeys = _freeKeys;

            if (freeKeys.Count <= _freeIndicesLimit)
            {
                SetDefault(out key, out metaAddress, out valueIndex);
                return false;
            }

            var oldKey = freeKeys.Dequeue();
            key = oldKey.WithVersion(oldKey.Version + 1);
            metaAddress = PagedAddress.FromIndex(key.Index, pageSize);
            valueIndex = (uint)(_lastValueIndex + 1);
            return true;
        }

        private bool TryAddPage()
        {
            var newPageIndex = _metaPages.Length;

            if (newPageIndex >= _maxPageCount)
            {
                Checks.Warning(false
                    , $"Cannot allocate more because it is limited {_maxPageCount} pages."
                );

                return false;
            }

            var newPageCount = newPageIndex + 1;

            Array.Resize(ref _metaPages, newPageCount);
            Array.Resize(ref _valuePages, newPageCount);

            _metaPages[newPageIndex] = new MetaPage(_pageSize);
            _valuePages[newPageIndex] = new ValuePage(_pageSize);

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
            var index = _metaPages.Length;

            Array.Resize(ref _metaPages, (int)newPageCount);
            Array.Resize(ref _valuePages, (int)newPageCount);

            for (; index < newPageCount; index++)
            {
                _metaPages[index] = new MetaPage(_pageSize);
                _valuePages[index] = new ValuePage(_pageSize);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetDefault(
              out SlotKey key
            , out PagedAddress metaAddress
            , out uint valueIndex
        )
        {
            key = default;
            metaAddress = default;
            valueIndex = default;
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
