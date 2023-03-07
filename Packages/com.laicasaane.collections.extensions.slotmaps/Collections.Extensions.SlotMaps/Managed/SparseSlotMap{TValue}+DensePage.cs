using System;
using System.Runtime.CompilerServices;

namespace Collections.Extensions.SlotMaps
{
    partial class SparseSlotMap<TValue>
    {
        public readonly struct DensePage
        {
            internal readonly uint[] _sparseIndices;
            internal readonly TValue[] _values;

            public DensePage(uint size)
            {
                _sparseIndices = new uint[size];
                _values = new TValue[size];
            }

            public ReadOnlyMemory<uint> SparseIndices
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _sparseIndices;
            }

            public ReadOnlyMemory<TValue> Values
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _values;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal ref TValue GetRef(uint index)
                => ref _values[index];

            internal void Add(uint index, uint sparseIndex, TValue value)
            {
                _sparseIndices[index] = sparseIndex;
                _values[index] = value;
            }

            internal void Replace(uint index, uint sparseIndex, TValue value)
            {
                _sparseIndices[index] = sparseIndex;
                _values[index] = value;
            }

            internal void Remove(uint index, out uint sparseIndex, out TValue value)
            {
                sparseIndex = _sparseIndices[index];
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