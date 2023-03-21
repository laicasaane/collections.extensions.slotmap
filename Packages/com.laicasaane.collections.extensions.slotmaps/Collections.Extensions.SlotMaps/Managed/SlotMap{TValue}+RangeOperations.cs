using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Collections.Extensions.SlotMaps
{
    partial class SlotMap<TValue>
    {
        public RangeOperations WithRangeOperations()
            => new RangeOperations(this);

        public readonly struct RangeOperations
        {
            private readonly SlotMap<TValue> _slotmap;

            internal RangeOperations(SlotMap<TValue> slotmap)
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

                var pages = _slotmap._pages;
                var pageLength = pages.Length;
                var pageSize = _slotmap._pageSize;
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

            public void GetRange<TReturnValues>(
                  in ReadOnlySpan<SlotKey> keys
                , TReturnValues returnValues
            )
                where TReturnValues : ICollection<TValue>
            {
                Checks.Require(returnValues != null, $"{nameof(returnValues)} is null.");

                var pages = _slotmap._pages;
                var pageLength = pages.Length;
                var pageSize = _slotmap._pageSize;
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
                    returnValues.Add(page.GetRef(address.SlotIndex, key));
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

                var pages = _slotmap._pages;
                var pageLength = pages.Length;
                var pageSize = _slotmap._pageSize;

                foreach (var key in keys)
                {
                    Checks.Require(key.IsValid, $"Key {key} is invalid.");

                    if (Utils.FindPagedAddress(pageLength, pageSize, key, out var address) == false)
                    {
                        Checks.Require(false, $"Cannot find address for {key}.");
                        continue;
                    }

                    ref var page = ref pages[address.PageIndex];
                    returnValues.Add(page.GetRef(address.SlotIndex, key));
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
                    Checks.Require(false
                        , $"{nameof(returnValues)}.Length must be greater than or equal to {nameof(keys)}.Length."
                    );

                    returnValuesCount = 0;
                    return false;
                }

                var pages = _slotmap._pages;
                var pageLength = pages.Length;
                var pageSize = _slotmap._pageSize;
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
                    Checks.Require(false, $"{nameof(returnValues)} is null.");
                    return false;
                }

                var pages = _slotmap._pages;
                var pageLength = pages.Length;
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

                    returnKeys.Add(key);
                    returnValues.Add(valueRef);
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
                    Checks.Require(false, $"{nameof(returnValues)} is null.");
                    return false;
                }

                var pages = _slotmap._pages;
                var pageLength = pages.Length;
                var pageSize = _slotmap._pageSize;

                foreach (var key in keys)
                {
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

                    returnKeys.Add(key);
                    returnValues.Add(valueRef);
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

                var pages = _slotmap._pages;
                var length = values.Length;

                ref var slotCount = ref _slotmap._slotCount;

                _slotmap.SetCapacity(slotCount + (uint)length);

                for (var i = 0; i < length; i++)
                {
                    ref readonly var value = ref values[i];

                    var resultGetNewKey = _slotmap.TryGetNewKey(out var key, out var address);
                    Checks.Require(resultGetNewKey, $"Cannot add {value}.");

                    ref var page = ref pages[address.PageIndex];
                    page.Add(address.SlotIndex, key, value);
                    slotCount++;
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

                var pages = _slotmap._pages;
                var length = values.Length;

                ref var slotCount = ref _slotmap._slotCount;

                _slotmap.SetCapacity(slotCount + (uint)length);

                for (var i = 0; i < length; i++)
                {
                    ref readonly var value = ref values[i];

                    var resultGetNewKey = _slotmap.TryGetNewKey(out var key, out var address);
                    Checks.Require(resultGetNewKey, $"Cannot add {value}.");

                    ref var page = ref pages[address.PageIndex];
                    page.Add(address.SlotIndex, key, value);
                    slotCount++;

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

                var pages = _slotmap._pages;

                ref var slotCount = ref _slotmap._slotCount;

                foreach (var value in values)
                {
                    var resultGetNewKey = _slotmap.TryGetNewKey(out var key, out var address);
                    Checks.Require(resultGetNewKey, $"Cannot add {value}.");

                    ref var page = ref pages[address.PageIndex];
                    page.Add(address.SlotIndex, key, value);
                    slotCount++;

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

                var pages = _slotmap._pages;
                var length = valueCount;

                ref var slotCount = ref _slotmap._slotCount;

                _slotmap.SetCapacity(slotCount + (uint)length);

                foreach (var value in values)
                {
                    var resultGetNewKey = _slotmap.TryGetNewKey(out var key, out var address);
                    Checks.Require(resultGetNewKey, $"Cannot add {value}.");

                    ref var page = ref pages[address.PageIndex];
                    page.Add(address.SlotIndex, key, value);
                    slotCount++;

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

                var pages = _slotmap._pages;
                var length = values.Length;
                var resultIndex = 0;

                ref var slotCount = ref _slotmap._slotCount;

                if (_slotmap.TrySetCapacity(slotCount + (uint)length) == false)
                {
                    returnKeyCount = 0;
                    return false;
                }

                for (var i = 0; i < length; i++)
                {
                    ref readonly var value = ref values[i];

                    if (_slotmap.TryGetNewKey(out var key, out var address) == false)
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

                var pages = _slotmap._pages;
                var length = values.Length;
                var resultIndex = 0;

                ref var slotCount = ref _slotmap._slotCount;

                if (_slotmap.TrySetCapacity(slotCount + (uint)length) == false)
                {
                    return false;
                }

                for (var i = 0; i < length; i++)
                {
                    ref readonly var value = ref values[i];

                    if (_slotmap.TryGetNewKey(out var key, out var address) == false)
                    {
                        Checks.Warning(false, $"Cannot add {value}.");
                        continue;
                    }

                    ref var page = ref pages[address.PageIndex];

                    if (page.TryAdd(address.SlotIndex, key, value))
                    {
                        returnKeys.Add(key);

                        slotCount++;
                        resultIndex++;
                    }
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

                var pages = _slotmap._pages;
                var resultIndex = 0;

                ref var slotCount = ref _slotmap._slotCount;

                foreach (var value in values)
                {
                    if (_slotmap.TryGetNewKey(out var key, out var address) == false)
                    {
                        Checks.Warning(false, $"Cannot add {value}.");
                        continue;
                    }

                    ref var page = ref pages[address.PageIndex];

                    if (page.TryAdd(address.SlotIndex, key, value))
                    {
                        returnKeys.Add(key);

                        slotCount++;
                        resultIndex++;
                    }
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

                var pages = _slotmap._pages;
                var length = valueCount;
                var resultIndex = 0;

                ref var slotCount = ref _slotmap._slotCount;

                if (_slotmap.TrySetCapacity(slotCount + (uint)length) == false)
                {
                    return false;
                }

                foreach (var value in values)
                {
                    if (_slotmap.TryGetNewKey(out var key, out var address) == false)
                    {
                        Checks.Warning(false, $"Cannot add {value}.");
                        continue;
                    }

                    ref var page = ref pages[address.PageIndex];

                    if (page.TryAdd(address.SlotIndex, key, value))
                    {
                        returnKeys.Add(key);

                        slotCount++;
                        resultIndex++;
                    }
                }

                return true;
            }

            public void RemoveRange(in ReadOnlySpan<SlotKey> keys)
            {
                _slotmap._version++;

                var pages = _slotmap._pages;
                var pageLength = pages.Length;
                var pageSize = _slotmap._pageSize;
                var freeKeys = _slotmap._freeKeys;
                var length = keys.Length;

                ref var slotCount = ref _slotmap._slotCount;
                ref var tombstoneCount = ref _slotmap._tombstoneCount;

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

            public void RemoveRange<TKeys>(TKeys keys)
                where TKeys : IEnumerable<SlotKey>
            {
                if (keys == null)
                {
                    Checks.Warning(false, $"{nameof(keys)} is null.");
                }

                _slotmap._version++;

                var pages = _slotmap._pages;
                var pageLength = pages.Length;
                var pageSize = _slotmap._pageSize;
                var freeKeys = _slotmap._freeKeys;

                ref var slotCount = ref _slotmap._slotCount;
                ref var tombstoneCount = ref _slotmap._tombstoneCount;

                foreach (var key in keys)
                {
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
        }
    }
}
