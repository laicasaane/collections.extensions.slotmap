using System;
using System.Runtime.CompilerServices;

namespace Collections.Extensions.SlotMaps
{
    partial class SparseSlotMap<TValue>
    {
        public struct DensePage
        {
            internal readonly uint[] _sparseIndices;
            internal readonly TValue[] _values;

            private uint _count;

            public DensePage(uint size)
            {
                _sparseIndices = new uint[size];
                _values = new TValue[size];
                _count = 0;
            }

            public uint Count
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _count;
            }

            public ReadOnlyMemory<uint> SparseIndices
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => new ReadOnlyMemory<uint>(_sparseIndices, 0, (int)_count);
            }

            public ReadOnlyMemory<TValue> Values
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => new ReadOnlyMemory<TValue>(_values, 0, (int)_count);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal ref TValue GetRef(uint index)
                => ref _values[index];

            internal void Add(uint index, uint sparseIndex, TValue value)
            {
                _sparseIndices[index] = sparseIndex;
                _values[index] = value;
                _count++;
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
                _count--;
            }

            internal void Clear()
            {
                if (s_valueIsUnmanaged)
                {
                    Array.Clear(_values, 0, _values.Length);
                }

                _count = 0;
            }
        }
    }
}