using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Collections.Extensions.SlotMaps
{
    public partial class SparseSlotMap<TValue> : ISlotMap<TValue>
    {
        private static readonly string s_name = $"{nameof(SparseSlotMap<TValue>)}<{typeof(TValue).Name}>";
        private static readonly bool s_valueIsUnmanaged = RuntimeHelpers.IsReferenceOrContainsReferences<TValue>();

        private readonly uint _pageSize;
        private readonly uint _freeIndicesLimit;
        private readonly uint _maxPageCount;

        private readonly Queue<SlotKey> _freeKeys = new();

        private SparsePage[] _sparsePages = Array.Empty<SparsePage>();
        private DensePage[] _densePages = Array.Empty<DensePage>();
        private uint _slotCount;
        private uint _tombstoneCount;
        private long _lastDenseIndex;
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
            _lastDenseIndex = -1;
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
            get => _sparsePages.Length;
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

        public ReadOnlyMemory<SparsePage> SparsePages
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _sparsePages;
        }

        public ReadOnlyMemory<DensePage> DensePages
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _densePages;
        }

        public TValue Get(SlotKey key)
        {
            Checks.Require(key.IsValid, $"Key `{key}` is invalid.");

            var pageSize = _pageSize;
            var sparsePages = _sparsePages;

            if (Utils.FindPagedAddress(sparsePages.Length, pageSize, key, out var sparseAddress) == false)
            {
                throw new SlotMapException($"Cannot find address for `{key}`");
            }

            ref var sparsePage = ref sparsePages[sparseAddress.PageIndex];
            var denseIndex = sparsePage.GetDenseIndex(sparseAddress.SlotIndex, key);
            var denseAddress = PagedAddress.FromIndex(denseIndex, pageSize);
            return _densePages[denseAddress.PageIndex].GetRef(denseAddress.SlotIndex);
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

            var sparsePages = _sparsePages;
            var densePages = _densePages;
            var pageLength = sparsePages.Length;
            var pageSize = _pageSize;
            var length = keys.Length;

            for (var i = 0; i < length; i++)
            {
                ref readonly var key = ref keys[i];

                Checks.Require(key.IsValid, $"Key `{key}` is invalid.");

                if (Utils.FindPagedAddress(pageLength, pageSize, key, out var sparseAddress) == false)
                {
                    throw new SlotMapException(
                        $"Cannot find address for `{key}` at index {i}."
                    );
                }

                ref var sparsePage = ref sparsePages[sparseAddress.PageIndex];
                var denseIndex = sparsePage.GetDenseIndex(sparseAddress.SlotIndex, key);
                var denseAddress = PagedAddress.FromIndex(denseIndex, pageSize);
                returnValues[i] = densePages[denseAddress.PageIndex].GetRef(denseAddress.SlotIndex);
            }
        }

        public ref readonly TValue GetRef(SlotKey key)
        {
            Checks.Require(key.IsValid, $"Key `{key}` is invalid.");

            var pageSize = _pageSize;
            var sparsePages = _sparsePages;

            if (Utils.FindPagedAddress(sparsePages.Length, pageSize, key, out var sparseAddress) == false)
            {
                throw new SlotMapException($"Cannot find address for `{key}`.");
            }

            ref var sparsePage = ref sparsePages[sparseAddress.PageIndex];
            var denseIndex = sparsePage.GetDenseIndex(sparseAddress.SlotIndex, key);
            var denseAddress = PagedAddress.FromIndex(denseIndex, pageSize);
            return ref _densePages[denseAddress.PageIndex].GetRef(denseAddress.SlotIndex);
        }

        public ref readonly TValue GetRefNotThrow(SlotKey key)
        {
            if (key.IsValid == false)
            {
                Checks.Warning(false, $"Key `{key}` is invalid.");
                return ref Unsafe.NullRef<TValue>();
            }

            var pageSize = _pageSize;
            var sparsePages = _sparsePages;

            if (Utils.FindPagedAddress(sparsePages.Length, pageSize, key, out var sparseAddress) == false)
            {
                Checks.Warning(false, $"Cannot find address for `{key}`.");
                return ref Unsafe.NullRef<TValue>();
            }

            ref var sparsePage = ref sparsePages[sparseAddress.PageIndex];

            if (sparsePage.TryGetDenseIndex(sparseAddress.SlotIndex, key, out var denseIndex) == false)
            {
                return ref Unsafe.NullRef<TValue>();
            }

            var denseAddress = PagedAddress.FromIndex(denseIndex, pageSize);
            return ref _densePages[denseAddress.PageIndex].GetRef(denseAddress.SlotIndex);
        }

        public bool TryGet(SlotKey key, out TValue value)
        {
            if (key.IsValid == false)
            {
                Checks.Warning(false, $"Key `{key}` is invalid.");
                value = default;
                return false;
            }

            var pageSize = _pageSize;
            var sparsePages = _sparsePages;

            if (Utils.FindPagedAddress(sparsePages.Length, pageSize, key, out var sparseAddress) == false)
            {
                Checks.Warning(false, $"Cannot find address for `{key} `.");
                value = default;
                return false;
            }

            ref var sparsePage = ref sparsePages[sparseAddress.PageIndex];

            if (sparsePage.TryGetDenseIndex(sparseAddress.SlotIndex, key, out var denseIndex) == false)
            {
                value = default;
                return false;
            }

            var denseAddress = PagedAddress.FromIndex(denseIndex, pageSize);
            value = _densePages[denseAddress.PageIndex].GetRef(denseAddress.SlotIndex);
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
                Checks.Warning(false
                    , $"The length `{nameof(returnValues)}` must be greater than "
                    + $"or equal to the length of `{nameof(keys)}`."
                );

                returnValuesCount = 0;
                return false;
            }

            var sparsePages = _sparsePages;
            var densePages = _densePages;
            var pageLength = sparsePages.Length;
            var pageSize = _pageSize;
            var length = keys.Length;
            var returnIndex = 0;

            for (var i = 0; i < length; i++)
            {
                ref readonly var key = ref keys[i];

                if (key.IsValid == false)
                {
                    Checks.Warning(false, $"Key `{key}` is invalid.");
                    continue;
                }

                if (Utils.FindPagedAddress(pageLength, pageSize, key, out var sparseAddress) == false)
                {
                    Checks.Warning(false, $"Cannot find address for `{key} `.");
                    continue;
                }

                ref var sparsePage = ref sparsePages[sparseAddress.PageIndex];

                if (sparsePage.TryGetDenseIndex(sparseAddress.SlotIndex, key, out var denseIndex) == false)
                {
                    continue;
                }

                var denseAddress = PagedAddress.FromIndex(denseIndex, pageSize);

                returnKeys[returnIndex] = key;
                returnValues[returnIndex] = densePages[denseAddress.PageIndex].GetRef(denseAddress.SlotIndex);

                returnIndex++;
            }

            returnValuesCount = (uint)returnIndex;
            return true;
        }

        public SlotKey Add(TValue value)
        {
            _version++;

            if (TryGetNewKey(out var key, out var sparseAddress, out var denseIndex) == false)
            {
                throw new SlotMapException($"Cannot add `{value}` to {s_name}.");
            }

            ref var sparsePage = ref _sparsePages[sparseAddress.PageIndex];
            sparsePage.Add(sparseAddress.SlotIndex, key, denseIndex);

            var pageSize = _pageSize;
            var denseAddress = PagedAddress.FromIndex(denseIndex, pageSize);
            ref var densePage = ref _densePages[denseAddress.PageIndex];
            densePage.Add(denseAddress.SlotIndex, sparseAddress.ToIndex(pageSize), value);

            _lastDenseIndex++;
            _slotCount++;
            return key;
        }

        public void AddRange(
              in ReadOnlySpan<TValue> values
            , Span<SlotKey> returnKeys
        )
        {
            _version++;

            Checks.Require(
                  returnKeys.Length >= values.Length
                , $"The length `{nameof(returnKeys)}` must be greater than "
                + $"or equal to the length of `{nameof(values)}`."
            );

            var sparsePages = _sparsePages;
            var densePages = _densePages;
            var pageSize = _pageSize;
            var length = values.Length;

            ref var slotCount = ref _slotCount;
            ref var lastDenseIndex = ref _lastDenseIndex;

            for (var i = 0; i < length; i++)
            {
                ref readonly var value = ref values[i];

                if (TryGetNewKey(out var key, out var sparseAddress, out var denseIndex) == false)
                {
                    throw new SlotMapException(
                        $"Cannot add `{value}` to {s_name} at index {i}.."
                    );
                }

                ref var sparsePage = ref sparsePages[sparseAddress.PageIndex];
                sparsePage.Add(sparseAddress.SlotIndex, key, denseIndex);

                var denseAddress = PagedAddress.FromIndex(denseIndex, pageSize);
                ref var densePage = ref densePages[denseAddress.PageIndex];
                densePage.Add(denseAddress.SlotIndex, sparseAddress.ToIndex(pageSize), value);

                slotCount++;
                lastDenseIndex++;
                returnKeys[i] = key;
            }
        }

        public bool TryAdd(TValue value, out SlotKey key)
        {
            _version++;

            if (TryGetNewKey(out key, out var sparseAddress, out var denseIndex) == false)
            {
                Checks.Warning(false, $"Cannot add `{value}` to {s_name}.");
                return false;
            }

            ref var sparsePage = ref _sparsePages[sparseAddress.PageIndex];

            var pageSize = _pageSize;
            var denseAddress = PagedAddress.FromIndex(denseIndex, _pageSize);
            ref var densePage = ref _densePages[denseAddress.PageIndex];

            if (sparsePage.TryAdd(sparseAddress.SlotIndex, key, denseIndex) == false)
            {
                return false;
            }

#if !DISABLE_SLOTMAP_CHECKS
            try
#endif
            {
                densePage.Add(denseAddress.SlotIndex, sparseAddress.ToIndex(pageSize), value);

                _slotCount++;
                _lastDenseIndex++;
                return true;
            }
#if !DISABLE_SLOTMAP_CHECKS
            catch (Exception ex)
            {
                Checks.Require(false, $"{s_name} is unexpectedly corrupted.", ex);
                return false;
            }
#endif
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

            var sparsePages = _sparsePages;
            var densePages = _densePages;
            var pageSize = _pageSize;
            var length = values.Length;
            var resultIndex = 0;

            ref var slotCount = ref _slotCount;
            ref var lastDenseIndex = ref _lastDenseIndex;

            for (var i = 0; i < length; i++)
            {
                ref readonly var value = ref values[i];

                if (TryGetNewKey(out var key, out var sparseAddress, out var denseIndex) == false)
                {
                    Checks.Warning(false
                        , $"Cannot add `{value}` to {s_name} at index {i}."
                    );
                    continue;
                }

                ref var sparsePage = ref sparsePages[sparseAddress.PageIndex];

                var denseAddress = PagedAddress.FromIndex(denseIndex, pageSize);
                ref var densePage = ref densePages[denseAddress.PageIndex];

                if (sparsePage.TryAdd(sparseAddress.SlotIndex, key, denseIndex) == false)
                {
                    continue;
                }

#if !DISABLE_SLOTMAP_CHECKS
                try
#endif
                {
                    densePage.Add(denseAddress.SlotIndex, sparseAddress.ToIndex(pageSize), value);

                    lastDenseIndex++;
                    slotCount++;

                    returnKeys[resultIndex] = key;
                    resultIndex++;
                }
#if !DISABLE_SLOTMAP_CHECKS
                catch (Exception ex)
                {
                    Checks.Require(false, $"{s_name} is unexpectedly corrupted.", ex);
                    continue;
                }
#endif
            }

            returnKeyCount = (uint)resultIndex;
            return true;
        }

        public SlotKey Replace(SlotKey key, TValue value)
        {
            _version++;

            Checks.Require(key.IsValid, $"Key `{key}` is invalid.");

            var sparsePages = _sparsePages;
            var pageSize = _pageSize;
            var pageLength = sparsePages.Length;

            if (Utils.FindPagedAddress(pageLength, pageSize, key, out var sparseAddress) == false)
            {
                throw new SlotMapException($"Cannot replace `{value}` in {s_name}.");
            }

            ref var sparsePage = ref sparsePages[sparseAddress.PageIndex];
            var denseIndex = sparsePage.GetDenseIndex(sparseAddress.SlotIndex, key);

            var newKey = sparsePage.Replace(sparseAddress.SlotIndex, key, denseIndex);
            var denseAddress = PagedAddress.FromIndex(denseIndex, pageSize);

            ref var densePage = ref _densePages[denseAddress.PageIndex];
            densePage.Replace(denseAddress.SlotIndex, sparseAddress.ToIndex(pageSize), value);

            return newKey;
        }

        public bool TryReplace(SlotKey key, TValue value, out SlotKey newKey)
        {
            _version++;

            if (key.IsValid == false)
            {
                Checks.Warning(false, $"Key `{key}` is invalid.");
                newKey = key;
                return false;
            }

            var sparsePages = _sparsePages;
            var pageSize = _pageSize;
            var pageLength = sparsePages.Length;

            if (Utils.FindPagedAddress(pageLength, pageSize, key, out var sparseAddress) == false)
            {
                newKey = key;
                return false;
            }

            ref var sparsePage = ref sparsePages[sparseAddress.PageIndex];
            
            if (sparsePage.TryGetDenseIndex(sparseAddress.SlotIndex, key, out var denseIndex) == false)
            {
                newKey = key;
                return false;
            }

            if (sparsePage.TryReplace(sparseAddress.SlotIndex, key, denseIndex, out newKey) == false)
            {
                return false;
            }

#if !DISABLE_SLOTMAP_CHECKS
            try
#endif
            {
                var denseAddress = PagedAddress.FromIndex(denseIndex, pageSize);
                ref var densePage = ref _densePages[denseAddress.PageIndex];
                densePage.Replace(denseAddress.SlotIndex, sparseAddress.ToIndex(pageSize), value);
                return true;
            }
#if !DISABLE_SLOTMAP_CHECKS
            catch (Exception ex)
            {
                Checks.Require(false, $"{s_name} is unexpectedly corrupted.", ex);
                return false;
            }
#endif
        }

        public bool Remove(SlotKey key)
        {
            _version++;

            if (key.IsValid == false)
            {
                Checks.Warning(false, $"Key `{key}` is invalid.");
                return false;
            }

            var sparsePages = _sparsePages;
            var pageLength = sparsePages.Length;
            var pageSize = _pageSize;

            if (Utils.FindPagedAddress(pageLength, pageSize, key, out var sparseAddressToRemove) == false)
            {
                return false;
            }

            ref var sparsePage = ref sparsePages[sparseAddressToRemove.PageIndex];

            if (sparsePage.Remove(sparseAddressToRemove.SlotIndex, key, out var denseIndexToRemove) == false)
            {
                return false;
            }

#if !DISABLE_SLOTMAP_CHECKS
            try
#endif
            {
                // Swap last slot pointed by _lastDenseIndex
                // to the slot pointed by denseIndexToRemove

                var indexDest = denseIndexToRemove;
                var indexSrc = _lastDenseIndex;
                var densePages = _densePages;

                var addressDest = PagedAddress.FromIndex(indexDest, pageSize);
                var addressSrc = PagedAddress.FromIndex(indexSrc, pageSize);

                ref var pageDest = ref densePages[addressDest.PageIndex];
                ref var pageSrc = ref densePages[addressSrc.PageIndex];

                pageSrc.Remove(addressSrc.SlotIndex, out var sparseIndexToReplace, out var value);
                pageDest.Replace(addressDest.SlotIndex, sparseIndexToReplace, value);

                var sparseAddressToReplace = PagedAddress.FromIndex(sparseIndexToReplace, pageSize);
                ref var sparsePageToReplace = ref sparsePages[sparseAddressToReplace.PageIndex];
                sparsePageToReplace.ReplaceDenseIndexUnsafe(sparseAddressToReplace.SlotIndex, indexDest);

                _lastDenseIndex--;
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
                Checks.Require(false, $"{s_name} is unexpectedly corrupted.", ex);
                return false;
            }
#endif
        }

        public void RemoveRange(in ReadOnlySpan<SlotKey> keys)
        {
            _version++;

            var sparsePages = _sparsePages;
            var densePages = _densePages;
            var pageLength = sparsePages.Length;
            var pageSize = _pageSize;
            var freeKeys = _freeKeys;
            var length = keys.Length;

            ref var slotCount = ref _slotCount;
            ref var tombstoneCount = ref _tombstoneCount;
            ref var lastDenseIndex = ref _lastDenseIndex;

            for (var i = 0; i < length; i++)
            {
                ref readonly var key = ref keys[i];

                if (key.IsValid == false)
                {
                    Checks.Warning(false, $"Key `{key}` is invalid.");
                    continue;
                }

                if (Utils.FindPagedAddress(pageLength, pageSize, key, out var sparseAddress) == false)
                {
                    continue;
                }

                ref var sparsePage = ref sparsePages[sparseAddress.PageIndex];

                if (sparsePage.Remove(sparseAddress.SlotIndex, key, out var denseIndexToRemove) == false)
                {
                    continue;
                }

                // Swap last slot pointed by _lastDenseIndex
                // to the slot pointed by denseIndexToRemove

                var indexDest = denseIndexToRemove;
                var indexSrc = lastDenseIndex;

                var addressDest = PagedAddress.FromIndex(indexDest, pageSize);
                var addressSrc = PagedAddress.FromIndex(indexSrc, pageSize);

                ref var pageDest = ref densePages[addressDest.PageIndex];
                ref var pageSrc = ref densePages[addressSrc.PageIndex];

                pageSrc.Remove(addressSrc.SlotIndex, out var sparseIndexToReplace, out var value);
                pageDest.Replace(addressDest.SlotIndex, sparseAddress.ToIndex(pageSize), value);

                var sparseAddressToReplace = PagedAddress.FromIndex(sparseIndexToReplace, pageSize);
                ref var sparsePageToReplace = ref sparsePages[sparseAddressToReplace.PageIndex];
                sparsePageToReplace.ReplaceDenseIndexUnsafe(sparseAddressToReplace.SlotIndex, addressDest.SlotIndex);

                lastDenseIndex--;
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

            if (Utils.FindPagedAddress(_sparsePages.Length, _pageSize, key, out var address) == false)
            {
                return false;
            }

            ref var sparsePage = ref _sparsePages[address.PageIndex];
            return sparsePage.Contains(address.SlotIndex, key);
        }

        public SlotKey UpdateVersion(SlotKey key)
        {
            Checks.Require(key.IsValid, $"Key `{key}` is invalid.");

            if (Utils.FindPagedAddress(_sparsePages.Length, _pageSize, key, out var address) == false)
            {
                Checks.Require(false, $"Cannot update version for `{key}`.");
                return default;
            }

            ref var sparsePage = ref _sparsePages[address.PageIndex];
            return sparsePage.UpdateVersion(address.SlotIndex, key);
        }

        public bool TryUpdateVersion(SlotKey key, out SlotKey newKey)
        {
            if (key.IsValid == false)
            {
                Checks.Warning(false, $"Key `{key}` is invalid.");
                newKey = key;
                return false;
            }

            if (Utils.FindPagedAddress(_sparsePages.Length, _pageSize, key, out var address) == false)
            {
                Checks.Warning(false, $"Cannot update version for `{key}`.");
                newKey = key;
                return false;
            }

            ref var sparsePage = ref _sparsePages[address.PageIndex];
            return sparsePage.TryUpdateVersion(address.SlotIndex, key, out newKey);
        }

        /// <summary>
        /// Clear the first page, but remove every other pages.
        /// </summary>
        public void Reset()
        {
            _version++;

            ref var sparsePages = ref _sparsePages;
            ref var densePages = ref _densePages;
            var length = (uint)sparsePages.Length;

            if (length > 0)
            {
                ref var firstSparsePage = ref sparsePages[0];
                ref var firstDensePage = ref densePages[0];
                
                firstSparsePage.Clear();
                firstDensePage.Clear();

                sparsePages = new SparsePage[1] {
                    firstSparsePage
                };

                densePages = new DensePage[1] {
                    firstDensePage
                };
            }

            _freeKeys.Clear();
            _slotCount = 0;
            _tombstoneCount = 0;
            _lastDenseIndex = -1;
        }

        private bool TryGetNewKey(
              out SlotKey key
            , out PagedAddress sparseAddress
            , out uint denseIndex
        )
        {
            if (TryReuseFreeKey(out key, out sparseAddress, out denseIndex))
            {
                return true;
            }

            var pageSize = _pageSize;
            var pages = _sparsePages;
            var numberOfPages = (uint)pages.Length;
            var lastPageIndex = numberOfPages - 1;

            ref var lastPage = ref pages[lastPageIndex];
            var lastPageSlotCount = lastPage.Count;

            // If the last page is full, try adding a new page
            if (lastPageSlotCount >= pageSize)
            {
                if (TryAddPage() == false)
                {
                    SetDefault(out key, out sparseAddress, out denseIndex);
                    return false;
                }

                lastPageIndex += 1;
                lastPageSlotCount = 0;
            }

            sparseAddress = new(lastPageIndex, lastPageSlotCount);
            key = new SlotKey(sparseAddress.ToIndex(_pageSize));
            denseIndex = (uint)(_lastDenseIndex + 1);
            return true;
        }

        private bool TryReuseFreeKey(
              out SlotKey key
            , out PagedAddress sparseAddress
            , out uint denseIndex
        )
        {
            var pageSize = _pageSize;
            var freeKeys = _freeKeys;

            if (freeKeys.Count <= _freeIndicesLimit)
            {
                SetDefault(out key, out sparseAddress, out denseIndex);
                return false;
            }

            var oldKey = freeKeys.Dequeue();
            key = oldKey.WithVersion(oldKey.Version + 1);
            sparseAddress = PagedAddress.FromIndex(key.Index, pageSize);
            denseIndex = (uint)(_lastDenseIndex + 1);
            return true;
        }

        private bool TryAddPage()
        {
            var newPageIndex = _sparsePages.Length;

            if (newPageIndex >= _maxPageCount)
            {
                Checks.Warning(false,
                      $"Cannot add new page because it has reached "
                    + $"the maximum limit of {_maxPageCount} pages."
                );

                return false;
            }

            var newPageLength = newPageIndex + 1;

            Array.Resize(ref _sparsePages, newPageLength);
            Array.Resize(ref _densePages, newPageLength);

            _sparsePages[newPageIndex] = new SparsePage(_pageSize);
            _densePages[newPageIndex] = new DensePage(_pageSize);

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetDefault(
              out SlotKey key
            , out PagedAddress sparseAddress
            , out uint denseIndex
        )
        {
            key = default;
            sparseAddress = default;
            denseIndex = default;
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
