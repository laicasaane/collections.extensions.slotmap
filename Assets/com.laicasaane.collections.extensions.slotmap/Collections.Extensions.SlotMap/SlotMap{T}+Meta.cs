namespace Collections.Extensions.SlotMap
{
    partial class SlotMap<T>
    {
        private struct Meta
        {
            public SlotVersion version;
            public bool inactive;
            public bool tombstone;
        }
    }
}
