using System;
using System.Runtime.CompilerServices;

namespace Collections.Extensions.SlotMaps
{
    partial class SparseSlotMap<TValue>
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

            internal SlotKey UpdateVersion(uint index, SlotKey key)
            {
                ref readonly var meta = ref _metas[index];

                Checks.Require(meta.IsValid
                    , $"Cannot get value because `{key}` is pointing to an invalid slot."
                );

                Checks.Require(meta.State != SlotState.Tombstone
                    , $"Cannot get value because `{key}` is pointing to a dead slot."
                );

                Checks.Require(meta.State == SlotState.Occupied
                    , $"Cannot get value because `{key}` is pointing to an empty slot."
                );

                return key.WithVersion(meta.Version);
            }

            public bool TryUpdateVersion(uint index, SlotKey key, out SlotKey newKey)
            {
                ref readonly var meta = ref _metas[index];

                if (meta.IsValid == false)
                {
                    Checks.Warning(false
                        , $"Cannot replace value because `{key}` is pointing to an invalid slot."
                    );

                    newKey = default;
                    return false;
                }

                var state = meta.State;

                if (state == SlotState.Tombstone)
                {
                    Checks.Warning(false
                        , $"Cannot replace value because `{key}` is pointing to a dead slot."
                    );

                    newKey = default;
                    return false;
                }

                if (state != SlotState.Occupied)
                {
                    Checks.Warning(false
                        , $"Cannot replace value because `{key}` is pointing to an empty slot."
                    );

                    newKey = default;
                    return false;
                }

                newKey = key.WithVersion(meta.Version);
                return true;
            }

            internal uint GetDenseIndex(uint index, SlotKey key)
            {
                ref readonly var meta = ref _metas[index];

                Checks.Require(meta.IsValid
                    , $"Cannot get value because `{key}` is pointing to an invalid slot."
                );

                Checks.Require(meta.State != SlotState.Tombstone
                    , $"Cannot get value because `{key}` is pointing to a dead slot."
                );

                Checks.Require(meta.State == SlotState.Occupied
                    , $"Cannot get value because `{key}` is pointing to an empty slot."
                );

                Checks.Require(meta.Version == key.Version
                    , $"Cannot get value because `key.Version` "
                    + $"is different from the current version. "
                    + $"key.Version: {key.Version}. Current version: {meta.Version}."
                );

                return _denseIndices[index];
            }

            internal bool TryGetDenseIndex(uint index, SlotKey key, out uint denseIndex)
            {
                ref readonly var meta = ref _metas[index];

                if (meta.IsValid == false)
                {
                    Checks.Warning(false
                        , $"Cannot get value because `{key}` is pointing to an invalid slot."
                    );

                    denseIndex = default;
                    return false;
                }

                var state = meta.State;

                if (state == SlotState.Tombstone)
                {
                    Checks.Warning(false
                        , $"Cannot get value because `{key}` is pointing to a dead slot."
                    );

                    denseIndex = default;
                    return false;
                }

                if (state == SlotState.Empty)
                {
                    Checks.Warning(false
                        , $"Cannot get value because `{key}` is pointing to an empty slot."
                    );

                    denseIndex = default;
                    return false;
                }

                if (meta.Version != key.Version)
                {
                    Checks.Warning(false
                        , $"Cannot get value because `key.Version` "
                        + $"is different from the current version. "
                        + $"key.Version: {key.Version}. Current version: {meta.Version}."
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
                    , $"Cannot add value because `{key}` is pointing to a dead slot."
                );

                Checks.Require(meta.State == SlotState.Empty
                    , $"Cannot add value because `{key}` is pointing to an occupied slot."
                );

                var version = key.Version;

                Checks.Require(meta.Version < version
                    , $"Cannot add value because `key.Version` "
                    + $"is lesser than or equal to the current version. "
                    + $"key.Version: {version}. Current version: {meta.Version}."
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
                        , $"Cannot add value because `{key}` is pointing to a dead slot."
                    );

                    return false;
                }

                if (state != SlotState.Empty)
                {
                    Checks.Warning(false
                        , $"Cannot add value because `{key}` is pointing to an occupied slot."
                    );

                    return false;
                }

                var currentVersion = meta.Version;
                var version = key.Version;

                if (currentVersion >= version)
                {
                    Checks.Warning(false
                        , $"Cannot add value because `key.Version` "
                        + $"is lesser than or equal to the current version. "
                        + $"key.Version: {version}. Current version: {currentVersion}."
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
                    , $"Cannot replace value because `{key}` is pointing to an invalid slot."
                );

                Checks.Require(meta.State != SlotState.Tombstone
                    , $"Cannot replace value because `{key}` is pointing to a dead slot."
                );

                Checks.Require(meta.State == SlotState.Occupied
                    , $"Cannot replace value because `{key}` is pointing to an empty slot."
                );

                var currentVersion = meta.Version;
                var version = key.Version;

                Checks.Require(currentVersion < SlotVersion.MaxValue
                    , $"Cannot replace value because `key.Version` "
                    + $"has reached the maximum limit. "
                    + $"key.Version: {version}. Current version: {currentVersion}."
                );

                Checks.Require(currentVersion == version
                    , $"Cannot replace value because `key.Version` "
                    + $"is different from the current version. "
                    + $"key.Version: {version}. Current version: {currentVersion}."
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
                        , $"Cannot replace value because `{key}` is pointing to an invalid slot."
                    );

                    newKey = default;
                    return false;
                }

                var state = meta.State;

                if (state == SlotState.Tombstone)
                {
                    Checks.Warning(false
                        , $"Cannot replace value because `{key}` is pointing to a dead slot."
                    );

                    newKey = default;
                    return false;
                }

                if (state != SlotState.Occupied)
                {
                    Checks.Warning(false
                        , $"Cannot replace value because `{key}` is pointing to an empty slot."
                    );

                    newKey = default;
                    return false;
                }

                var currentVersion = meta.Version;
                var version = key.Version;

                if (currentVersion >= SlotVersion.MaxValue)
                {
                    Checks.Warning(false
                        , $"Cannot replace value because `key.Version` "
                        + $"has reached the maximum limit. "
                        + $"key.Version: {version}. Current version: {currentVersion}."
                    );

                    newKey = default;
                    return false;
                }

                if (currentVersion != version)
                {
                    Checks.Warning(false
                        , $"Cannot replace value because `key.Version` "
                        + $"is different from the current version. "
                        + $"key.Version: {version}. Current version: {currentVersion}."
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
                        , $"Cannot remove value because `{key}` is pointing to an invalid slot."
                    );

                    denseIndex = 0;
                    return false;
                }

                var state = meta.State;

                if (state == SlotState.Tombstone)
                {
                    Checks.Warning(false
                        , $"Cannot remove value because `{key}` is pointing to a dead slot."
                    );

                    denseIndex = 0;
                    return false;
                }

                if (state != SlotState.Occupied)
                {
                    Checks.Warning(false
                        , $"Cannot remove value because `{key}` is pointing to an empty slot."
                    );

                    denseIndex = 0;
                    return false;
                }

                var currentVersion = meta.Version;

                if (currentVersion != key.Version)
                {
                    Checks.Warning(false
                        , $"Cannot remove value because the  `key.Version` "
                        + $"is different from the current version. "
                        + $"key.Version: {key.Version}. Current version: {currentVersion}."
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