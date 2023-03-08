using System;
using System.Runtime.CompilerServices;

namespace Collections.Extensions.SlotMaps
{
    partial class SparseSlotMap<TValue>
    {
        public struct MetaPage
        {
            internal readonly SlotMeta[] _metas;
            internal readonly uint[] _valueIndices;

            private uint _count;

            public MetaPage(uint size)
            {
                _metas = new SlotMeta[size];
                _valueIndices = new uint[size];
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

            public ReadOnlyMemory<uint> ValueIndices
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _valueIndices;
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

            internal uint GetValueIndex(uint index, in SlotKey key)
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

                return _valueIndices[index];
            }

            internal bool TryGetValueIndex(uint index, in SlotKey key, out uint valueIndex)
            {
                ref readonly var meta = ref _metas[index];

                if (meta.IsValid == false)
                {
                    Checks.Warning(false
                        , $"Key {key} is pointing to an invalid slot."
                    );

                    valueIndex = default;
                    return false;
                }

                var state = meta.State;

                if (state == SlotState.Tombstone)
                {
                    Checks.Warning(false
                        , $"Key {key} is pointing to a dead slot."
                    );

                    valueIndex = default;
                    return false;
                }

                if (state == SlotState.Empty)
                {
                    Checks.Warning(false
                        , $"Key {key} is pointing to an empty slot."
                    );

                    valueIndex = default;
                    return false;
                }

                if (meta.Version != key.Version)
                {
                    Checks.Warning(false
                        , $"Key version {key.Version} is not equal to the slot version {meta.Version}."
                    );

                    valueIndex = default;
                    return false;
                }

                valueIndex = _valueIndices[index];
                return true;
            }

            internal void Add(uint index, in SlotKey key, uint valueIndex)
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
                _valueIndices[index] = valueIndex;
                _count++;
            }

            public bool TryAdd(uint index, in SlotKey key, uint valueIndex)
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
                _valueIndices[index] = valueIndex;
                _count++;
                return true;
            }

            internal SlotKey Replace(uint index, in SlotKey key, uint valueIndex)
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

                _valueIndices[index] = valueIndex;
                currentVersion = version + 1;
                meta = new(meta, currentVersion);
                return key.WithVersion(currentVersion);
            }

            internal bool TryReplace(uint index, in SlotKey key, uint valueIndex, out SlotKey newKey)
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

                _valueIndices[index] = valueIndex;
                currentVersion = version + 1;
                meta = new(meta, currentVersion);
                newKey = key.WithVersion(currentVersion);
                return true;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void ReplaceValueIndexUnsafe(uint index, uint valueIndex)
                => _valueIndices[index] = valueIndex;

            internal bool Remove(uint index, in SlotKey key, out uint valueIndex)
            {
                ref var meta = ref _metas[index];

                if (meta.IsValid == false)
                {
                    Checks.Warning(false
                        , $"Key {key} is pointing to an invalid slot."
                    );

                    valueIndex = 0;
                    return false;
                }

                var state = meta.State;

                if (state == SlotState.Tombstone)
                {
                    Checks.Warning(false
                        , $"Key {key} is pointing to a dead slot."
                    );

                    valueIndex = 0;
                    return false;
                }

                if (state != SlotState.Occupied)
                {
                    Checks.Warning(false
                        , $"Key {key} is pointing to an empty slot."
                    );

                    valueIndex = 0;
                    return false;
                }

                var currentVersion = meta.Version;

                if (currentVersion != key.Version)
                {
                    Checks.Warning(false
                        , $"Key version {key.Version} is not equal to the slot version {currentVersion}."
                    );

                    valueIndex = 0;
                    return false;
                }

                valueIndex = _valueIndices[index];
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