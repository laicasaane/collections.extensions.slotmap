using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Collections.Extensions.SlotMaps
{
    partial class SparseSlotMap<TValue>
    {
        public RangeOperations WithRangeOperations()
            => new RangeOperations(this);

        public readonly struct RangeOperations
        {
            private readonly SparseSlotMap<TValue> _slotmap;

            internal RangeOperations(SparseSlotMap<TValue> slotmap)
            {
                _slotmap = slotmap ?? throw new ArgumentNullException(nameof(slotmap));
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

                var metaPages = _slotmap._metaPages;
                var valuePages = _slotmap._valuePages;
                var pageLength = metaPages.Length;
                var pageSize = _slotmap._pageSize;
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

            public void GetRange<TReturnValues>(
                  in ReadOnlySpan<SlotKey> keys
                , TReturnValues returnValues
            )
                where TReturnValues : ICollection<TValue>
            {
                Checks.Require(returnValues != null, $"{nameof(returnValues)} is null.");

                var metaPages = _slotmap._metaPages;
                var valuePages = _slotmap._valuePages;
                var pageLength = metaPages.Length;
                var pageSize = _slotmap._pageSize;
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
                    returnValues.Add(valuePages[valueAddress.PageIndex].GetRef(valueAddress.SlotIndex));
                }
            }

            public void GetRange<TKeys, TReturnValues>(
                  TKeys keys
                , TReturnValues returnValues
            )
                where TKeys : IEnumerable<SlotKey>
                where TReturnValues : ICollection<TValue>
            {
                Checks.Require(keys != null, $"{nameof(keys)} is null.");
                Checks.Require(returnValues != null, $"{nameof(returnValues)} is null.");

                var metaPages = _slotmap._metaPages;
                var valuePages = _slotmap._valuePages;
                var pageLength = metaPages.Length;
                var pageSize = _slotmap._pageSize;

                foreach (var key in keys)
                {
                    Checks.Require(key.IsValid, $"Key {key} is invalid.");

                    if (Utils.FindPagedAddress(pageLength, pageSize, key, out var metaAddress) == false)
                    {
                        Checks.Require(false, $"Cannot find address for {key}.");
                        continue;
                    }

                    ref var metaPage = ref metaPages[metaAddress.PageIndex];
                    var valueIndex = metaPage.GetValueIndex(metaAddress.SlotIndex, key);
                    var valueAddress = PagedAddress.FromIndex(valueIndex, pageSize);
                    returnValues.Add(valuePages[valueAddress.PageIndex].GetRef(valueAddress.SlotIndex));
                }
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

                var metaPages = _slotmap._metaPages;
                var valuePages = _slotmap._valuePages;
                var pageLength = metaPages.Length;
                var pageSize = _slotmap._pageSize;
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

            public bool TryGetRange<TReturnKeys, TReturnValues>(
                  in ReadOnlySpan<SlotKey> keys
                , TReturnKeys returnKeys
                , TReturnValues returnValues
            )
                where TReturnKeys : ICollection<SlotKey>
                where TReturnValues : ICollection<TValue>
            {
                if (returnKeys == null)
                {
                    Checks.Warning(false, $"{nameof(returnKeys)} is null.");
                    return false;
                }

                if (returnValues == null)
                {
                    Checks.Warning(false, $"{nameof(returnValues)} is null.");
                    return false;
                }

                var metaPages = _slotmap._metaPages;
                var valuePages = _slotmap._valuePages;
                var pageLength = metaPages.Length;
                var pageSize = _slotmap._pageSize;
                var length = keys.Length;

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

                    returnKeys.Add(key);
                    returnValues.Add(valuePages[valueAddress.PageIndex].GetRef(valueAddress.SlotIndex));
                }

                return true;
            }

            public bool TryGetRange<TKeys, TReturnKeys, TReturnValues>(
                  TKeys keys
                , TReturnKeys returnKeys
                , TReturnValues returnValues
            )
                where TKeys : IEnumerable<SlotKey>
                where TReturnKeys : ICollection<SlotKey>
                where TReturnValues : ICollection<TValue>
            {
                if (keys == null)
                {
                    Checks.Warning(false, $"{nameof(keys)} is null.");
                    return false;
                }

                if (returnKeys == null)
                {
                    Checks.Warning(false, $"{nameof(returnKeys)} is null.");
                    return false;
                }

                if (returnValues == null)
                {
                    Checks.Warning(false, $"{nameof(returnValues)} is null.");
                    return false;
                }

                var metaPages = _slotmap._metaPages;
                var valuePages = _slotmap._valuePages;
                var pageLength = metaPages.Length;
                var pageSize = _slotmap._pageSize;

                foreach (var key in keys)
                {
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

                    returnKeys.Add(key);
                    returnValues.Add(valuePages[valueAddress.PageIndex].GetRef(valueAddress.SlotIndex));
                }

                return true;
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

                _slotmap._version++;

                var metaPages = _slotmap._metaPages;
                var valuePages = _slotmap._valuePages;
                var pageSize = _slotmap._pageSize;
                var length = values.Length;

                ref var slotCount = ref _slotmap._slotCount;
                ref var lastValueIndex = ref _slotmap._lastValueIndex;

                _slotmap.SetCapacity(slotCount + (uint)length);

                for (var i = 0; i < length; i++)
                {
                    ref readonly var value = ref values[i];

                    var resultGetNewKey = _slotmap.TryGetNewKey(out var key, out var metaAddress, out var valueIndex);
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

            public void AddRange<TReturnKeys>(
                  in ReadOnlySpan<TValue> values
                , TReturnKeys returnKeys
            )
                where TReturnKeys : ICollection<SlotKey>
            {
                Checks.Require(returnKeys != null, $"{nameof(returnKeys)} is null.");

                _slotmap._version++;

                var metaPages = _slotmap._metaPages;
                var valuePages = _slotmap._valuePages;
                var pageSize = _slotmap._pageSize;
                var length = values.Length;

                ref var slotCount = ref _slotmap._slotCount;
                ref var lastValueIndex = ref _slotmap._lastValueIndex;

                _slotmap.SetCapacity(slotCount + (uint)length);

                for (var i = 0; i < length; i++)
                {
                    ref readonly var value = ref values[i];

                    var resultGetNewKey = _slotmap.TryGetNewKey(out var key, out var metaAddress, out var valueIndex);
                    Checks.Require(resultGetNewKey, $"Cannot add {value}.");

                    ref var metaPage = ref metaPages[metaAddress.PageIndex];
                    metaPage.Add(metaAddress.SlotIndex, key, valueIndex);

                    var valueAddress = PagedAddress.FromIndex(valueIndex, pageSize);
                    ref var valuePage = ref valuePages[valueAddress.PageIndex];
                    valuePage.Add(valueAddress.SlotIndex, metaAddress.ToIndex(pageSize), value);

                    slotCount++;
                    lastValueIndex++;
                    returnKeys.Add(key);
                }
            }

            public void AddRange<TValues, TReturnKeys>(
                  TValues values
                , int valueCount
                , TReturnKeys returnKeys
            )
                where TValues : IEnumerable<TValue>
                where TReturnKeys : ICollection<SlotKey>
            {
                Checks.Require(values != null, $"{nameof(values)} is null.");
                Checks.Require(returnKeys != null, $"{nameof(returnKeys)} is null.");

                _slotmap._version++;

                var metaPages = _slotmap._metaPages;
                var valuePages = _slotmap._valuePages;
                var pageSize = _slotmap._pageSize;
                var length = valueCount;

                ref var slotCount = ref _slotmap._slotCount;
                ref var lastValueIndex = ref _slotmap._lastValueIndex;

                _slotmap.SetCapacity(slotCount + (uint)length);

                foreach (var value in values)
                {
                    var resultGetNewKey = _slotmap.TryGetNewKey(out var key, out var metaAddress, out var valueIndex);
                    Checks.Require(resultGetNewKey, $"Cannot add {value}.");

                    ref var metaPage = ref metaPages[metaAddress.PageIndex];
                    metaPage.Add(metaAddress.SlotIndex, key, valueIndex);

                    var valueAddress = PagedAddress.FromIndex(valueIndex, pageSize);
                    ref var valuePage = ref valuePages[valueAddress.PageIndex];
                    valuePage.Add(valueAddress.SlotIndex, metaAddress.ToIndex(pageSize), value);

                    slotCount++;
                    lastValueIndex++;
                    returnKeys.Add(key);
                }
            }

            public void AddRange<TValues, TReturnKeys>(
                  TValues values
                , TReturnKeys returnKeys
            )
                where TValues : IEnumerable<TValue>
                where TReturnKeys : ICollection<SlotKey>
            {
                Checks.Require(values != null, $"{nameof(values)} is null.");
                Checks.Require(returnKeys != null, $"{nameof(returnKeys)} is null.");

                _slotmap._version++;

                var metaPages = _slotmap._metaPages;
                var valuePages = _slotmap._valuePages;
                var pageSize = _slotmap._pageSize;

                ref var slotCount = ref _slotmap._slotCount;
                ref var lastValueIndex = ref _slotmap._lastValueIndex;

                foreach (var value in values)
                {
                    var resultGetNewKey = _slotmap.TryGetNewKey(out var key, out var metaAddress, out var valueIndex);
                    Checks.Require(resultGetNewKey, $"Cannot add {value}.");

                    ref var metaPage = ref metaPages[metaAddress.PageIndex];
                    metaPage.Add(metaAddress.SlotIndex, key, valueIndex);

                    var valueAddress = PagedAddress.FromIndex(valueIndex, pageSize);
                    ref var valuePage = ref valuePages[valueAddress.PageIndex];
                    valuePage.Add(valueAddress.SlotIndex, metaAddress.ToIndex(pageSize), value);

                    slotCount++;
                    lastValueIndex++;
                    returnKeys.Add(key);
                }
            }

            public bool TryAddRange(
                  in ReadOnlySpan<TValue> values
                , in Span<SlotKey> returnKeys
                , out uint returnKeyCount
            )
            {
                if (returnKeys.Length < values.Length)
                {
                    Checks.Warning(false
                        , $"{nameof(returnKeys)}.Length must be greater than or equal to {nameof(values)}.Length."
                    );

                    returnKeyCount = 0;
                    return false;
                }

                _slotmap._version++;

                var metaPages = _slotmap._metaPages;
                var valuePages = _slotmap._valuePages;
                var pageSize = _slotmap._pageSize;
                var length = values.Length;
                var resultIndex = 0;

                ref var slotCount = ref _slotmap._slotCount;
                ref var lastValueIndex = ref _slotmap._lastValueIndex;

                if (_slotmap.TrySetCapacity(slotCount + (uint)length) == false)
                {
                    returnKeyCount = 0;
                    return false;
                }

                for (var i = 0; i < length; i++)
                {
                    ref readonly var value = ref values[i];

                    if (_slotmap.TryGetNewKey(out var key, out var metaAddress, out var valueIndex) == false)
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

            public bool TryAddRange<TReturnKeys>(
                  in ReadOnlySpan<TValue> values
                , TReturnKeys returnKeys
            )
                where TReturnKeys : ICollection<SlotKey>
            {
                if (returnKeys == null)
                {
                    Checks.Warning(false, $"{nameof(returnKeys)} is null.");
                    return false;
                }

                _slotmap._version++;

                var metaPages = _slotmap._metaPages;
                var valuePages = _slotmap._valuePages;
                var pageSize = _slotmap._pageSize;
                var length = values.Length;

                ref var slotCount = ref _slotmap._slotCount;
                ref var lastValueIndex = ref _slotmap._lastValueIndex;

                if (_slotmap.TrySetCapacity(slotCount + (uint)length) == false)
                {
                    return false;
                }

                for (var i = 0; i < length; i++)
                {
                    ref readonly var value = ref values[i];

                    if (_slotmap.TryGetNewKey(out var key, out var metaAddress, out var valueIndex) == false)
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

                        returnKeys.Add(key);
                    }
#if !DISABLE_SLOTMAP_CHECKS
                    catch (Exception ex)
                    {
                        Checks.Require(false, $"The slot map is unexpectedly corrupted.", ex);
                        continue;
                    }
#endif
                }

                return true;
            }

            public bool TryAddRange<TValues, TReturnKeys>(
                  TValues values
                , int valueCount
                , TReturnKeys returnKeys
            )
                where TValues : IEnumerable<TValue>
                where TReturnKeys : ICollection<SlotKey>
            {
                if (values == null)
                {
                    Checks.Warning(false, $"{nameof(values)} is null.");
                    return false;
                }

                if (returnKeys == null)
                {
                    Checks.Warning(false, $"{nameof(returnKeys)} is null.");
                    return false;
                }

                _slotmap._version++;

                var metaPages = _slotmap._metaPages;
                var valuePages = _slotmap._valuePages;
                var pageSize = _slotmap._pageSize;
                var length = valueCount;

                ref var slotCount = ref _slotmap._slotCount;
                ref var lastValueIndex = ref _slotmap._lastValueIndex;

                if (_slotmap.TrySetCapacity(slotCount + (uint)length) == false)
                {
                    return false;
                }

                foreach (var value in values)
                {
                    if (_slotmap.TryGetNewKey(out var key, out var metaAddress, out var valueIndex) == false)
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

                        returnKeys.Add(key);
                    }
#if !DISABLE_SLOTMAP_CHECKS
                    catch (Exception ex)
                    {
                        Checks.Require(false, $"The slot map is unexpectedly corrupted.", ex);
                        continue;
                    }
#endif
                }

                return true;
            }

            public bool TryAddRange<TValues, TReturnKeys>(
                  TValues values
                , TReturnKeys returnKeys
            )
                where TValues : IEnumerable<TValue>
                where TReturnKeys : ICollection<SlotKey>
            {
                if (values == null)
                {
                    Checks.Warning(false, $"{nameof(values)} is null.");
                    return false;
                }

                if (returnKeys == null)
                {
                    Checks.Warning(false, $"{nameof(returnKeys)} is null.");
                    return false;
                }

                _slotmap._version++;

                var metaPages = _slotmap._metaPages;
                var valuePages = _slotmap._valuePages;
                var pageSize = _slotmap._pageSize;

                ref var slotCount = ref _slotmap._slotCount;
                ref var lastValueIndex = ref _slotmap._lastValueIndex;

                foreach (var value in values)
                {
                    if (_slotmap.TryGetNewKey(out var key, out var metaAddress, out var valueIndex) == false)
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

                        returnKeys.Add(key);
                    }
#if !DISABLE_SLOTMAP_CHECKS
                    catch (Exception ex)
                    {
                        Checks.Require(false, $"The slot map is unexpectedly corrupted.", ex);
                        continue;
                    }
#endif
                }

                return true;
            }

            public void RemoveRange(in ReadOnlySpan<SlotKey> keys)
            {
                _slotmap._version++;

                var metaPages = _slotmap._metaPages;
                var valuePages = _slotmap._valuePages;
                var pageLength = metaPages.Length;
                var pageSize = _slotmap._pageSize;
                var freeKeys = _slotmap._freeKeys;
                var length = keys.Length;

                ref var slotCount = ref _slotmap._slotCount;
                ref var tombstoneCount = ref _slotmap._tombstoneCount;
                ref var lastValueIndex = ref _slotmap._lastValueIndex;

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

            public void RemoveRange<TKeys>(TKeys keys)
                where TKeys : IEnumerable<SlotKey>
            {
                if (keys == null)
                {
                    Checks.Warning(false, $"{nameof(keys)} is null.");
                }

                _slotmap._version++;

                var metaPages = _slotmap._metaPages;
                var valuePages = _slotmap._valuePages;
                var pageLength = metaPages.Length;
                var pageSize = _slotmap._pageSize;
                var freeKeys = _slotmap._freeKeys;

                ref var slotCount = ref _slotmap._slotCount;
                ref var tombstoneCount = ref _slotmap._tombstoneCount;
                ref var lastValueIndex = ref _slotmap._lastValueIndex;

                foreach (var key in keys)
                {
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
        }
    }
}