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
    public partial struct NativeSparseSlotMap<TValue> : ISlotMap<TValue>
        where TValue : unmanaged
    {
        private readonly uint _allocationSize;
        private readonly uint _freeIndicesLimit;
        private readonly uint _maxSlotCount;
        private readonly Allocator _allocator;

        private NativeQueue<SlotKey> _freeKeys;

        private NativeArray<SlotMeta> _metas;
        private NativeArray<TValue> _values;

        /// <summary>
        /// The index of _valueIndices is the index of _metas
        /// <br/>
        /// The value of _valueIndices[i] is the index of _values
        /// </summary>
        private NativeArray<int> _valueIndices;

        /// <summary>
        /// The index of _metaIndices is the index of _values
        /// <br/>
        /// The value of _metaIndices[i] is the index of _metas
        /// </summary>
        private NativeArray<int> _metaIndices;

        private NativeReference<uint> _slotCount;
        private NativeReference<uint> _tombstoneCount;
        private NativeReference<int> _version;
        private NativeReference<int> _lastValueIndex;
        private NativeReference<int> _nextMetaIndexToUse;

        /// <summary></summary>
        /// <param name="allocationSize">
        /// <para>The number of slots that will be allocated at a time.</para>
        /// <para>Must be a power of two.</para>
        /// </param>
        /// <param name="freeIndicesLimit">
        /// <para>The maximum number of indices that was removed and can be free.</para>
        /// <para>Free indices will be reused when their total count exceeds this threshold.</para>
        /// </param>
        public NativeSparseSlotMap(
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
            var maxIndexCount = GetMaxCount<uint>();
            _maxSlotCount = maxMetaCount < maxValueCount ? maxMetaCount : maxValueCount;
            _maxSlotCount = _maxSlotCount < maxIndexCount ? _maxSlotCount : maxIndexCount;

            NativeChecks.Require(
                  allocationSize <= _maxSlotCount
                , $"Allocation size cannot exceed {_maxSlotCount}."
            );

            _allocator = allocator;
            _freeKeys = new NativeQueue<SlotKey>(allocator);

            _metas = new NativeArray<SlotMeta>((int)_allocationSize, allocator);
            _valueIndices = new NativeArray<int>((int)_allocationSize, allocator);

            _values = new NativeArray<TValue>((int)_allocationSize, allocator, NativeArrayOptions.UninitializedMemory);
            _metaIndices = new NativeArray<int>((int)_allocationSize, allocator);

            _slotCount = new NativeReference<uint>(allocator) { Value = 0 };
            _tombstoneCount = new NativeReference<uint>(allocator) { Value = 0 };
            _version = new NativeReference<int>(allocator) { Value = 0 };
            _lastValueIndex = new NativeReference<int>(allocator) { Value = -1 };
            _nextMetaIndexToUse = new NativeReference<int>(allocator) { Value = 0 };
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
        public NativeSparseSlotMap(
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

            if (_metaIndices.IsCreated)
            {
                _metaIndices.Dispose();
            }

            _metaIndices = default;

            if (_valueIndices.IsCreated)
            {
                _valueIndices.Dispose();
            }

            _valueIndices = default;

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

            if (_lastValueIndex.IsCreated)
            {
                _lastValueIndex.Dispose();
            }

            _lastValueIndex = default;

            if (_nextMetaIndexToUse.IsCreated)
            {
                _nextMetaIndexToUse.Dispose();
            }

            _nextMetaIndexToUse = default;
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

        public NativeArray<int>.ReadOnly MetaIndices
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _metaIndices.AsReadOnly();
        }

        public NativeArray<int>.ReadOnly ValueIndices
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _valueIndices.AsReadOnly();
        }

        public TValue Get(in SlotKey key)
        {
            CheckRequiredKeyAndMeta(key);

            var valueIndex = _valueIndices[(int)key.Index];
            return _values[valueIndex];
        }

        public unsafe ref readonly TValue GetRef(in SlotKey key)
        {
            CheckRequiredKeyAndMeta(key);

            var valueIndex = _valueIndices[(int)key.Index];
            return ref _values.GetElementAsRef(valueIndex);
        }

        public ref readonly TValue GetRefNotThrow(in SlotKey key)
        {
            if (ValidateKeyAndMeta(key) == false)
            {
                Checks.Warning(false, $"Key {key} is invalid.");
                return ref Unsafe.NullRef<TValue>();
            }

            var valueIndex = _valueIndices[(int)key.Index];
            return ref _values.GetElementAsRef(valueIndex);
        }

        public bool TryGet(in SlotKey key, out TValue value)
        {
            if (ValidateKeyAndMeta(key) == false)
            {
                value = default;
                return false;
            }

            var valueIndex = _valueIndices[(int)key.Index];
            value = _values[valueIndex];
            return true;
        }

        public SlotKey Add(TValue value)
        {
            _version.Value++;

            var resultGetNewKey = TryGetNewKey(
                  out var key
                , out var metaIndex
                , out var valueIndex
            );

            NativeChecks.Require(resultGetNewKey, $"Cannot add {value}.");

            ref var meta = ref _metas.GetElementAsRef(metaIndex);
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

            meta = new(version, SlotState.Occupied);
            _valueIndices[metaIndex] = valueIndex;

            _metaIndices[valueIndex] = metaIndex;
            _values[valueIndex] = value;

            _slotCount.Value++;
            _nextMetaIndexToUse.Value++;
            _lastValueIndex.Value++;

            return key;
        }

        public bool TryAdd(TValue value, out SlotKey key)
        {
            _version.Value++;

            if (TryGetNewKey(out key, out var metaIndex, out var valueIndex) == false)
            {
                NativeChecks.Warning(false, $"Cannot add {value}.");
                return false;
            }

            ref var meta = ref _metas.GetElementAsRef(metaIndex);
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

            meta = new(version, SlotState.Occupied);
            _valueIndices[metaIndex] = valueIndex;

            _metaIndices[valueIndex] = metaIndex;
            _values[valueIndex] = value;

            _slotCount.Value++;
            _nextMetaIndexToUse.Value++;
            _lastValueIndex.Value++;

            return true;
        }

        public SlotKey Replace(in SlotKey key, TValue value)
        {
            _version.Value++;

            NativeChecks.Require(key.IsValid, $"Key {key} is invalid.");
            NativeChecks.Require(key.Index < _metas.Length, $"Index {key.Index} is out of range.");

            var metaIndex = (int)key.Index;
            ref var meta = ref _metas.GetElementAsRef(metaIndex);

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

            var valueIndex = _valueIndices[metaIndex];
            _values[valueIndex] = value;
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

            var metaIndex = (int)key.Index;
            ref var meta = ref _metas.GetElementAsRef(metaIndex);

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

            var valueIndex = _valueIndices[metaIndex];
            _values[valueIndex] = value;
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

            var metaIndexToRemove = (int)key.Index;
            ref var meta = ref _metas.GetElementAsRef(metaIndexToRemove);

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

            // Swap last slot pointed by _lastValueIndex
            // to the slot pointed by valueIndexToRemove

            var valueIndexToRemove = _valueIndices[metaIndexToRemove];
            var lastValueIndex = _lastValueIndex.Value;

            _values[valueIndexToRemove] = _values[lastValueIndex];
            _values[lastValueIndex] = default;

            var lastMetaIndex = _metaIndices[lastValueIndex];
            _metaIndices[valueIndexToRemove] = lastMetaIndex;
            _valueIndices[lastMetaIndex] = valueIndexToRemove;

            // Change meta at _metas[metaIndexToRemove]
            meta = (currentVersion == SlotVersion.MaxValue)
                ? new(meta, SlotState.Tombstone)
                : new(meta, SlotState.Empty)
                ;

            _slotCount.Value--;
            _lastValueIndex.Value--;

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
            _valueIndices.Grow(allocSize, allocator);

            _values.Grow(allocSize, allocator, NativeArrayOptions.UninitializedMemory);
            _metaIndices.Grow(allocSize, allocator);
        }

        /// <summary>
        /// Clear everything and re-allocate to <paramref name="newSlotCount"/>.
        /// </summary>
        public void Reset(uint newSlotCount)
        {
            _version.Value++;

            var allocSize = (int)CalculateGrowSize(newSlotCount);

            _metas.Dispose();
            _valueIndices.Dispose();
            _values.Dispose();
            _metaIndices.Dispose();
            _freeKeys.Clear();

            _metas = new NativeArray<SlotMeta>(allocSize, _allocator);
            _valueIndices = new NativeArray<int>(allocSize, _allocator);

            _values = new NativeArray<TValue>(allocSize, _allocator, NativeArrayOptions.UninitializedMemory);
            _metaIndices = new NativeArray<int>(allocSize, _allocator);

            _slotCount.Value = 0;
            _tombstoneCount.Value = 0;
            _lastValueIndex.Value = -1;
            _nextMetaIndexToUse.Value = 0;
        }

        /// <summary>
        /// Clear everything and re-allocate to <see cref="AllocationSize"/>.
        /// </summary>
        public void Reset()
        {
            _version.Value++;

            _metas.Dispose();
            _valueIndices.Dispose();
            _values.Dispose();
            _metaIndices.Dispose();
            _freeKeys.Clear();

            _metas = new NativeArray<SlotMeta>((int)_allocationSize, _allocator);
            _valueIndices = new NativeArray<int>((int)_allocationSize, _allocator);

            _values = new NativeArray<TValue>((int)_allocationSize, _allocator, NativeArrayOptions.UninitializedMemory);
            _metaIndices = new NativeArray<int>((int)_allocationSize, _allocator);

            _slotCount.Value = 0;
            _tombstoneCount.Value = 0;
            _lastValueIndex.Value = -1;
            _nextMetaIndexToUse.Value = 0;
        }

        private bool TryGetNewKey(
              out SlotKey key
            , out int metaIndex
            , out int valueIndex
        )
        {
            if (TryReuseFreeKey(out key, out metaIndex, out valueIndex))
            {
                return true;
            }

            var nextMetaIndex = _nextMetaIndexToUse.Value;
            var length = _metas.Length;

            if (nextMetaIndex >= length)
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
                _valueIndices.Grow(allocSize, allocator);

                _values.Grow(allocSize, allocator, NativeArrayOptions.UninitializedMemory);
                _metaIndices.Grow(allocSize, allocator);
            }

            metaIndex = nextMetaIndex;
            key = new SlotKey((uint)metaIndex);
            valueIndex = _lastValueIndex.Value + 1;
            return true;
        }

        private bool TryReuseFreeKey(
              out SlotKey key
            , out int metaIndex
            , out int valueIndex
        )
        {
            if (_freeKeys.Count <= _freeIndicesLimit)
            {
                key = default;
                metaIndex = default;
                valueIndex = default;
                return false;
            }

            var oldKey = _freeKeys.Dequeue();
            key = oldKey.WithVersion(oldKey.Version + 1);
            metaIndex = (int)key.Index;
            valueIndex = _lastValueIndex.Value + 1;
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
