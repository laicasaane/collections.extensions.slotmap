using System;
using System.Runtime.CompilerServices;

namespace Collections.Extensions.SlotMaps
{
    partial class SlotMap<T>
    {
        private struct Page
        {
            private uint _count;

            private readonly bool[] _tombstones;
            private readonly bool[] _occupied;
            private readonly SlotVersion[] _versions;
            private readonly T[] _items;

            public Page(uint size)
            {
                _tombstones= new bool[size];
                _occupied = new bool[size];
                _versions = new SlotVersion[size];
                _items = new T[size];
                _count = 0;
            }

            public uint Count => _count;

            public ref T GetRef(uint index, SlotKey key)
            {
                Checks.Require(_tombstones[index] == false
                    , $"Cannot get item because `key` is pointing to a dead slot. "
                    + $"Key value: {key}."
                );

                Checks.Require(_occupied[index] == true
                    , $"Cannot get item because `key` is pointing to an empty slot. "
                    + $"Key value: {key}."
                );

                var currentVersion = _versions[index];

                Checks.Require(currentVersion == key.Version
                    , $"Cannot get item because `key.{nameof(SlotKey.Version)}` "
                    + $"is different from the current version. "
                    + $"Key value: {key}. Current version: {currentVersion}. "
                );

                return ref _items[index];
            }

            public ref T GetRefNotThrow(uint index, SlotKey key)
            {
                if (_tombstones[index] == true)
                {
                    Checks.Suggest(false
                        , $"Cannot get item because `key` is pointing to a dead slot. "
                        + $"Key value: {key}."
                    );

                    return ref Unsafe.NullRef<T>();
                }

                if (_occupied[index] == false)
                {
                    Checks.Suggest(false
                        , $"Cannot get item because `key` is pointing to an empty slot. "
                        + $"Key value: {key}."
                    );

                    return ref Unsafe.NullRef<T>();
                }

                var currentVersion = _versions[index];

                if (currentVersion != key.Version)
                {
                    Checks.Suggest(false
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
                Checks.Require(_tombstones[index] == false
                    , $"Cannot add item because `key` is pointing to a dead slot. "
                    + $"Key value: {key}."
                );

                ref var currentOccupied = ref _occupied[index];

                Checks.Require(currentOccupied == false
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

                currentOccupied = true;
                currentVersion = version;
            }

            public bool TryAdd(uint index, SlotKey key, T item)
            {
                if (_tombstones[index] == true)
                {
                    Checks.Suggest(false
                        , $"Cannot add item because `key` is pointing to a dead slot. "
                        + $"Key value: {key}."
                    );

                    return false;
                }

                ref var currentOccupied = ref _occupied[index];

                if (currentOccupied == true)
                {
                    Checks.Suggest(false
                        , $"Cannot add item because `key` is pointing to an occupied slot. "
                        + $"Key value: {key}."
                    );

                    return false;
                }

                ref var currentVersion = ref _versions[index];
                var version = key.Version;

                if (currentVersion >= version)
                {
                    Checks.Suggest(false
                        , $"Cannot add item because `key.{nameof(SlotKey.Version)}` "
                        + $"is lesser than or equal to the current version. "
                        + $"Key value: {key}. Current version: {currentVersion}."
                    );

                    return false;
                }

                _items[index] = item;
                _count++;

                currentOccupied = true;
                currentVersion = version;
                return true;
            }

            public bool TryReplace(uint index, SlotKey key, T item, out SlotKey newKey)
            {
                if (_tombstones[index] == true)
                {
                    Checks.Suggest(false
                        , $"Cannot replace item because `key` is pointing to a dead slot. "
                        + $"Key value: {key}."
                    );

                    newKey = default;
                    return false;
                }

                if (_occupied[index] == false)
                {
                    Checks.Suggest(false
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
                    Checks.Suggest(false
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
                ref var currentTombstone = ref _tombstones[index];

                if (currentTombstone == true)
                {
                    Checks.Suggest(false
                        , $"Cannot remove item because `{nameof(key)}` is pointing to a dead slot. "
                        + $"Key value: {key}."
                    );

                    return true;
                }

                ref var currentOccupied = ref _occupied[index];

                if (currentOccupied == false)
                {
                    Checks.Suggest(false
                        , $"Cannot remove item because `{nameof(key)}` is pointing to an empty slot. "
                        + $"Key value: {key}."
                    );

                    return false;
                }

                var currentVersion = _versions[index];

                if (currentVersion != key.Version)
                {
                    Checks.Suggest(false
                        , $"Cannot remove item because the  `key.{nameof(SlotKey.Version)}` "
                        + $"is different from the current version. "
                        + $"Key value: {key}. Current version: {currentVersion}."
                    );

                    return false;
                }

                _items[index] = default;
                currentOccupied = false;

                if (currentVersion < SlotVersion.MaxValue)
                {
                    _count -= 1;
                }
                else
                {
                    _count = 0;
                    currentTombstone = true;
                }

                return true;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Contains(uint index, SlotKey key)
                => _tombstones[index] == false
                && _occupied[index] == true 
                && _versions[index] == key.Version;

            public void Clear()
            {
                Array.Clear(_tombstones, 0, _tombstones.Length);
                Array.Clear(_occupied, 0, _occupied.Length);
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
