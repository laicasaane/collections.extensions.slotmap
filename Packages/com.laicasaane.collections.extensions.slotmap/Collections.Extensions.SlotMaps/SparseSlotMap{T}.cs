using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Collections.Extensions.SlotMaps
{
    public partial class SparseSlotMap<T> : ISlotMap<T>
    {
        private static readonly string s_name = $"{nameof(SparseSlotMap<T>)}<{typeof(T).Name}>";
        private static readonly bool s_itemIsUnmanaged = RuntimeHelpers.IsReferenceOrContainsReferences<T>();

        private readonly uint _pageSize;
        private readonly uint _freeIndicesLimit;
        private readonly uint _maxPageCount;

        private readonly Queue<SlotKey> _freeKeys = new();

        private SparsePage[] _sparsePages = Array.Empty<SparsePage>();
        private DensePage[] _densePages = Array.Empty<DensePage>();
        private uint _itemCount;
        private uint _tombstoneCount;
        private long _lastDenseIndex;
        private int _version;

        /// <summary></summary>
        /// <param name="pageSize">
        /// <para>The maximum number of items that can be stored in a page.</para>
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
            Checks.Require(pageSize > 0, $"`{nameof(pageSize)}` must be greater than 0. Page size value: {pageSize}.");

            _pageSize = (uint)Math.Clamp(pageSize, 0, (int)PowerOfTwo.x1_073_741_824);
            _freeIndicesLimit = (uint)Math.Clamp(freeIndicesLimit, 0, pageSize);

            Checks.Require(
                  Utils.IsPowerOfTwo(_pageSize)
                , $"`{nameof(pageSize)}` must be a power of two. Page size value: {_pageSize}."
            );

            Checks.Warning(
                  _freeIndicesLimit <= _pageSize
                , $"`{nameof(freeIndicesLimit)}` should be lesser than "
                + $"or equal to `{nameof(pageSize)}: {_pageSize}`, "
                + $"or it would be clamped to `{nameof(_pageSize)}`. "
                + $"Free indices limit value: {_freeIndicesLimit}."
            );

            _maxPageCount = Utils.GetMaxPageCount(_pageSize);
            _itemCount = 0;
            _tombstoneCount = 0;
            _lastDenseIndex = -1;
            _version = 0;

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
        public SparseSlotMap(
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
            get => _sparsePages.Length;
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

        public T Get(SlotKey key)
        {
            var pageSize = _pageSize;
            var sparsePages = _sparsePages;

            if (Utils.FindAddress(sparsePages.Length, pageSize, key, out var sparseAddress) == false)
            {
                throw new SlotMapException($"Cannot find address for `{nameof(key)}`. Key value: {key}.");
            }

            ref var sparsePage = ref sparsePages[sparseAddress.PageIndex];
            var denseIndex = sparsePage.GetDenseIndex(sparseAddress.ItemIndex, key);
            var denseAddress = SlotAddress.FromIndex(denseIndex, pageSize);
            return _densePages[denseAddress.PageIndex].GetRef(denseAddress.ItemIndex);
        }

        public void GetRange(
              in ReadOnlySpan<SlotKey> keys
            , Span<T> returnItems
        )
        {
            Checks.Require(
                  returnItems.Length >= keys.Length
                , $"The length `{nameof(returnItems)}` must be greater than "
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

                if (Utils.FindAddress(pageLength, pageSize, key, out var sparseAddress) == false)
                {
                    throw new SlotMapException(
                        $"Cannot find address for `{nameof(key)}` at index {i}. Key value: {key}."
                    );
                }

                ref var sparsePage = ref sparsePages[sparseAddress.PageIndex];
                var denseIndex = sparsePage.GetDenseIndex(sparseAddress.ItemIndex, key);
                var denseAddress = SlotAddress.FromIndex(denseIndex, pageSize);
                returnItems[i] = densePages[denseAddress.PageIndex].GetRef(denseAddress.ItemIndex);
            }
        }

        public ref readonly T GetRef(SlotKey key)
        {
            var pageSize = _pageSize;
            var sparsePages = _sparsePages;

            if (Utils.FindAddress(sparsePages.Length, pageSize, key, out var sparseAddress) == false)
            {
                throw new SlotMapException($"Cannot find address for `{nameof(key)}`. Key value: {key}.");
            }

            ref var sparsePage = ref sparsePages[sparseAddress.PageIndex];
            var denseIndex = sparsePage.GetDenseIndex(sparseAddress.ItemIndex, key);
            var denseAddress = SlotAddress.FromIndex(denseIndex, pageSize);
            return ref _densePages[denseAddress.PageIndex].GetRef(denseAddress.ItemIndex);
        }

        public ref readonly T GetRefNotThrow(SlotKey key)
        {
            var pageSize = _pageSize;
            var sparsePages = _sparsePages;

            if (Utils.FindAddress(sparsePages.Length, pageSize, key, out var sparseAddress) == false)
            {
                Checks.Warning(false, $"Cannot find address for `{nameof(key)}`. Key value: {key}.");
                return ref Unsafe.NullRef<T>();
            }

            ref var sparsePage = ref sparsePages[sparseAddress.PageIndex];

            if (sparsePage.TryGetDenseIndex(sparseAddress.ItemIndex, key, out var denseIndex) == false)
            {
                return ref Unsafe.NullRef<T>();
            }

            var denseAddress = SlotAddress.FromIndex(denseIndex, pageSize);
            return ref _densePages[denseAddress.PageIndex].GetRef(denseAddress.ItemIndex);
        }

        public bool TryGet(SlotKey key, out T item)
        {
            var pageSize = _pageSize;
            var sparsePages = _sparsePages;

            if (Utils.FindAddress(sparsePages.Length, pageSize, key, out var sparseAddress) == false)
            {
                Checks.Warning(false, $"Cannot find address for `{nameof(key)}`. Key value: {key}.");
                item = default;
                return false;
            }

            ref var sparsePage = ref sparsePages[sparseAddress.PageIndex];

            if (sparsePage.TryGetDenseIndex(sparseAddress.ItemIndex, key, out var denseIndex) == false)
            {
                item = default;
                return false;
            }

            var denseAddress = SlotAddress.FromIndex(denseIndex, pageSize);
            item = _densePages[denseAddress.PageIndex].GetRef(denseAddress.ItemIndex);
            return true;
        }

        public bool TryGetRange(
              in ReadOnlySpan<SlotKey> keys
            , Span<SlotKey> returnKeys
            , Span<T> returnItems
            , out uint returnItemCount
        )
        {
            if (returnKeys.Length < keys.Length)
            {
                Checks.Warning(false
                    , $"The length `{nameof(returnKeys)}` must be greater than "
                    + $"or equal to the length of `{nameof(keys)}`."
                );

                returnItemCount = 0;
                return false;
            }

            if (returnItems.Length < keys.Length)
            {
                Checks.Warning(false
                    , $"The length `{nameof(returnItems)}` must be greater than "
                    + $"or equal to the length of `{nameof(keys)}`."
                );

                returnItemCount = 0;
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

                if (Utils.FindAddress(pageLength, pageSize, key, out var sparseAddress) == false)
                {
                    Checks.Warning(false, $"Cannot find address for `{nameof(key)}`. Key value: {key}.");
                    continue;
                }

                ref var sparsePage = ref sparsePages[sparseAddress.PageIndex];

                if (sparsePage.TryGetDenseIndex(sparseAddress.ItemIndex, key, out var denseIndex) == false)
                {
                    continue;
                }

                var denseAddress = SlotAddress.FromIndex(denseIndex, pageSize);

                returnKeys[returnIndex] = key;
                returnItems[returnIndex] = densePages[denseAddress.PageIndex].GetRef(denseAddress.ItemIndex);

                returnIndex++;
            }

            returnItemCount = (uint)returnIndex;
            return true;
        }

        public SlotKey Add(T item)
        {
            _version++;

            if (TryGetNewKey(out var key, out var sparseAddress, out var denseIndex) == false)
            {
                throw new SlotMapException($"Cannot add `{nameof(item)}` to {s_name}. Item value: {item}.");
            }

            ref var sparsePage = ref _sparsePages[sparseAddress.PageIndex];
            sparsePage.Add(sparseAddress.ItemIndex, key, denseIndex);

            var pageSize = _pageSize;
            var denseAddress = SlotAddress.FromIndex(denseIndex, pageSize);
            ref var densePage = ref _densePages[denseAddress.PageIndex];
            densePage.Add(denseAddress.ItemIndex, sparseAddress.ToIndex(pageSize), item);

            _lastDenseIndex++;
            _itemCount++;
            return key;
        }

        public void AddRange(
              in ReadOnlySpan<T> items
            , Span<SlotKey> returnKeys
        )
        {
            _version++;

            Checks.Require(
                  returnKeys.Length >= items.Length
                , $"The length `{nameof(returnKeys)}` must be greater than "
                + $"or equal to the length of `{nameof(items)}`."
            );

            var sparsePages = _sparsePages;
            var densePages = _densePages;
            var pageSize = _pageSize;
            var length = items.Length;

            ref var itemCount = ref _itemCount;
            ref var lastDenseIndex = ref _lastDenseIndex;

            for (var i = 0; i < length; i++)
            {
                ref readonly var item = ref items[i];

                if (TryGetNewKey(out var key, out var sparseAddress, out var denseIndex) == false)
                {
                    throw new SlotMapException(
                        $"Cannot add `{nameof(item)}` to {s_name} at index {i}. Item value: {item}."
                    );
                }

                ref var sparsePage = ref sparsePages[sparseAddress.PageIndex];
                sparsePage.Add(sparseAddress.ItemIndex, key, denseIndex);

                var denseAddress = SlotAddress.FromIndex(denseIndex, pageSize);
                ref var densePage = ref densePages[denseAddress.PageIndex];
                densePage.Add(denseAddress.ItemIndex, sparseAddress.ToIndex(pageSize), item);

                itemCount++;
                lastDenseIndex++;
                returnKeys[i] = key;
            }
        }

        public bool TryAdd(T item, out SlotKey key)
        {
            _version++;

            if (TryGetNewKey(out key, out var sparseAddress, out var denseIndex) == false)
            {
                Checks.Warning(false, $"Cannot add `{nameof(item)}` to {s_name}. Item value: {item}.");
                return false;
            }

            ref var sparsePage = ref _sparsePages[sparseAddress.PageIndex];

            var pageSize = _pageSize;
            var denseAddress = SlotAddress.FromIndex(denseIndex, _pageSize);
            ref var densePage = ref _densePages[denseAddress.PageIndex];

            if (sparsePage.TryAdd(sparseAddress.ItemIndex, key, denseIndex) == false)
            {
                return false;
            }

#if !DISABLE_SLOTMAP_CHECKS
            try
#endif
            {
                densePage.Add(denseAddress.ItemIndex, sparseAddress.ToIndex(pageSize), item);

                _itemCount++;
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
              in ReadOnlySpan<T> items
            , Span<SlotKey> returnKeys
            , out uint returnKeyCount
        )
        {
            _version++;

            if (returnKeys.Length < items.Length)
            {
                Checks.Warning(false
                    , $"The length `{nameof(returnKeys)}` must be greater than "
                    + $"or equal to the length of `{nameof(items)}`."
                );

                returnKeyCount = 0;
                return false;
            }

            var sparsePages = _sparsePages;
            var densePages = _densePages;
            var pageSize = _pageSize;
            var length = items.Length;
            var resultIndex = 0;

            ref var itemCount = ref _itemCount;
            ref var lastDenseIndex = ref _lastDenseIndex;

            for (var i = 0; i < length; i++)
            {
                ref readonly var item = ref items[i];

                if (TryGetNewKey(out var key, out var sparseAddress, out var denseIndex) == false)
                {
                    Checks.Warning(false
                        , $"Cannot add `{nameof(item)}` to {s_name} at index {i}. Item value: {item}."
                    );
                    continue;
                }

                ref var sparsePage = ref sparsePages[sparseAddress.PageIndex];

                var denseAddress = SlotAddress.FromIndex(denseIndex, pageSize);
                ref var densePage = ref densePages[denseAddress.PageIndex];

                if (sparsePage.TryAdd(sparseAddress.ItemIndex, key, denseIndex) == false)
                {
                    continue;
                }

#if !DISABLE_SLOTMAP_CHECKS
                try
#endif
                {
                    densePage.Add(denseAddress.ItemIndex, sparseAddress.ToIndex(pageSize), item);

                    lastDenseIndex++;
                    itemCount++;

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

        public SlotKey Replace(SlotKey key, T item)
        {
            _version++;

            var sparsePages = _sparsePages;
            var pageSize = _pageSize;
            var pageLength = sparsePages.Length;

            if (Utils.FindAddress(pageLength, pageSize, key, out var sparseAddress) == false)
            {
                throw new SlotMapException($"Cannot replace `{nameof(item)}` in {s_name}. Item value: {item}.");
            }

            ref var sparsePage = ref sparsePages[sparseAddress.PageIndex];
            var denseIndex = sparsePage.GetDenseIndex(sparseAddress.ItemIndex, key);

            var newKey = sparsePage.Replace(sparseAddress.ItemIndex, key, denseIndex);
            var denseAddress = SlotAddress.FromIndex(denseIndex, pageSize);

            ref var densePage = ref _densePages[denseAddress.PageIndex];
            densePage.Replace(denseAddress.ItemIndex, sparseAddress.ToIndex(pageSize), item);

            return newKey;
        }

        public bool TryReplace(SlotKey key, T item, out SlotKey newKey)
        {
            var sparsePages = _sparsePages;
            var pageSize = _pageSize;
            var pageLength = sparsePages.Length;

            if (Utils.FindAddress(pageLength, pageSize, key, out var sparseAddress) == false)
            {
                newKey = key;
                return false;
            }

            ref var sparsePage = ref sparsePages[sparseAddress.PageIndex];
            
            if (sparsePage.TryGetDenseIndex(sparseAddress.ItemIndex, key, out var denseIndex) == false)
            {
                newKey = key;
                return false;
            }

            if (sparsePage.TryReplace(sparseAddress.ItemIndex, key, denseIndex, out newKey) == false)
            {
                return false;
            }

#if !DISABLE_SLOTMAP_CHECKS
            try
#endif
            {
                var denseAddress = SlotAddress.FromIndex(denseIndex, pageSize);
                ref var densePage = ref _densePages[denseAddress.PageIndex];
                densePage.Replace(denseAddress.ItemIndex, sparseAddress.ToIndex(pageSize), item);
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

            var sparsePages = _sparsePages;
            var pageLength = sparsePages.Length;
            var pageSize = _pageSize;

            if (Utils.FindAddress(pageLength, pageSize, key, out var sparseAddressToRemove) == false)
            {
                return false;
            }

            ref var sparsePage = ref sparsePages[sparseAddressToRemove.PageIndex];

            if (sparsePage.Remove(sparseAddressToRemove.ItemIndex, key, out var denseIndexToRemove) == false)
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

                var addressDest = SlotAddress.FromIndex(indexDest, pageSize);
                var addressSrc = SlotAddress.FromIndex(indexSrc, pageSize);

                ref var pageDest = ref densePages[addressDest.PageIndex];
                ref var pageSrc = ref densePages[addressSrc.PageIndex];

                pageSrc.Remove(addressSrc.ItemIndex, out var sparseIndexToReplace, out var item);
                pageDest.Replace(addressDest.ItemIndex, sparseIndexToReplace, item);

                var sparseAddressToReplace = SlotAddress.FromIndex(sparseIndexToReplace, pageSize);
                ref var sparsePageToReplace = ref sparsePages[sparseAddressToReplace.PageIndex];
                sparsePageToReplace.ReplaceDenseIndexUnsafe(sparseAddressToReplace.ItemIndex, indexDest);

                _lastDenseIndex--;
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

            ref var itemCount = ref _itemCount;
            ref var tombstoneCount = ref _tombstoneCount;
            ref var lastDenseIndex = ref _lastDenseIndex;

            for (var i = 0; i < length; i++)
            {
                ref readonly var key = ref keys[i];

                if (Utils.FindAddress(pageLength, pageSize, key, out var sparseAddress) == false)
                {
                    continue;
                }

                ref var sparsePage = ref sparsePages[sparseAddress.PageIndex];

                if (sparsePage.Remove(sparseAddress.ItemIndex, key, out var denseIndexToRemove) == false)
                {
                    continue;
                }

                // Swap last slot pointed by _lastDenseIndex
                // to the slot pointed by denseIndexToRemove

                var indexDest = denseIndexToRemove;
                var indexSrc = lastDenseIndex;

                var addressDest = SlotAddress.FromIndex(indexDest, pageSize);
                var addressSrc = SlotAddress.FromIndex(indexSrc, pageSize);

                ref var pageDest = ref densePages[addressDest.PageIndex];
                ref var pageSrc = ref densePages[addressSrc.PageIndex];

                pageSrc.Remove(addressSrc.ItemIndex, out var sparseIndexToReplace, out var item);
                pageDest.Replace(addressDest.ItemIndex, sparseAddress.ToIndex(pageSize), item);

                var sparseAddressToReplace = SlotAddress.FromIndex(sparseIndexToReplace, pageSize);
                ref var sparsePageToReplace = ref sparsePages[sparseAddressToReplace.PageIndex];
                sparsePageToReplace.ReplaceDenseIndexUnsafe(sparseAddressToReplace.ItemIndex, addressDest.ItemIndex);

                lastDenseIndex--;
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
            var sparsePages = _sparsePages;

            if (Utils.FindAddress(sparsePages.Length, _pageSize, key, out var address) == false)
            {
                return false;
            }

            ref var sparsePage = ref sparsePages[address.PageIndex];
            return sparsePage.Contains(address.ItemIndex, key);
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
            _itemCount = 0;
            _tombstoneCount = 0;
            _lastDenseIndex = -1;
        }

        private bool TryGetNewKey(
              out SlotKey key
            , out SlotAddress sparseAddress
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
            var lastPageItemCount = lastPage.Count;

            // If the last page is full, try adding a new page
            if (lastPageItemCount >= pageSize)
            {
                if (TryAddPage() == false)
                {
                    SetDefault(out key, out sparseAddress, out denseIndex);
                    return false;
                }

                lastPageIndex += 1;
                lastPageItemCount = 0;
            }

            sparseAddress = new(lastPageIndex, lastPageItemCount);
            key = new SlotKey(sparseAddress.ToIndex(_pageSize));
            denseIndex = (uint)(_lastDenseIndex + 1);
            return true;
        }

        private bool TryReuseFreeKey(
              out SlotKey key
            , out SlotAddress sparseAddress
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
            sparseAddress = SlotAddress.FromIndex(key.Index, pageSize);
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
            , out SlotAddress sparseAddress
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
        IEnumerator<KeyValuePair<SlotKey, T>> IEnumerable<KeyValuePair<SlotKey, T>>.GetEnumerator()
            => new Enumerator(this);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        IEnumerator IEnumerable.GetEnumerator()
            => new Enumerator(this);
    }
}
