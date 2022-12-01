using System;
using System.Runtime.CompilerServices;

namespace Collections.Extensions.SlotMaps
{
    partial class SparseSlotMap<T>
    {
        public struct DensePage
        {
            private readonly uint[] _sparseIndices;
            private readonly T[] _items;

            private uint _count;

            public DensePage(uint size)
            {
                _sparseIndices = new uint[size];
                _items = new T[size];
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
                get => _sparseIndices;
            }

            public ReadOnlyMemory<T> Items
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _items;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal ref T GetRef(uint index)
                => ref _items[index];

            internal void Add(uint index, uint sparseIndex, T item)
            {
                _sparseIndices[index] = sparseIndex;
                _items[index] = item;
                _count++;
            }

            internal void Replace(uint index, uint sparseIndex, T item)
            {
                _sparseIndices[index] = sparseIndex;
                _items[index] = item;
            }

            internal void Remove(uint index)
            {
                _items[index] = default;
                _count--;
            }

            internal void Clear()
            {
                if (s_itemIsUnmanaged)
                {
                    Array.Clear(_items, 0, _items.Length);
                }

                _count = 0;
            }
        }
    }
}