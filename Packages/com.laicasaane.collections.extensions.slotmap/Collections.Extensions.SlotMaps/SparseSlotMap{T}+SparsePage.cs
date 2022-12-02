using System;
using System.Runtime.CompilerServices;

namespace Collections.Extensions.SlotMaps
{
    partial class SparseSlotMap<T>
    {
        public struct SparsePage
        {
            internal readonly SlotMeta[] _metas;
            internal readonly uint[] _denseIndices;

            private uint _count;

            public SparsePage(uint size)
            {
                _metas = new SlotMeta[size];
                _denseIndices = new uint[size];
                _count = 0;
            }

            public uint Count
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _count;
            }

            public ReadOnlyMemory<SlotMeta> Metas
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _metas;
            }

            public ReadOnlyMemory<uint> DenseIndices
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _denseIndices;
            }

            internal uint GetDenseIndex(uint index, SlotKey key)
            {
                ref readonly var meta = ref _metas[index];

                Checks.Require(meta.IsValid
                    , $"Cannot get item because `{nameof(key)}` is pointing to an invalid slot. "
                    + $"Key value: {key}."
                );

                Checks.Require(meta.State != SlotState.Tombstone
                    , $"Cannot get item because `{nameof(key)}` is pointing to a dead slot. "
                    + $"Key value: {key}."
                );

                Checks.Require(meta.State == SlotState.Occupied
                    , $"Cannot get item because `{nameof(key)}` is pointing to an empty slot. "
                    + $"Key value: {key}."
                );

                Checks.Require(meta.Version == key.Version
                    , $"Cannot get item because `key.{nameof(SlotKey.Version)}` "
                    + $"is different from the current version. "
                    + $"Key value: {key}. Current version: {meta.Version}. "
                );

                return _denseIndices[index];
            }

            internal bool TryGetDenseIndex(uint index, SlotKey key, out uint denseIndex)
            {
                ref readonly var meta = ref _metas[index];

                if (meta.IsValid == false)
                {
                    Checks.Warning(false
                        , $"Cannot get item because `{nameof(key)}` is pointing to an invalid slot. "
                        + $"Key value: {key}."
                    );

                    denseIndex = default;
                    return false;
                }

                var state = meta.State;

                if (state == SlotState.Tombstone)
                {
                    Checks.Warning(false
                        , $"Cannot get item because `{nameof(key)}` is pointing to a dead slot. "
                        + $"Key value: {key}."
                    );

                    denseIndex = default;
                    return false;
                }

                if (state == SlotState.Empty)
                {
                    Checks.Warning(false
                        , $"Cannot get item because `{nameof(key)}` is pointing to an empty slot. "
                        + $"Key value: {key}."
                    );

                    denseIndex = default;
                    return false;
                }

                if (meta.Version != key.Version)
                {
                    Checks.Warning(false
                        , $"Cannot get item because `key.{nameof(SlotKey.Version)}` "
                        + $"is different from the current version. "
                        + $"Key value: {key}. Current version: {meta.Version}."
                    );

                    denseIndex = default;
                    return false;
                }

                denseIndex = _denseIndices[index];
                return true;
            }

            internal void Add(uint index, SlotKey key, uint denseIndex)
            {
                ref var meta = ref _metas[index];

                Checks.Require(meta.State != SlotState.Tombstone
                    , $"Cannot add item because `{nameof(key)}` is pointing to a dead slot. "
                    + $"Key value: {key}."
                );

                Checks.Require(meta.State == SlotState.Empty
                    , $"Cannot add item because `{nameof(key)}` is pointing to an occupied slot. "
                    + $"Key value: {key}."
                );

                var version = key.Version;

                Checks.Require(meta.Version < version
                    , $"Cannot add item because `key.{nameof(SlotKey.Version)}` "
                    + $"is lesser than or equal to the current version. "
                    + $"Key value: {key}. Current version: {meta.Version}."
                );

                meta = new(version, SlotState.Occupied);
                _denseIndices[index] = denseIndex;
                _count++;
            }

            public bool TryAdd(uint index, SlotKey key, uint denseIndex)
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

                meta = new(version, SlotState.Occupied);
                _denseIndices[index] = denseIndex;
                _count++;
                return true;
            }

            internal SlotKey Replace(uint index, SlotKey key, uint denseIndex)
            {
                ref var meta = ref _metas[index];

                Checks.Require(meta.IsValid == true
                    , $"Cannot replace item because `{nameof(key)}` is pointing to an invalid slot. "
                    + $"Key value: {key}."
                );

                Checks.Require(meta.State != SlotState.Tombstone
                    , $"Cannot replace item because `{nameof(key)}` is pointing to a dead slot. "
                    + $"Key value: {key}."
                );

                Checks.Require(meta.State == SlotState.Occupied
                    , $"Cannot replace item because `{nameof(key)}` is pointing to an empty slot. "
                    + $"Key value: {key}."
                );

                var currentVersion = meta.Version;

                Checks.Require(currentVersion < SlotVersion.MaxValue
                    , $"Cannot replace item because `key.{nameof(SlotKey.Version)}` "
                    + $"has reached the maximum limit. "
                    + $"Key value: {key}. Current version: {currentVersion}."
                );

                var version = key.Version;

                Checks.Require(currentVersion == version
                    , $"Cannot replace item because `key.{nameof(SlotKey.Version)}` "
                    + $"is different from the current version. "
                    + $"Key value: {key}. Current version: {currentVersion}."
                );

                _denseIndices[index] = denseIndex;
                currentVersion = version + 1;
                meta = new(meta, currentVersion);
                return key.WithVersion(currentVersion);
            }

            internal bool TryReplace(uint index, SlotKey key, uint denseIndex, out SlotKey newKey)
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

                _denseIndices[index] = denseIndex;
                currentVersion = version + 1;
                meta = new(meta, currentVersion);
                newKey = key.WithVersion(currentVersion);
                return true;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void ReplaceDenseIndexUnsafe(uint index, uint denseIndex)
                => _denseIndices[index] = denseIndex;

            internal bool Remove(uint index, SlotKey key, out uint denseIndex)
            {
                ref var meta = ref _metas[index];

                if (meta.IsValid == false)
                {
                    Checks.Warning(false
                        , $"Cannot remove item because `{nameof(key)}` is pointing to an invalid slot. "
                        + $"Key value: {key}."
                    );

                    denseIndex = 0;
                    return false;
                }

                var state = meta.State;

                if (state == SlotState.Tombstone)
                {
                    Checks.Warning(false
                        , $"Cannot remove item because `{nameof(key)}` is pointing to a dead slot. "
                        + $"Key value: {key}."
                    );

                    denseIndex = 0;
                    return false;
                }

                if (state != SlotState.Occupied)
                {
                    Checks.Warning(false
                        , $"Cannot remove item because `{nameof(key)}` is pointing to an empty slot. "
                        + $"Key value: {key}."
                    );

                    denseIndex = 0;
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

                    denseIndex = 0;
                    return false;
                }

                denseIndex = _denseIndices[index];
                meta = currentVersion == SlotVersion.MaxValue
                    ? new(meta, SlotState.Tombstone)
                    : new(meta, SlotState.Empty)
                    ;

                _count -= 1;
                return true;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal bool Contains(uint index, SlotKey key)
            {
                ref readonly var meta = ref _metas[index];
                return meta.IsValid
                    && meta.State == SlotState.Occupied
                    && meta.Version == key.Version;
            }

            internal void Clear()
            {
                Array.Clear(_metas, 0, _metas.Length);
                _count = 0;
            }
        }
    }
}