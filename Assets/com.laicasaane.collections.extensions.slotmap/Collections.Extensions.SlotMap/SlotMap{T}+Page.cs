namespace Collections.Extensions.SlotMap
{
    partial class SlotMap<T>
    {
        private class Page
        {
            private readonly T[] _slots;
            private uint _count;

            public Page(uint size)
            {
                _slots = new T[size];
                _count = 0;
            }

            public uint Count => _count;

            public void Add(uint index, T value)
            {
                _slots[index] = value;
                _count++;
            }

            public void Remove(uint index)
            {
                _slots[index] = default;
                _count--;
            }
        }
    }
}
