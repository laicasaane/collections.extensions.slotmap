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

            private readonly SlotMeta[] _metas;
            private readonly T[] _items;

            public Page(uint size)
            {
                _metas = new SlotMeta[size];
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
                ref readonly var meta = ref _metas[index];

                Checks.Require(meta.IsValid
                    , $"Cannot get item because `{nameof(key)}` is pointing to an invalid slot. "
                    + $"Key value: {key}."
                );

                var state = meta.State;

                Checks.Require(state != SlotState.Tombstone
                    , $"Cannot get item because `{nameof(key)}` is pointing to a dead slot. "
                    + $"Key value: {key}."
                );

                Checks.Require(state == SlotState.Occupied
                    , $"Cannot get item because `{nameof(key)}` is pointing to an empty slot. "
                    + $"Key value: {key}."
                );

                var version = meta.Version;

                Checks.Require(version == key.Version
                    , $"Cannot get item because `key.{nameof(SlotKey.Version)}` "
                    + $"is different from the current version. "
                    + $"Key value: {key}. Current version: {version}. "
                );

                return ref _items[index];
            }

            public ref T GetRefNotThrow(uint index, SlotKey key)
            {
                ref readonly var meta = ref _metas[index];

                if (meta.IsValid == false)
                {
                    Checks.Warning(false
                        , $"Cannot get item because `{nameof(key)}` is pointing to an invalid slot. "
                        + $"Key value: {key}."
                    );

                    return ref Unsafe.NullRef<T>();
                }

                var state = meta.State;

                if (state == SlotState.Tombstone)
                {
                    Checks.Warning(false
                        , $"Cannot get item because `{nameof(key)}` is pointing to a dead slot. "
                        + $"Key value: {key}."
                    );

                    return ref Unsafe.NullRef<T>();
                }

                if (state == SlotState.Empty)
                {
                    Checks.Warning(false
                        , $"Cannot get item because `{nameof(key)}` is pointing to an empty slot. "
                        + $"Key value: {key}."
                    );

                    return ref Unsafe.NullRef<T>();
                }

                var currentVersion = meta.Version;

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
                ref var meta = ref _metas[index];
                var state = meta.State;

                Checks.Require(state != SlotState.Tombstone
                    , $"Cannot add item because `{nameof(key)}` is pointing to a dead slot. "
                    + $"Key value: {key}."
                );

                Checks.Require(state == SlotState.Empty
                    , $"Cannot add item because `{nameof(key)}` is pointing to an occupied slot. "
                    + $"Key value: {key}."
                );

                var currentVersion = meta.Version;
                var version = key.Version;

                Checks.Require(currentVersion < version
                    , $"Cannot add item because `key.{nameof(SlotKey.Version)}` "
                    + $"is lesser than or equal to the current version. "
                    + $"Key value: {key}. Current version: {currentVersion}."
                );

                _items[index] = item;
                _count++;

                meta = new(version, SlotState.Occupied);
            }

            public bool TryAdd(uint index, SlotKey key, T item)
            {
                ref var meta = ref _metas[index];
                var state = meta.State;

                if (state == SlotState.Tombstone)
                {
                    Checks.Warning(false
                        , $"Cannot add item because `{nameof(key)}` is pointing to a dead slot. "
                        + $"Key value: {key}."
                    );

                    return false;
                }

                if (state != SlotState.Empty)
                {
                    Checks.Warning(false
                        , $"Cannot add item because `{nameof(key)}` is pointing to an occupied slot. "
                        + $"Key value: {key}."
                    );

                    return false;
                }

                var currentVersion = meta.Version;
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

                meta = new(version, SlotState.Occupied);
                return true;
            }

            public bool TryReplace(uint index, SlotKey key, T item, out SlotKey newKey)
            {
                ref var meta = ref _metas[index];

                if (meta.IsValid == false)
                {
                    Checks.Warning(false
                        , $"Cannot replace item because `{nameof(key)}` is pointing to an invalid slot. "
                        + $"Key value: {key}."
                    );

                    newKey = default;
                    return false;
                }

                var state = meta.State;

                if (state == SlotState.Tombstone)
                {
                    Checks.Warning(false
                        , $"Cannot replace item because `{nameof(key)}` is pointing to a dead slot. "
                        + $"Key value: {key}."
                    );

                    newKey = default;
                    return false;
                }

                if (state != SlotState.Occupied)
                {
                    Checks.Warning(false
                        , $"Cannot replace item because `{nameof(key)}` is pointing to an empty slot. "
                        + $"Key value: {key}."
                    );

                    newKey = default;
                    return false;
                }

                var currentVersion = meta.Version;

                if (currentVersion >= SlotVersion.MaxValue)
                {
                    Checks.Warning(false
                        , $"Cannot replace item because `key.{nameof(SlotKey.Version)}` "
                        + $"has reached the maximum limit. "
                        + $"Key value: {key}. Current version: {currentVersion}."
                    );

                    newKey = default;
                    return false;
                }

                var version = key.Version;

                if (currentVersion != version)
                {
                    Checks.Warning(false
                        , $"Cannot replace item because `key.{nameof(SlotKey.Version)}` "
                        + $"is different from the current version. "
                        + $"Key value: {key}. Current version: {currentVersion}."
                    );

                    newKey = default;
                    return false;
                }

                _items[index] = item;
                currentVersion = version + 1;
                meta = new(meta, currentVersion);
                newKey = key.WithVersion(currentVersion);
                return true;
            }

            public bool TryRemove(uint index, SlotKey key)
            {
                ref var meta = ref _metas[index];

                if (meta.IsValid == false)
                {
                    Checks.Warning(false
                        , $"Cannot remove item because `{nameof(key)}` is pointing to an invalid slot. "
                        + $"Key value: {key}."
                    );

                    return false;
                }

                var state = meta.State;

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

                var currentVersion = meta.Version;

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
                    meta = new(meta, SlotState.Tombstone);
                    _tombstoneCount++;
                }
                else
                {
                    meta = new(meta, SlotState.Empty);
                }

                return true;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Contains(uint index, SlotKey key)
            {
                ref readonly var meta = ref _metas[index];
                return meta.IsValid 
                    && meta.State == SlotState.Occupied 
                    && meta.Version == key.Version;
            }

            public void Clear()
            {
                Array.Clear(_metas, 0, _metas.Length);

                if (s_itemIsUnmanaged)
                {
                    Array.Clear(_items, 0, _items.Length);
                }
            }
        }
    }
}
