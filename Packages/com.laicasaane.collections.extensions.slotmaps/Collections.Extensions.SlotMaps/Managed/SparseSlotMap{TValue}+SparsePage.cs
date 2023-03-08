﻿using System;
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

            /// <summary>
            /// To determine whether the last page is full.
            /// </summary>
            /// <remarks>
            /// Does NOT equal to the length of internal arrays.
            /// </remarks>
            internal uint Count
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

            internal SlotKey UpdateVersion(uint index, in SlotKey key)
            {
                ref readonly var meta = ref _metas[index];

                Checks.Require(meta.IsValid
                    , $"Key {key} is pointing to an invalid slot."
                );

                Checks.Require(meta.State != SlotState.Tombstone
                    , $"Key {key} is pointing to a dead slot."
                );

                Checks.Require(meta.State == SlotState.Occupied
                    , $"Key {key} is pointing to an empty slot."
                );

                return key.WithVersion(meta.Version);
            }

            public bool TryUpdateVersion(uint index, in SlotKey key, out SlotKey newKey)
            {
                ref readonly var meta = ref _metas[index];

                if (meta.IsValid == false)
                {
                    Checks.Warning(false
                        , $"Key {key} is pointing to an invalid slot."
                    );

                    newKey = default;
                    return false;
                }

                var state = meta.State;

                if (state == SlotState.Tombstone)
                {
                    Checks.Warning(false
                        , $"Key {key} is pointing to a dead slot."
                    );

                    newKey = default;
                    return false;
                }

                if (state != SlotState.Occupied)
                {
                    Checks.Warning(false
                        , $"Key {key} is pointing to an empty slot."
                    );

                    newKey = default;
                    return false;
                }

                newKey = key.WithVersion(meta.Version);
                return true;
            }

            internal uint GetDenseIndex(uint index, in SlotKey key)
            {
                ref readonly var meta = ref _metas[index];

                Checks.Require(meta.IsValid
                    , $"Key {key} is pointing to an invalid slot."
                );

                Checks.Require(meta.State != SlotState.Tombstone
                    , $"Key {key} is pointing to a dead slot."
                );

                Checks.Require(meta.State == SlotState.Occupied
                    , $"Key {key} is pointing to an empty slot."
                );

                Checks.Require(meta.Version == key.Version
                    , $"Key version {key.Version} is not equal to the slot version {meta.Version}."
                );

                return _denseIndices[index];
            }

            internal bool TryGetDenseIndex(uint index, in SlotKey key, out uint denseIndex)
            {
                ref readonly var meta = ref _metas[index];

                if (meta.IsValid == false)
                {
                    Checks.Warning(false
                        , $"Key {key} is pointing to an invalid slot."
                    );

                    denseIndex = default;
                    return false;
                }

                var state = meta.State;

                if (state == SlotState.Tombstone)
                {
                    Checks.Warning(false
                        , $"Key {key} is pointing to a dead slot."
                    );

                    denseIndex = default;
                    return false;
                }

                if (state == SlotState.Empty)
                {
                    Checks.Warning(false
                        , $"Key {key} is pointing to an empty slot."
                    );

                    denseIndex = default;
                    return false;
                }

                if (meta.Version != key.Version)
                {
                    Checks.Warning(false
                        , $"Key version {key.Version} is not equal to the slot version {meta.Version}."
                    );

                    denseIndex = default;
                    return false;
                }

                denseIndex = _denseIndices[index];
                return true;
            }

            internal void Add(uint index, in SlotKey key, uint denseIndex)
            {
                ref var meta = ref _metas[index];

                Checks.Require(meta.State != SlotState.Tombstone
                    , $"Key {key} is pointing to a dead slot."
                );

                Checks.Require(meta.State == SlotState.Empty
                    , $"Key {key} is pointing to an occupied slot."
                );

                var version = key.Version;

                Checks.Require(meta.Version < version
                    , $"Key version {version} is lesser than or equal to the slot version {meta.Version}."
                );

                meta = new(version, SlotState.Occupied);
                _denseIndices[index] = denseIndex;
                _count++;
            }

            public bool TryAdd(uint index, in SlotKey key, uint denseIndex)
            {
                ref var meta = ref _metas[index];
                var state = meta.State;

                if (state == SlotState.Tombstone)
                {
                    Checks.Warning(false
                        , $"Key {key} is pointing to a dead slot."
                    );

                    return false;
                }

                if (state != SlotState.Empty)
                {
                    Checks.Warning(false
                        , $"Key {key} is pointing to an occupied slot."
                    );

                    return false;
                }

                var currentVersion = meta.Version;
                var version = key.Version;

                if (currentVersion >= version)
                {
                    Checks.Warning(false
                        , $"Key version {version} is lesser than or equal to the slot version {currentVersion}."
                    );

                    return false;
                }

                meta = new(version, SlotState.Occupied);
                _denseIndices[index] = denseIndex;
                _count++;
                return true;
            }

            internal SlotKey Replace(uint index, in SlotKey key, uint denseIndex)
            {
                ref var meta = ref _metas[index];

                Checks.Require(meta.IsValid == true
                    , $"Key {key} is pointing to an invalid slot."
                );

                Checks.Require(meta.State != SlotState.Tombstone
                    , $"Key {key} is pointing to a dead slot."
                );

                Checks.Require(meta.State == SlotState.Occupied
                    , $"Key {key} is pointing to an empty slot."
                );

                var currentVersion = meta.Version;
                var version = key.Version;

                Checks.Require(currentVersion < SlotVersion.MaxValue
                    , $"Key version {version} has reached the maximum limit."
                );

                Checks.Require(currentVersion == version
                    , $"Key version {version} is not equal to the slot version {currentVersion}."
                );

                _denseIndices[index] = denseIndex;
                currentVersion = version + 1;
                meta = new(meta, currentVersion);
                return key.WithVersion(currentVersion);
            }

            internal bool TryReplace(uint index, in SlotKey key, uint denseIndex, out SlotKey newKey)
            {
                ref var meta = ref _metas[index];

                if (meta.IsValid == false)
                {
                    Checks.Warning(false
                        , $"Key {key} is pointing to an invalid slot."
                    );

                    newKey = default;
                    return false;
                }

                var state = meta.State;

                if (state == SlotState.Tombstone)
                {
                    Checks.Warning(false
                        , $"Key {key} is pointing to a dead slot."
                    );

                    newKey = default;
                    return false;
                }

                if (state != SlotState.Occupied)
                {
                    Checks.Warning(false
                        , $"Key {key} is pointing to an empty slot."
                    );

                    newKey = default;
                    return false;
                }

                var currentVersion = meta.Version;
                var version = key.Version;

                if (currentVersion >= SlotVersion.MaxValue)
                {
                    Checks.Warning(false
                        , $"Key version {version} has reached the maximum limit."
                    );

                    newKey = default;
                    return false;
                }

                if (currentVersion != version)
                {
                    Checks.Warning(false
                        , $"Key version {version} is not equal to the slot version {currentVersion}."
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

            internal bool Remove(uint index, in SlotKey key, out uint denseIndex)
            {
                ref var meta = ref _metas[index];

                if (meta.IsValid == false)
                {
                    Checks.Warning(false
                        , $"Key {key} is pointing to an invalid slot."
                    );

                    denseIndex = 0;
                    return false;
                }

                var state = meta.State;

                if (state == SlotState.Tombstone)
                {
                    Checks.Warning(false
                        , $"Key {key} is pointing to a dead slot."
                    );

                    denseIndex = 0;
                    return false;
                }

                if (state != SlotState.Occupied)
                {
                    Checks.Warning(false
                        , $"Key {key} is pointing to an empty slot."
                    );

                    denseIndex = 0;
                    return false;
                }

                var currentVersion = meta.Version;

                if (currentVersion != key.Version)
                {
                    Checks.Warning(false
                        , $"Key version {key.Version} is not equal to the slot version {currentVersion}."
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
            internal bool Contains(uint index, in SlotKey key)
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