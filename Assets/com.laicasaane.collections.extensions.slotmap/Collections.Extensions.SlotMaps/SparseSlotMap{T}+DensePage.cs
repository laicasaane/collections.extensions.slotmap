using System;
using System.Runtime.CompilerServices;

namespace Collections.Extensions.SlotMaps
{
    partial class SparseSlotMap<T>
    {
        private struct DensePage
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

            public void Clear()
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