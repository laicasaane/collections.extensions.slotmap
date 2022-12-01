using System;
using System.Runtime.CompilerServices;

namespace Collections.Extensions.SlotMaps
{
    partial class SparseSlotMap<T>
    {
        private struct SparsePage
        {
            private readonly SlotMeta[] _metas;
            private readonly uint[] _denseIndices;

            private uint _count;

            public SparsePage(uint size)
            {
                _metas = new SlotMeta[size];
                _denseIndices = new uint[size];
                _count = 0;
            }

            public void Clear()
            {
                Array.Clear(_metas, 0, _metas.Length);
                _count = 0;
            }
        }
    }
}