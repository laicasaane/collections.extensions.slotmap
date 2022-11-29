using System;
using System.Runtime.CompilerServices;

namespace Collections.Extensions.SlotMaps
{
    partial class SlotMap<T>
    {
        private struct Page
        {
            private uint _count;
            private uint _tombstoneCount;

            private readonly SlotState[] _states;
            private readonly SlotVersion[] _versions;
            private readonly T[] _items;

            public Page(uint size)
            {
                _states= new SlotState[size];
                _versions = new SlotVersion[size];
                _items = new T[size];
                _count = 0;
                _tombstoneCount = 0;
            }

            public uint Count
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _count;
            }

            public uint TombstoneCount
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _tombstoneCount;
            }

            public ref T GetRef(uint index, SlotKey key)
            {
                ref readonly var state = ref _states[index];

                Checks.Require(state != SlotState.Tombstone
                    , $"Cannot get item because `key` is pointing to a dead slot. "
                    + $"Key value: {key}."
                );

                Checks.Require(state == SlotState.Occupied
                    , $"Cannot get item because `key` is pointing to an empty slot. "
                    + $"Key value: {key}."
                );

                ref readonly var currentVersion = ref _versions[index];

                Checks.Require(currentVersion == key.Version
                    , $"Cannot get item because `key.{nameof(SlotKey.Version)}` "
                    + $"is different from the current version. "
                    + $"Key value: {key}. Current version: {currentVersion}. "
                );

                return ref _items[index];
            }

            public ref T GetRefNotThrow(uint index, SlotKey key)
            {
                ref readonly var state = ref _states[index];

                if (state == SlotState.Tombstone)
                {
                    Checks.Warning(false
                        , $"Cannot get item because `key` is pointing to a dead slot. "
                        + $"Key value: {key}."
                    );

                    return ref Unsafe.NullRef<T>();
                }

                if (state == SlotState.Empty)
                {
                    Checks.Warning(false
                        , $"Cannot get item because `key` is pointing to an empty slot. "
                        + $"Key value: {key}."
                    );

                    return ref Unsafe.NullRef<T>();
                }

                ref readonly var currentVersion = ref _versions[index];

                if (currentVersion != key.Version)
                {
                    Checks.Warning(false
                        , $"Cannot get item because `key.{nameof(SlotKey.Version)}` "
                        + $"is different from the current version. "
                        + $"Key value: {key}. Current version: {currentVersion}."
                    );

                    return ref Unsafe.NullRef<T>();
                }

                return ref _items[index];
            }

            public void Add(uint index, SlotKey key, T item)
            {
                ref var state = ref _states[index];

                Checks.Require(state != SlotState.Tombstone
                    , $"Cannot add item because `key` is pointing to a dead slot. "
                    + $"Key value: {key}."
                );

                Checks.Require(state == SlotState.Empty
                    , $"Cannot add item because `key` is pointing to an occupied slot. "
                    + $"Key value: {key}."
                );

                ref var currentVersion = ref _versions[index];
                var version = key.Version;

                Checks.Require(currentVersion < version
                    , $"Cannot add item because `key.{nameof(SlotKey.Version)}` "
                    + $"is lesser than or equal to the current version. "
                    + $"Key value: {key}. Current version: {currentVersion}."
                );

                _items[index] = item;
                _count++;

                state = SlotState.Occupied;
                currentVersion = version;
            }

            public bool TryAdd(uint index, SlotKey key, T item)
            {
                ref var state = ref _states[index];

                if (state == SlotState.Tombstone)
                {
                    Checks.Warning(false
                        , $"Cannot add item because `key` is pointing to a dead slot. "
                        + $"Key value: {key}."
                    );

                    return false;
                }

                if (state != SlotState.Empty)
                {
                    Checks.Warning(false
                        , $"Cannot add item because `key` is pointing to an occupied slot. "
                        + $"Key value: {key}."
                    );

                    return false;
                }

                ref var currentVersion = ref _versions[index];
                var version = key.Version;

                if (currentVersion >= version)
                {
                    Checks.Warning(false
                        , $"Cannot add item because `key.{nameof(SlotKey.Version)}` "
                        + $"is lesser than or equal to the current version. "
                        + $"Key value: {key}. Current version: {currentVersion}."
                    );

                    return false;
                }

                _items[index] = item;
                _count++;

                state = SlotState.Occupied;
                currentVersion = version;
                return true;
            }

            public bool TryReplace(uint index, SlotKey key, T item, out SlotKey newKey)
            {
                ref var state = ref _states[index];

                if (state == SlotState.Tombstone)
                {
                    Checks.Warning(false
                        , $"Cannot replace item because `key` is pointing to a dead slot. "
                        + $"Key value: {key}."
                    );

                    newKey = default;
                    return false;
                }

                if (state != SlotState.Occupied)
                {
                    Checks.Warning(false
                        , $"Cannot replace item because `key` is pointing to an empty slot. "
                        + $"Key value: {key}."
                    );

                    newKey = default;
                    return false;
                }

                ref var currentVersion = ref _versions[index];
                var version = key.Version;

                if (currentVersion != version)
                {
                    Checks.Warning(false
                        , $"Cannot add item because `key.{nameof(SlotKey.Version)}` "
                        + $"is different from the current version. "
                        + $"Key value: {key}. Current version: {currentVersion}."
                    );

                    newKey = default;
                    return false;
                }

                _items[index] = item;
                currentVersion = version + 1;
                newKey = key.WithVersion(currentVersion);
                return true;
            }

            public bool TryRemove(uint index, SlotKey key)
            {
                ref var state = ref _states[index];

                if (state == SlotState.Tombstone)
                {
                    Checks.Warning(false
                        , $"Cannot remove item because `{nameof(key)}` is pointing to a dead slot. "
                        + $"Key value: {key}."
                    );

                    return true;
                }

                if (state != SlotState.Occupied)
                {
                    Checks.Warning(false
                        , $"Cannot remove item because `{nameof(key)}` is pointing to an empty slot. "
                        + $"Key value: {key}."
                    );

                    return false;
                }

                ref readonly var currentVersion = ref _versions[index];

                if (currentVersion != key.Version)
                {
                    Checks.Warning(false
                        , $"Cannot remove item because the  `key.{nameof(SlotKey.Version)}` "
                        + $"is different from the current version. "
                        + $"Key value: {key}. Current version: {currentVersion}."
                    );

                    return false;
                }

                _items[index] = default;
                _count -= 1;

                if (currentVersion == SlotVersion.MaxValue)
                {
                    state = SlotState.Tombstone;
                    _tombstoneCount++;
                }
                else
                {
                    state = SlotState.Empty;
                }

                return true;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Contains(uint index, SlotKey key)
                => _states[index] == SlotState.Occupied 
                && _versions[index] == key.Version;

            public void Clear()
            {
                Array.Clear(_states, 0, _states.Length);
                Array.Clear(_versions, 0, _versions.Length);

                ClearItems();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void ClearItems()
            {
                if (s_itemIsUnmanaged)
                {
                    Array.Clear(_items, 0, _items.Length);
                }
            }
        }
    }
}
