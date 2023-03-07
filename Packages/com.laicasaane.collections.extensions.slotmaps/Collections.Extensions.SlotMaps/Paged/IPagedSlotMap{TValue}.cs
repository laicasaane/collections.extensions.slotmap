namespace Collections.Extensions.SlotMaps
{
    public interface IReadOnlyPagedSlotMap<TValue> : IReadOnlySlotMap<TValue>
    {
        uint PageSize { get; }

        int PageCount { get; }
    }

    public interface IPagedSlotMap<TValue> : IReadOnlyPagedSlotMap<TValue>, ISlotMap<TValue>
    {
    }
}