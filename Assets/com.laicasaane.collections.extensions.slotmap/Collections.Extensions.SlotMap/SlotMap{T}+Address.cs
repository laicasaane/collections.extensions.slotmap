namespace Collections.Extensions.SlotMap
{
    partial class SlotMap<T>
    {
        private readonly struct Address
        {
            public readonly uint PageIndex;
            public readonly uint ItemIndex;

            public Address(uint pageIndex, uint itemIndex)
            {
                PageIndex = pageIndex;
                ItemIndex = itemIndex;
            }

            public void Deconstruct(out uint pageIndex, out uint itemIndex)
            {
                pageIndex = PageIndex;
                itemIndex = ItemIndex;
            }
        }
    }
}
