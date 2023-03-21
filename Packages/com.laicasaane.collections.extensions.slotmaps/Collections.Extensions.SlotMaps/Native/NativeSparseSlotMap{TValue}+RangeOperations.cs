#if __SLOTMAP_UNITY_COLLECTIONS__

using Unity.Collections;

namespace Collections.Extensions.SlotMaps
{
    partial struct NativeSparseSlotMap<TValue>
    {
        public RangeOperations WithRangeOperations()
            => new RangeOperations(this);

        public readonly struct RangeOperations
        {
            private readonly NativeSparseSlotMap<TValue> _slotmap;

            internal RangeOperations(in NativeSparseSlotMap<TValue> slotmap)
            {
                _slotmap = slotmap;
            }

            public void GetRange(
                  in NativeArray<SlotKey> keys
                , NativeArray<TValue> returnValues
            )
            {
                GetRange(keys.AsReadOnly(), returnValues);
            }

            public void GetRange(
                  in NativeArray<SlotKey> keys
                , NativeList<TValue> returnValues
            )
            {
                GetRange(keys.AsReadOnly(), returnValues);
            }

            public bool TryGetRange(
                  in NativeArray<SlotKey> keys
                , NativeArray<SlotKey> returnKeys
                , NativeArray<TValue> returnValues
                , out uint returnValuesCount
            )
            {
                return TryGetRange(keys.AsReadOnly(), returnKeys, returnValues, out returnValuesCount);
            }

            public bool TryGetRange(
                  in NativeArray<SlotKey> keys
                , NativeList<SlotKey> returnKeys
                , NativeList<TValue> returnValues
            )
            {
                return TryGetRange(keys.AsReadOnly(), returnKeys, returnValues);
            }

            public void AddRange(
                  in NativeArray<TValue> values
                , NativeArray<SlotKey> returnKeys
            )
            {
                AddRange(values.AsReadOnly(), returnKeys);
            }

            public void AddRange(
                  in NativeArray<TValue> values
                , NativeList<SlotKey> returnKeys
            )
            {
                AddRange(values.AsReadOnly(), returnKeys);
            }

            public bool TryAddRange(
                  in NativeArray<TValue> values
                , NativeArray<SlotKey> returnKeys
                , out uint returnKeyCount
            )
            {
                return TryAddRange(values.AsReadOnly(), returnKeys, out returnKeyCount);
            }

            public bool TryAddRange(
                  in NativeArray<TValue> values
                , NativeList<SlotKey> returnKeys
            )
            {
                return TryAddRange(values.AsReadOnly(), returnKeys);
            }

            public void RemoveRange(in NativeArray<SlotKey> keys)
            {
                RemoveRange(keys.AsReadOnly());
            }

            public void GetRange(
                  in NativeArray<SlotKey>.ReadOnly keys
                , NativeArray<TValue> returnValues
            )
            {
                NativeChecks.Require(
                      returnValues.Length >= keys.Length
                    , $"{nameof(returnValues)}.Length must be greater than or equal to {nameof(keys)}.Length."
                );

                var length = keys.Length;
                ref readonly var slotmap = ref _slotmap;

                for (var i = 0; i < length; i++)
                {
                    returnValues[i] = slotmap.Get(keys[i]);
                }
            }

            public void GetRange(
                  in NativeArray<SlotKey>.ReadOnly keys
                , NativeList<TValue> returnValues
            )
            {
                NativeChecks.Require(
                      returnValues.Length >= keys.Length
                    , $"{nameof(returnValues)}.Length must be greater than or equal to {nameof(keys)}.Length."
                );

                var length = keys.Length;
                ref readonly var slotmap = ref _slotmap;

                for (var i = 0; i < length; i++)
                {
                    returnValues.Add(slotmap.Get(keys[i]));
                }
            }

            public bool TryGetRange(
                  in NativeArray<SlotKey>.ReadOnly keys
                , NativeArray<SlotKey> returnKeys
                , NativeArray<TValue> returnValues
                , out uint returnValuesCount
            )
            {
                if (returnKeys.Length < keys.Length)
                {
                    NativeChecks.Warning(false
                        , $"{nameof(returnKeys)}.Length must be greater than or equal to {nameof(keys)}.Length."
                    );

                    returnValuesCount = 0;
                    return false;
                }

                if (returnValues.Length < keys.Length)
                {
                    NativeChecks.Require(false
                        , $"{nameof(returnValues)}.Length must be greater than or equal to {nameof(keys)}.Length."
                    );

                    returnValuesCount = 0;
                    return false;
                }

                var length = keys.Length;
                var destIndex = 0;
                ref readonly var slotmap = ref _slotmap;

                for (var i = 0; i < length; i++)
                {
                    var key = keys[i];

                    if (slotmap.TryGet(key, out var value))
                    {
                        returnKeys[destIndex] = key;
                        returnValues[destIndex] = value;
                        destIndex++;
                    }
                }

                returnValuesCount = (uint)destIndex;
                return true;
            }

            public bool TryGetRange(
                  in NativeArray<SlotKey>.ReadOnly keys
                , NativeList<SlotKey> returnKeys
                , NativeList<TValue> returnValues
            )
            {
                if (returnKeys.Length < keys.Length)
                {
                    NativeChecks.Warning(false
                        , $"{nameof(returnKeys)}.Length must be greater than or equal to {nameof(keys)}.Length."
                    );

                    return false;
                }

                if (returnValues.Length < keys.Length)
                {
                    NativeChecks.Require(false
                        , $"{nameof(returnValues)}.Length must be greater than or equal to {nameof(keys)}.Length."
                    );

                    return false;
                }

                var length = keys.Length;
                ref readonly var slotmap = ref _slotmap;

                for (var i = 0; i < length; i++)
                {
                    var key = keys[i];

                    if (slotmap.TryGet(key, out var value))
                    {
                        returnKeys.Add(key);
                        returnValues.Add(value);
                    }
                }

                return true;
            }

            public void AddRange(
                  in NativeArray<TValue>.ReadOnly values
                , NativeArray<SlotKey> returnKeys
            )
            {
                NativeChecks.Require(
                      returnKeys.Length >= values.Length
                    , $"{nameof(returnKeys)}.Length must be greater than or equal to {nameof(values)}.Length."
                );

                ref readonly var slotmap = ref _slotmap;
                var version = slotmap._version;
                version.Value++;

                var length = values.Length;

                slotmap.SetCapacity(slotmap._slotCount.Value + (uint)length);

                for (var i = 0; i < length; i++)
                {
                    returnKeys[i] = slotmap.Add(values[i]);
                }
            }

            public void AddRange(
                  in NativeArray<TValue>.ReadOnly values
                , NativeList<SlotKey> returnKeys
            )
            {
                NativeChecks.Require(
                      returnKeys.Length >= values.Length
                    , $"{nameof(returnKeys)}.Length must be greater than or equal to {nameof(values)}.Length."
                );

                ref readonly var slotmap = ref _slotmap;
                var version = slotmap._version;
                version.Value++;

                var length = values.Length;

                slotmap.SetCapacity(slotmap._slotCount.Value + (uint)length);

                for (var i = 0; i < length; i++)
                {
                    returnKeys.Add(slotmap.Add(values[i]));
                }
            }

            public bool TryAddRange(
                  in NativeArray<TValue>.ReadOnly values
                , NativeArray<SlotKey> returnKeys
                , out uint returnKeyCount
            )
            {
                if (returnKeys.Length < values.Length)
                {
                    NativeChecks.Warning(false
                        , $"{nameof(returnKeys)}.Length must be greater than or equal to {nameof(values)}.Length."
                    );

                    returnKeyCount = 0;
                    return false;
                }

                ref readonly var slotmap = ref _slotmap;
                var version = slotmap._version;
                version.Value++;

                var length = values.Length;
                var resultIndex = 0;

                if (slotmap.TrySetCapacity(slotmap._slotCount.Value + (uint)length) == false)
                {
                    returnKeyCount = 0;
                    return false;
                }

                for (var i = 0; i < length; i++)
                {
                    if (slotmap.TryAdd(values[i], out var key))
                    {
                        returnKeys[resultIndex] = key;
                        resultIndex++;
                    }
                }

                returnKeyCount = (uint)resultIndex;
                return true;
            }

            public bool TryAddRange(
                  in NativeArray<TValue>.ReadOnly values
                , NativeList<SlotKey> returnKeys
            )
            {
                if (returnKeys.Length < values.Length)
                {
                    NativeChecks.Warning(false
                        , $"{nameof(returnKeys)}.Length must be greater than or equal to {nameof(values)}.Length."
                    );

                    return false;
                }

                ref readonly var slotmap = ref _slotmap;
                var version = slotmap._version;
                version.Value++;

                var length = values.Length;

                if (slotmap.TrySetCapacity(slotmap._slotCount.Value + (uint)length) == false)
                {
                    return false;
                }

                for (var i = 0; i < length; i++)
                {
                    if (slotmap.TryAdd(values[i], out var key))
                    {
                        returnKeys.Add(key);
                    }
                }

                return true;
            }

            public void RemoveRange(in NativeArray<SlotKey>.ReadOnly keys)
            {
                ref readonly var slotmap = ref _slotmap;
                var version = slotmap._version;
                version.Value++;

                var length = keys.Length;

                for (var i = 0; i < length; i++)
                {
                    slotmap.Remove(keys[i]);
                }
            }
        }
    }
}

#endif
