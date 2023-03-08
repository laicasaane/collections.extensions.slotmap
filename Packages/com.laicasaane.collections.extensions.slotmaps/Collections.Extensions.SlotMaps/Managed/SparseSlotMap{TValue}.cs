using System;
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

        public void GetRange(
              in ReadOnlySpan<SlotKey> keys
            , in Span<TValue> returnValues
        )
        {
            Checks.Require(
                  returnValues.Length >= keys.Length
                , $"{nameof(returnValues)}.Length must be greater than or equal to {nameof(keys)}.Length."
            );

            var metaPages = _metaPages;
            var valuePages = _valuePages;
            var pageLength = metaPages.Length;
            var pageSize = _pageSize;
            var length = keys.Length;

            for (var i = 0; i < length; i++)
            {
                ref readonly var key = ref keys[i];

                Checks.Require(key.IsValid, $"Key {key} is invalid.");

                if (Utils.FindPagedAddress(pageLength, pageSize, key, out var metaAddress) == false)
                {
                    Checks.Require(false, $"Cannot find address for {key}.");
                    continue;
                }

                ref var metaPage = ref metaPages[metaAddress.PageIndex];
                var valueIndex = metaPage.GetValueIndex(metaAddress.SlotIndex, key);
                var valueAddress = PagedAddress.FromIndex(valueIndex, pageSize);
                returnValues[i] = valuePages[valueAddress.PageIndex].GetRef(valueAddress.SlotIndex);
            }
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
                Checks.Warning(false
                    , $"{nameof(returnValues)}.Length must be greater than or equal to {nameof(keys)}.Length."
                );

                returnValuesCount = 0;
                return false;
            }

            var metaPages = _metaPages;
            var valuePages = _valuePages;
            var pageLength = metaPages.Length;
            var pageSize = _pageSize;
            var length = keys.Length;
            var returnIndex = 0;

            for (var i = 0; i < length; i++)
            {
                ref readonly var key = ref keys[i];

                if (key.IsValid == false)
                {
                    Checks.Warning(false, $"Key {key} is invalid.");
                    continue;
                }

                if (Utils.FindPagedAddress(pageLength, pageSize, key, out var metaAddress) == false)
                {
                    Checks.Warning(false, $"Cannot find address for {key} .");
                    continue;
                }

                ref var metaPage = ref metaPages[metaAddress.PageIndex];

                if (metaPage.TryGetValueIndex(metaAddress.SlotIndex, key, out var valueIndex) == false)
                {
                    continue;
                }

                var valueAddress = PagedAddress.FromIndex(valueIndex, pageSize);

                returnKeys[returnIndex] = key;
                returnValues[returnIndex] = valuePages[valueAddress.PageIndex].GetRef(valueAddress.SlotIndex);

                returnIndex++;
            }

            returnValuesCount = (uint)returnIndex;
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

        public void AddRange(
              in ReadOnlySpan<TValue> values
            , in Span<SlotKey> returnKeys
        )
        {
            _version++;

            Checks.Require(
                  returnKeys.Length >= values.Length
                , $"{nameof(returnKeys)}.Length must be greater than or equal to {nameof(values)}.Length."
            );

            var metaPages = _metaPages;
            var valuePages = _valuePages;
            var pageSize = _pageSize;
            var length = values.Length;

            ref var slotCount = ref _slotCount;
            ref var lastValueIndex = ref _lastValueIndex;

            for (var i = 0; i < length; i++)
            {
                ref readonly var value = ref values[i];

                var resultGetNewKey = TryGetNewKey(out var key, out var metaAddress, out var valueIndex);
                Checks.Require(resultGetNewKey, $"Cannot add {value}.");

                ref var metaPage = ref metaPages[metaAddress.PageIndex];
                metaPage.Add(metaAddress.SlotIndex, key, valueIndex);

                var valueAddress = PagedAddress.FromIndex(valueIndex, pageSize);
                ref var valuePage = ref valuePages[valueAddress.PageIndex];
                valuePage.Add(valueAddress.SlotIndex, metaAddress.ToIndex(pageSize), value);

                slotCount++;
                lastValueIndex++;
                returnKeys[i] = key;
            }
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

            var metaPages = _metaPages;
            var valuePages = _valuePages;
            var pageSize = _pageSize;
            var length = values.Length;
            var resultIndex = 0;

            ref var slotCount = ref _slotCount;
            ref var lastValueIndex = ref _lastValueIndex;

            for (var i = 0; i < length; i++)
            {
                ref readonly var value = ref values[i];

                if (TryGetNewKey(out var key, out var metaAddress, out var valueIndex) == false)
                {
                    Checks.Warning(false, $"Cannot add {value}.");
                    continue;
                }

                ref var metaPage = ref metaPages[metaAddress.PageIndex];

                var valueAddress = PagedAddress.FromIndex(valueIndex, pageSize);
                ref var valuePage = ref valuePages[valueAddress.PageIndex];

                if (metaPage.TryAdd(metaAddress.SlotIndex, key, valueIndex) == false)
                {
                    continue;
                }

#if !DISABLE_SLOTMAP_CHECKS
                try
#endif
                {
                    valuePage.Add(valueAddress.SlotIndex, metaAddress.ToIndex(pageSize), value);

                    lastValueIndex++;
                    slotCount++;

                    returnKeys[resultIndex] = key;
                    resultIndex++;
                }
#if !DISABLE_SLOTMAP_CHECKS
                catch (Exception ex)
                {
                    Checks.Require(false, $"The slot map is unexpectedly corrupted.", ex);
                    continue;
                }
#endif
            }

            returnKeyCount = (uint)resultIndex;
            return true;
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

        public void RemoveRange(in ReadOnlySpan<SlotKey> keys)
        {
            _version++;

            var metaPages = _metaPages;
            var valuePages = _valuePages;
            var pageLength = metaPages.Length;
            var pageSize = _pageSize;
            var freeKeys = _freeKeys;
            var length = keys.Length;

            ref var slotCount = ref _slotCount;
            ref var tombstoneCount = ref _tombstoneCount;
            ref var lastValueIndex = ref _lastValueIndex;

            for (var i = 0; i < length; i++)
            {
                ref readonly var key = ref keys[i];

                if (key.IsValid == false)
                {
                    Checks.Warning(false, $"Key {key} is invalid.");
                    continue;
                }

                if (Utils.FindPagedAddress(pageLength, pageSize, key, out var metaAddress) == false)
                {
                    continue;
                }

                ref var metaPage = ref metaPages[metaAddress.PageIndex];

                if (metaPage.Remove(metaAddress.SlotIndex, key, out var valueIndexToRemove) == false)
                {
                    continue;
                }

                // Swap last slot pointed by _lastValueIndex
                // to the slot pointed by valueIndexToRemove

                var indexDest = valueIndexToRemove;
                var indexSrc = lastValueIndex;

                var addressDest = PagedAddress.FromIndex(indexDest, pageSize);
                var addressSrc = PagedAddress.FromIndex(indexSrc, pageSize);

                ref var pageDest = ref valuePages[addressDest.PageIndex];
                ref var pageSrc = ref valuePages[addressSrc.PageIndex];

                pageSrc.Remove(addressSrc.SlotIndex, out var metaIndexToReplace, out var value);
                pageDest.Replace(addressDest.SlotIndex, metaAddress.ToIndex(pageSize), value);

                var metaAddressToReplace = PagedAddress.FromIndex(metaIndexToReplace, pageSize);
                ref var metaPageToReplace = ref metaPages[metaAddressToReplace.PageIndex];
                metaPageToReplace.ReplaceValueIndexUnsafe(metaAddressToReplace.SlotIndex, addressDest.SlotIndex);

                lastValueIndex--;
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

        /// <summary>
        /// Clear the first page, but remove every other pages.
        /// </summary>
        public void Reset()
        {
            _version++;

            ref var metaPages = ref _metaPages;
            ref var valuePages = ref _valuePages;
            var length = (uint)metaPages.Length;

            if (length > 0)
            {
                ref var firstSparsePage = ref metaPages[0];
                ref var firstValuePage = ref valuePages[0];
                
                firstSparsePage.Clear();
                firstValuePage.Clear();

                metaPages = new MetaPage[1] {
                    firstSparsePage
                };

                valuePages = new ValuePage[1] {
                    firstValuePage
                };
            }

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

            var newPageLength = newPageIndex + 1;

            Array.Resize(ref _metaPages, newPageLength);
            Array.Resize(ref _valuePages, newPageLength);

            _metaPages[newPageIndex] = new MetaPage(_pageSize);
            _valuePages[newPageIndex] = new ValuePage(_pageSize);

            return true;
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
