#if __SLOTMAP_UNITY_COLLECTIONS__

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.Collections;

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
    public partial struct NativeSlotMap<TValue> : ISlotMap<TValue>
        where TValue : unmanaged
    {
        private readonly uint _allocationSize;
        private readonly uint _freeIndicesLimit;
        private readonly uint _maxSlotCount;
        private readonly Allocator _allocator;

        private NativeQueue<SlotKey> _freeKeys;
        private NativeArray<SlotMeta> _metas;
        private NativeArray<TValue> _values;

        private NativeReference<uint> _slotCount;
        private NativeReference<uint> _tombstoneCount;
        private NativeReference<int> _version;
        private NativeReference<int> _nextIndexToUse;

        /// <summary></summary>
        /// <param name="allocationSize">
        /// <para>The number of slots that will be allocated at a time.</para>
        /// <para>Must be a power of two.</para>
        /// </param>
        /// <param name="freeIndicesLimit">
        /// <para>The maximum number of indices that was removed and can be free.</para>
        /// <para>Free indices will be reused when their total count exceeds this threshold.</para>
        /// </param>
        public NativeSlotMap(
              Allocator allocator
            , int allocationSize = (int)PowerOfTwo.x1024
            , int freeIndicesLimit = (int)PowerOfTwo.x32
        )
        {
            NativeChecks.Require(
                  allocationSize > 0
                , $"{nameof(allocationSize)} must be greater than 0."
            );

            _allocationSize = (uint)Math.Clamp(allocationSize, 0, (int)PowerOfTwo.x1_073_741_824);
            _freeIndicesLimit = (uint)Math.Clamp(freeIndicesLimit, 0, allocationSize);

            NativeChecks.Require(
                  Utils.IsPowerOfTwo(_allocationSize)
                , $"{nameof(allocationSize)} must be a power of two."
            );

            NativeChecks.Warning(
                  _freeIndicesLimit <= _allocationSize
                , $"{nameof(freeIndicesLimit)} should be lesser than or equal to {_allocationSize}."
            );

            var maxMetaCount = GetMaxCount<SlotMeta>();
            var maxValueCount = GetMaxCount<TValue>();
            _maxSlotCount = maxMetaCount < maxValueCount ? maxMetaCount : maxValueCount;

            NativeChecks.Require(
                  allocationSize <= _maxSlotCount
                , $"Allocation size cannot exceed {_maxSlotCount}."
            );

            _allocator = allocator;
            _metas = new NativeArray<SlotMeta>((int)_allocationSize, allocator);
            _values = new NativeArray<TValue>((int)_allocationSize, allocator, NativeArrayOptions.UninitializedMemory);
            _freeKeys = new NativeQueue<SlotKey>(allocator);

            _slotCount = new NativeReference<uint>(allocator) { Value = 0 };
            _tombstoneCount = new NativeReference<uint>(allocator) { Value = 0 };
            _version = new NativeReference<int>(allocator) { Value = 0 };
            _nextIndexToUse = new NativeReference<int>(allocator) { Value = 0 };
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
        public NativeSlotMap(
              Allocator allocator
            , PowerOfTwo pageSize
            , int freeIndicesLimit = (int)PowerOfTwo.x32
        )
            : this(allocator, (int)pageSize, freeIndicesLimit)
        { }

        public void Dispose()
        {
            if (_freeKeys.IsCreated)
            {
                _freeKeys.Dispose();
            }

            _freeKeys = default;

            if (_metas.IsCreated)
            {
                _metas.Dispose();
            }

            _metas = default;

            if (_values.IsCreated)
            {
                _values.Dispose();
            }

            _values = default;

            if (_slotCount.IsCreated)
            {
                _slotCount.Dispose();
            }

            _slotCount = default;

            if (_tombstoneCount.IsCreated)
            {
                _tombstoneCount.Dispose();
            }

            _tombstoneCount = default;

            if (_version.IsCreated)
            {
                _version.Dispose();
            }

            _version = default;

            if (_nextIndexToUse.IsCreated)
            {
                _nextIndexToUse.Dispose();
            }

            _nextIndexToUse = default;
        }

        public bool IsCreated => _metas.IsCreated;

        /// <summary>
        /// The number of slots that will be allocated at a time.
        /// </summary>
        public uint AllocationSize
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _allocationSize;
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
        /// The number of stored slots.
        /// </summary>
        public uint SlotCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _slotCount.Value;
        }

        /// <summary>
        /// The maximum number of slots that can be allocated.
        /// </summary>
        public uint MaxSlotCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _maxSlotCount;
        }

        /// <summary>
        /// The number of slots that are tombstone and cannot be used anymore.
        /// </summary>
        public uint TombstoneCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _tombstoneCount.Value;
        }

        public NativeArray<SlotMeta>.ReadOnly Metas
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _metas.AsReadOnly();
        }

        public NativeArray<TValue>.ReadOnly Values
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _values.AsReadOnly();
        }

        public TValue Get(in SlotKey key)
        {
            CheckRequiredKeyAndMeta(key);
            return _values[(int)key.Index];
        }

        public void GetRange(
              in ReadOnlySpan<SlotKey> keys
            , in Span<TValue> returnValues
        )
        {
            NativeChecks.Require(
                  returnValues.Length >= keys.Length
                , $"{nameof(returnValues)}.Length must be greater than or equal to {nameof(keys)}.Length."
            );

            var length = keys.Length;

            for (var i = 0; i < length; i++)
            {
                returnValues[i] = Get(keys[i]);
            }
        }

        public unsafe ref readonly TValue GetRef(in SlotKey key)
        {
            CheckRequiredKeyAndMeta(key);
            return ref _values.GetElementAsRef((int)key.Index);
        }

        public ref readonly TValue GetRefNotThrow(in SlotKey key)
        {
            if (ValidateKeyAndMeta(key) == false)
            {
                Checks.Warning(false, $"Key {key} is invalid.");
                return ref Unsafe.NullRef<TValue>();
            }

            return ref _values.GetElementAsRef((int)key.Index);
        }

        public bool TryGet(in SlotKey key, out TValue value)
        {
            if (ValidateKeyAndMeta(key) == false)
            {
                value = default;
                return false;
            }

            value = _values[(int)key.Index];
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

            for (var i = 0; i < length; i++)
            {
                ref readonly var key = ref keys[i];

                if (TryGet(key, out var value))
                {
                    returnKeys[destIndex] = key;
                    returnValues[destIndex] = value;
                    destIndex++;
                }
            }

            returnValuesCount = (uint)destIndex;
            return true;
        }

        public SlotKey Add(TValue value)
        {
            _version.Value++;

            var resultGetNewKey = TryGetNewKey(out var key);
            NativeChecks.Require(resultGetNewKey, $"Cannot add {value}.");

            var index = (int)key.Index;
            ref var meta = ref _metas.GetElementAsRef(index);
            var state = meta.State;

            NativeChecks.Require(state != SlotState.Tombstone
                , $"Key {key} is pointing to a dead slot."
            );

            NativeChecks.Require(state == SlotState.Empty
                , $"Key {key} is pointing to an occupied slot."
            );

            var version = key.Version;

            NativeChecks.Require(meta.Version < version
                , $"Key version {version} is lesser than or equal to the slot version {meta.Version}"
            );

            _values[index] = value;
            meta = new(version, SlotState.Occupied);

            _slotCount.Value++;
            _nextIndexToUse.Value++;

            return key;
        }

        public void AddRange(
              in ReadOnlySpan<TValue> values
            , in Span<SlotKey> returnKeys
        )
        {
            _version.Value++;

            NativeChecks.Require(
                  returnKeys.Length >= values.Length
                , $"{nameof(returnKeys)}.Length must be greater than or equal to {nameof(values)}.Length."
            );

            var length = values.Length;

            SetCapacity(_slotCount.Value + (uint)length);

            for (var i = 0; i < length; i++)
            {
                returnKeys[i] = Add(values[i]);
            }
        }

        public bool TryAdd(TValue value, out SlotKey key)
        {
            _version.Value++;

            if (TryGetNewKey(out key) == false)
            {
                NativeChecks.Warning(false, $"Cannot add {value}.");
                return false;
            }

            var index = (int)key.Index;
            ref var meta = ref _metas.GetElementAsRef(index);
            var state = meta.State;

            if (state == SlotState.Tombstone)
            {
                NativeChecks.Warning(false
                    , $"Key {key} is pointing to a dead slot."
                );

                return false;
            }

            if (state != SlotState.Empty)
            {
                NativeChecks.Warning(false
                    , $"Key {key} is pointing to an occupied slot."
                );

                return false;
            }

            var currentVersion = meta.Version;
            var version = key.Version;

            if (currentVersion >= version)
            {
                NativeChecks.Warning(false
                    , $"Key version {version} is lesser than or equal to the slot version {currentVersion}."
                );

                return false;
            }

            _values[index] = value;
            meta = new(version, SlotState.Occupied);

            _slotCount.Value++;
            _nextIndexToUse.Value++;

            return true;
        }

        public bool TryAddRange(
              in ReadOnlySpan<TValue> values
            , in Span<SlotKey> returnKeys
            , out uint returnKeyCount
        )
        {
            _version.Value++;

            if (returnKeys.Length < values.Length)
            {
                NativeChecks.Warning(false
                    , $"{nameof(returnKeys)}.Length must be greater than or equal to {nameof(values)}.Length."
                );

                returnKeyCount = 0;
                return false;
            }

            var length = values.Length;
            var resultIndex = 0;

            if (TrySetCapacity(_slotCount.Value + (uint)length) == false)
            {
                returnKeyCount = 0;
                return false;
            }

            for (var i = 0; i < length; i++)
            {
                if (TryAdd(values[i], out var key))
                {
                    returnKeys[resultIndex] = key;
                    resultIndex++;
                }
            }

            returnKeyCount = (uint)resultIndex;
            return true;
        }

        public SlotKey Replace(in SlotKey key, TValue value)
        {
            _version.Value++;

            NativeChecks.Require(key.IsValid, $"Key {key} is invalid.");
            NativeChecks.Require(key.Index < _metas.Length, $"Index {key.Index} is out of range.");

            var index = (int)key.Index;
            ref var meta = ref _metas.GetElementAsRef(index);

            NativeChecks.Require(meta.IsValid == true
                , $"Key {key} is pointing to an invalid slot."
            );

            NativeChecks.Require(meta.State != SlotState.Tombstone
                , $"Key {key} is pointing to a dead slot."
            );

            NativeChecks.Require(meta.State == SlotState.Occupied
                , $"Key {key} is pointing to an empty slot."
            );

            var currentVersion = meta.Version;
            var version = key.Version;

            NativeChecks.Require(currentVersion < SlotVersion.MaxValue
                , $"Key version {version} has reached the maximum limit."
            );

            NativeChecks.Require(currentVersion == version
                , $"Key version {version} is not equal to the current version {currentVersion}."
            );

            _values[index] = value;
            currentVersion = version + 1;
            meta = new(meta, currentVersion);

            return key.WithVersion(currentVersion);
        }

        public bool TryReplace(in SlotKey key, TValue value, out SlotKey newKey)
        {
            _version.Value++;

            if (key.IsValid == false)
            {
                NativeChecks.Warning(key.IsValid, $"Key {key} is invalid.");
                newKey = key;
                return false;
            }

            if (key.Index >= _metas.Length)
            {
                NativeChecks.Warning(false, $"Index {key.Index} is out of range.");
                newKey = key;
                return false;
            }

            var index = (int)key.Index;
            ref var meta = ref _metas.GetElementAsRef(index);

            if (meta.IsValid == false)
            {
                NativeChecks.Warning(false
                    , $"Key {key} is pointing to an invalid slot."
                );

                newKey = default;
                return false;
            }

            var state = meta.State;

            if (state == SlotState.Tombstone)
            {
                NativeChecks.Warning(false
                    , $"Key {key} is pointing to a dead slot."
                );

                newKey = default;
                return false;
            }

            if (state != SlotState.Occupied)
            {
                NativeChecks.Warning(false
                    , $"Key {key} is pointing to an empty slot."
                );

                newKey = default;
                return false;
            }

            var currentVersion = meta.Version;
            var version = key.Version;

            if (currentVersion >= SlotVersion.MaxValue)
            {
                NativeChecks.Warning(false
                    , $"Key version {version} has reached the maximum limit."
                );

                newKey = default;
                return false;
            }

            if (currentVersion != version)
            {
                NativeChecks.Warning(false
                    , $"Key version {version} is not equal to the current version {currentVersion}."
                );

                newKey = default;
                return false;
            }

            _values[index] = value;
            currentVersion = version + 1;
            meta = new(meta, currentVersion);
            newKey = key.WithVersion(currentVersion);

            return true;
        }

        public bool Remove(in SlotKey key)
        {
            _version.Value++;

            if (key.IsValid == false)
            {
                NativeChecks.Warning(key.IsValid, $"Key {key} is invalid.");
                return false;
            }

            if (key.Index >= _metas.Length)
            {
                NativeChecks.Warning(false, $"Index {key.Index} is out of range.");
                return false;
            }

            var index = (int)key.Index;
            ref var meta = ref _metas.GetElementAsRef(index);

            if (meta.IsValid == false)
            {
                NativeChecks.Warning(false
                    , $"Key {key} is pointing to an invalid slot."
                );

                return false;
            }

            var state = meta.State;

            if (state == SlotState.Tombstone)
            {
                NativeChecks.Warning(false
                    , $"Key {key} is pointing to a dead slot."
                );

                return false;
            }

            if (state != SlotState.Occupied)
            {
                NativeChecks.Warning(false
                    , $"Key {key} is pointing to an empty slot."
                );

                return false;
            }

            var currentVersion = meta.Version;

            if (currentVersion != key.Version)
            {
                NativeChecks.Warning(false
                    , $"Key version {key.Version} is not equal to the current version{currentVersion}."
                );

                return false;
            }

            _values[index] = default;
            meta = currentVersion == SlotVersion.MaxValue
                ? new(meta, SlotState.Tombstone)
                : new(meta, SlotState.Empty)
                ;

            _slotCount.Value--;

            if (key.Version < SlotVersion.MaxValue)
            {
                _freeKeys.Enqueue(key);
            }
            else
            {
                _tombstoneCount.Value++;
            }

            return true;
        }

        public void RemoveRange(in ReadOnlySpan<SlotKey> keys)
        {
            _version.Value++;

            var length = keys.Length;

            for (var i = 0; i < length; i++)
            {
                Remove(keys[i]);
            }
        }

        public bool Contains(in SlotKey key)
        {
            if (key.IsValid == false || key.Index >= _metas.Length)
            {
                return false;
            }

            var meta = _metas[(int)key.Index];

            return meta.IsValid
                && meta.State == SlotState.Occupied
                && meta.Version == key.Version
                ;
        }

        public SlotKey UpdateVersion(in SlotKey key)
        {
            CheckRequiredKey(key);

            var meta = _metas[(int)key.Index];

            NativeChecks.Require(meta.IsValid
                , $"Key {key} is pointing to an invalid slot."
            );

            NativeChecks.Require(meta.State != SlotState.Tombstone
                , $"Key {key} is pointing to a dead slot."
            );

            NativeChecks.Require(meta.State == SlotState.Occupied
                , $"Key {key} is pointing to an empty slot."
            );

            return key.WithVersion(meta.Version);
        }

        public bool TryUpdateVersion(in SlotKey key, out SlotKey newKey)
        {
            if (key.IsValid == false)
            {
                NativeChecks.Warning(false, $"Key {key} is invalid.");
                newKey = key;
                return false;
            }

            if (key.Index >= _metas.Length)
            {
                NativeChecks.Warning(false, $"Index {key.Index} is out of range.");
                newKey = key;
                return false;
            }

            var index = (int)key.Index;

            if (index >= _metas.Length)
            {
                NativeChecks.Warning(false, $"Index {index} is out of range.");
                newKey = key;
                return false;
            }

            ref var meta = ref _metas.GetElementAsRef(index);

            if (meta.IsValid == false)
            {
                NativeChecks.Warning(false
                    , $"Key {key} is pointing to an invalid slot."
                );

                newKey = default;
                return false;
            }

            var state = meta.State;

            if (state == SlotState.Tombstone)
            {
                NativeChecks.Warning(false
                    , $"Key {key} is pointing to a dead slot."
                );

                newKey = default;
                return false;
            }

            if (state != SlotState.Occupied)
            {
                NativeChecks.Warning(false
                    , $"Key {key} is pointing to an empty slot."
                );

                newKey = default;
                return false;
            }

            newKey = key.WithVersion(meta.Version);
            return true;
        }
        
        public void SetCapacity(uint newSlotCount)
        {
            Checks.Require(newSlotCount > _slotCount.Value
                , $"New slot count must be greater than the current slot count."
            );

            Checks.Require(newSlotCount <= _maxSlotCount
                , $"Exceeding the maximum of {_maxSlotCount} slots."
            );

            Grow(newSlotCount);
        }

        public bool TrySetCapacity(uint newSlotCount)
        {
            if (newSlotCount <= _slotCount.Value)
            {
                Checks.Warning(false
                    , $"New slot count must be greater than the current slot count."
                );

                return false;
            }

            if (newSlotCount > _maxSlotCount)
            {
                Checks.Warning(false
                    , $"Exceeding the maximum of {_maxSlotCount} slots."
                );

                return false;
            }

            Grow(newSlotCount);
            return true;
        }

        private uint CalculateGrowSize(uint newSlotCount)
        {
            var pageCount = newSlotCount / _allocationSize;
            var redundant = newSlotCount - (pageCount * _allocationSize);

            if (redundant > 0 || pageCount == 0)
            {
                pageCount += 1;
            }

            return Math.Clamp(pageCount * _allocationSize, 0, (int)PowerOfTwo.x1_073_741_824);
        }

        private void Grow(uint newSlotCount)
        {
            var allocSize = CalculateGrowSize(newSlotCount);
            var allocator = _allocator;

            _metas.Grow(allocSize, allocator);
            _values.Grow(allocSize, allocator, NativeArrayOptions.UninitializedMemory);
        }

        /// <summary>
        /// Clear everything and re-allocate to <paramref name="newSlotCount"/>.
        /// </summary>
        public void Reset(uint newSlotCount)
        {
            _version.Value++;

            var allocSize = (int)CalculateGrowSize(newSlotCount);

            _metas.Dispose();
            _values.Dispose();
            _freeKeys.Clear();

            _metas = new NativeArray<SlotMeta>(allocSize, _allocator);
            _values = new NativeArray<TValue>(allocSize, _allocator, NativeArrayOptions.UninitializedMemory);

            _slotCount.Value = 0;
            _tombstoneCount.Value = 0;
            _nextIndexToUse.Value = 0;
        }

        /// <summary>
        /// Clear everything and re-allocate to <see cref="AllocationSize"/>.
        /// </summary>
        public void Reset()
        {
            _version.Value++;

            _metas.Dispose();
            _values.Dispose();
            _freeKeys.Clear();

            _metas = new NativeArray<SlotMeta>((int)_allocationSize, _allocator);
            _values = new NativeArray<TValue>((int)_allocationSize, _allocator, NativeArrayOptions.UninitializedMemory);

            _slotCount.Value = 0;
            _tombstoneCount.Value = 0;
            _nextIndexToUse.Value = 0;
        }

        private bool TryGetNewKey(out SlotKey key)
        {
            if (_freeKeys.Count > _freeIndicesLimit)
            {
                var oldKey = _freeKeys.Dequeue();
                key = oldKey.WithVersion(oldKey.Version + 1);
                return true;
            }

            var nextIndex = _nextIndexToUse.Value;
            var length = _metas.Length;

            if (nextIndex >= length)
            {
                var allocator = _allocator;
                var allocSize = _allocationSize;
                var newLength = length + allocSize;

                if (newLength > _maxSlotCount)
                {
                    NativeChecks.Warning(false
                        , $"Cannot allocate more because it is limited to {_maxSlotCount} slots."
                    );

                    key = default;
                    return false;
                }

                _metas.Grow(allocSize, allocator);
                _values.Grow(allocSize, allocator, NativeArrayOptions.UninitializedMemory);
            }

            key = new SlotKey((uint)nextIndex);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Enumerator GetEnumerator()
            => new Enumerator(ref this);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        IEnumerator<KeyValuePair<SlotKey, TValue>> IEnumerable<KeyValuePair<SlotKey, TValue>>.GetEnumerator()
            => new Enumerator(ref this);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        IEnumerator IEnumerable.GetEnumerator()
            => new Enumerator(ref this);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint GetMaxCount<T>() where T : unmanaged
            => (uint)(int.MaxValue / Unsafe.SizeOf<T>());

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckRequiredKey(in SlotKey key)
        {
            NativeChecks.Require(key.IsValid, $"Key {key} is invalid.");
            NativeChecks.Require(key.Index < _metas.Length, $"Index {key.Index} is out of range.");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckRequiredKeyAndMeta(in SlotKey key)
        {
            NativeChecks.Require(key.IsValid, $"Key {key} is invalid.");
            NativeChecks.Require(key.Index < _metas.Length, $"Index {key.Index} is out of range.");

            var meta = _metas[(int)key.Index];

            NativeChecks.Require(meta.IsValid
                , $"Key {key} is pointing to an invalid slot."
            );

            NativeChecks.Require(meta.State != SlotState.Tombstone
                , $"Key {key} is pointing to a dead slot."
            );

            NativeChecks.Require(meta.State == SlotState.Occupied
                , $"Key {key} is pointing to an empty slot."
            );

            NativeChecks.Require(meta.Version == key.Version
                , $"Key version {key.Version} is not equal to the slot version {meta.Version}."
            );
        }

        private bool ValidateKeyAndMeta(in SlotKey key)
        {
            if (key.IsValid == false)
            {
                NativeChecks.Warning(false, $"Key {key} is invalid.");
                return false;
            }

            if (key.Index >= _metas.Length)
            {
                NativeChecks.Warning(false, $"Index {key.Index} is out of range.");
                return false;
            }

            var meta = _metas[(int)key.Index];

            if (meta.IsValid == false)
            {
                NativeChecks.Warning(false
                    , $"Key {key} is pointing to an invalid slot."
                );

                return false;
            }

            var state = meta.State;

            if (state == SlotState.Tombstone)
            {
                NativeChecks.Warning(false
                    , $"Key {key} is pointing to a dead slot."
                );

                return false;
            }

            if (state == SlotState.Empty)
            {
                NativeChecks.Warning(false
                    , $"Key {key} is pointing to an empty slot."
                );

                return false;
            }

            if (meta.Version != key.Version)
            {
                NativeChecks.Warning(false
                    , $"Key version {key.Version} is not equal to the slot version {meta.Version}."
                );

                return false;
            }

            return true;
        }
    }
}

#endif
