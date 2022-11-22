using System;
using System.Runtime.CompilerServices;

namespace Collections.Extensions.SlotMap
{
    partial class SlotMap<T>
    {
        private struct Page
        {
            private uint _count;

            private readonly SlotVersion[] _versions;
            private readonly bool[] _tombstones;
            private readonly T[] _items;

            public Page(uint size)
            {
                _versions = new SlotVersion[size];
                _tombstones= new bool[size];
                _items = new T[size];
                _count = 0;
            }

            public uint Count => _count;

            public T Get(uint index, SlotVersion version)
            {
                ref var currentTombstone = ref _tombstones[index];

                Checks.Require(currentTombstone == false, $"Cannot get item because `key` is pointing to a dead slot.");

                ref var currentVersion = ref _versions[index];

                Checks.Require(currentVersion == version, $"Cannot get item because " +
                    $"`key.{nameof(SlotKey.Version)}` is different from the current version. " +
                    $"Argument value: {version}. Current value: {currentVersion}."
                );

                return _items[index];
            }

            public ref readonly T GetRef(uint index, SlotVersion version)
            {
                ref var currentTombstone = ref _tombstones[index];

                Checks.Require(currentTombstone == false, $"Cannot get item because `key` is pointing to a dead slot.");

                ref var currentVersion = ref _versions[index];

                Checks.Require(currentVersion == version, $"Cannot get item because " +
                    $"`key.{nameof(SlotKey.Version)}` is different from the current version. " +
                    $"Argument value: {version}. Current value: {currentVersion}."
                );

                return ref _items[index];
            }

            public bool TryGet(uint index, SlotVersion version, out T item)
            {
                ref var currentTombstone = ref _tombstones[index];

                if (currentTombstone == true)
                {
                    Checks.Suggest(false, $"Cannot get item because `key` is pointing to a dead slot.");
                    item = default;
                    return false;
                }

                ref var currentVersion = ref _versions[index];

                if (currentVersion != version)
                {
                    Checks.Suggest(false, $"Cannot get item because " +
                        $"`key.{nameof(SlotKey.Version)}` is different from the current version. " +
                        $"Argument value: {version}. Current value: {currentVersion}."
                    );

                    item = default;
                    return false;
                }

                item = _items[index];
                return true;
            }

            public bool TryAdd(uint index, SlotVersion version, T item)
            {
                ref var currentTombstone = ref _tombstones[index];

                if (currentTombstone == true)
                {
                    Checks.Suggest(false, $"Cannot add item because `key` is pointing to a dead slot.");
                    return false;
                }

                ref var currentVersion = ref _versions[index];

                if (currentVersion >= version)
                {
                    Checks.Suggest(false, $"Cannot add item because " +
                        $"`key.{nameof(SlotKey.Version)}` is lesser than or equal to the current version. " +
                        $"Argument value: {version}. Current value: {currentVersion}."
                    );

                    return false;
                }

                currentVersion = version;
                _items[index] = item;
                _count++;
                return true;
            }

            public bool TryReplace(uint index, SlotVersion version, T item, out SlotVersion newVersion)
            {
                ref var currentTombstone = ref _tombstones[index];

                if (currentTombstone == true)
                {
                    Checks.Suggest(false, $"Cannot replace item because `key` is pointing to a dead slot.");
                    newVersion = default;
                    return false;
                }

                ref var currentVersion = ref _versions[index];

                if (currentVersion != version)
                {
                    Checks.Suggest(false, $"Cannot add item because " +
                        $"`key.{nameof(SlotKey.Version)}` is different from the current version. " +
                        $"Argument value: {version}. Current value: {currentVersion}."
                    );

                    newVersion = default;
                    return false;
                }

                currentVersion = newVersion = version + 1;
                _items[index] = item;
                return true;
            }

            public bool TryRemove(uint index, SlotVersion version)
            {
                ref var currentTombstone = ref _tombstones[index];

                if (currentTombstone == true)
                {
                    return true;
                }

                ref var currentVersion = ref _versions[index];

                if (currentVersion != version)
                {
                    Checks.Suggest(false, $"Cannot remove item because the " +
                        $"`key.{nameof(SlotKey.Version)}` is different from the current version. " +
                        $"Argument value: {version}. Current value: {currentVersion}."
                    );

                    return false;
                }

                _items[index] = default;

                if (currentVersion == SlotVersion.MaxValue)
                {
                    _count = 0;
                    currentTombstone = true;
                }
                else
                {
                    _count -= 1;
                }

                return true;
            }

            public void Clear()
            {
                Array.Clear(_versions, 0, _versions.Length);
                Array.Clear(_tombstones, 0, _tombstones.Length);

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
