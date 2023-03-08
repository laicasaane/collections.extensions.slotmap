using System;
using System.Runtime.CompilerServices;

namespace Collections.Extensions.SlotMaps
{
    partial class SparseSlotMap<TValue>
    {
        public readonly struct ValuePage
        {
            internal readonly uint[] _metaIndices;
            internal readonly TValue[] _values;

            public ValuePage(uint size)
            {
                _metaIndices = new uint[size];
                _values = new TValue[size];
            }

            public ReadOnlyMemory<uint> MetaIndices
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _metaIndices;
            }

            public ReadOnlyMemory<TValue> Values
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _values;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal ref TValue GetRef(uint index)
                => ref _values[index];

            internal void Add(uint index, uint metaIndex, TValue value)
            {
                _metaIndices[index] = metaIndex;
                _values[index] = value;
            }

            internal void Replace(uint index, uint metaIndex, TValue value)
            {
                _metaIndices[index] = metaIndex;
                _values[index] = value;
            }

            internal void Remove(uint index, out uint metaIndex, out TValue value)
            {
                metaIndex = _metaIndices[index];
                value = _values[index];
                _values[index] = default;
            }

            internal void Clear()
            {
                if (s_valueIsUnmanaged)
                {
                    Array.Clear(_values, 0, _values.Length);
                }
            }
        }
    }
}