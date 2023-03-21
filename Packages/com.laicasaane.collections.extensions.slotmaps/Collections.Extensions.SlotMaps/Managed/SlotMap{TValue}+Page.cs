using System;
using System.Runtime.CompilerServices;

namespace Collections.Extensions.SlotMaps
{
    partial class SlotMap<TValue>
    {
        public readonly struct Page
        {
            internal readonly SlotMeta[] _metas;
            internal readonly TValue[] _values;

            private readonly uint[] _count;

            public Page(uint size)
            {
                _metas = new SlotMeta[size];
                _values = new TValue[size];

                _count = new uint[1];
                _count[0] = 0;
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
                get => _count[0];
            }

            public ReadOnlyMemory<SlotMeta> Metas
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _metas;
            }

            public ReadOnlyMemory<TValue> Values
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _values;
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

            internal ref TValue GetRef(uint index, in SlotKey key)
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

                return ref _values[index];
            }

            internal ref TValue GetRefNotThrow(uint index, in SlotKey key)
            {
                ref readonly var meta = ref _metas[index];

                if (meta.IsValid == false)
                {
                    Checks.Warning(false
                        , $"Key {key} is pointing to an invalid slot."
                    );

                    return ref Unsafe.NullRef<TValue>();
                }

                var state = meta.State;

                if (state == SlotState.Tombstone)
                {
                    Checks.Warning(false
                        , $"Key {key} is pointing to a dead slot."
                    );

                    return ref Unsafe.NullRef<TValue>();
                }

                if (state == SlotState.Empty)
                {
                    Checks.Warning(false
                        , $"Key {key} is pointing to an empty slot."
                    );

                    return ref Unsafe.NullRef<TValue>();
                }

                if (meta.Version != key.Version)
                {
                    Checks.Warning(false
                        , $"Key version {key.Version} is not equal to the slot version {meta.Version}."
                    );

                    return ref Unsafe.NullRef<TValue>();
                }

                return ref _values[index];
            }

            internal void Add(uint index, in SlotKey key, TValue value)
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

                _values[index] = value;
                meta = new(version, SlotState.Occupied);
                _count[0]++;
            }

            public bool TryAdd(uint index, in SlotKey key, TValue value)
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

                _values[index] = value;
                meta = new(version, SlotState.Occupied);
                _count[0]++;
                return true;
            }

            internal SlotKey Replace(uint index, in SlotKey key, TValue value)
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

                _values[index] = value;
                currentVersion = version + 1;
                meta = new(meta, currentVersion);
                return key.WithVersion(currentVersion);
            }

            internal bool TryReplace(uint index, SlotKey key, TValue value, out SlotKey newKey)
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

                _values[index] = value;
                currentVersion = version + 1;
                meta = new(meta, currentVersion);
                newKey = key.WithVersion(currentVersion);
                return true;
            }

            internal bool Remove(uint index, in SlotKey key)
            {
                ref var meta = ref _metas[index];

                if (meta.IsValid == false)
                {
                    Checks.Warning(false
                        , $"Key {key} is pointing to an invalid slot."
                    );

                    return false;
                }

                var state = meta.State;

                if (state == SlotState.Tombstone)
                {
                    Checks.Warning(false
                        , $"Key {key} is pointing to a dead slot."
                    );

                    return false;
                }

                if (state != SlotState.Occupied)
                {
                    Checks.Warning(false
                        , $"Key {key} is pointing to an empty slot."
                    );

                    return false;
                }

                var currentVersion = meta.Version;

                if (currentVersion != key.Version)
                {
                    Checks.Warning(false
                        , $"Key version {key.Version} is not equal to the current version {currentVersion}."
                    );

                    return false;
                }

                _values[index] = default;
                meta = currentVersion == SlotVersion.MaxValue
                    ? new(meta, SlotState.Tombstone)
                    : new(meta, SlotState.Empty)
                    ;

                _count[0] -= 1;
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

                if (s_valueIsUnmanaged)
                {
                    Array.Clear(_values, 0, _values.Length);
                }

                _count[0] = 0;
            }
        }
    }
}
