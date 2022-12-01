using System;

namespace Collections.Extensions.SlotMaps
{
    public interface IReadOnlySlotMap<T>
    {
        uint FreeIndicesLimit { get; }

        uint ItemCount { get; }

        int PageCount { get; }

        uint PageSize { get; }

        uint TombstoneCount { get; }

        bool Contains(SlotKey key);

        T Get(SlotKey key);

        void GetRange(in ReadOnlySpan<SlotKey> keys, Span<T> returnItems);

        ref readonly T GetRef(SlotKey key);

        ref readonly T GetRefNotThrow(SlotKey key);

        bool TryGet(SlotKey key, out T item);

        bool TryGetRange(
              in ReadOnlySpan<SlotKey> keys
            , Span<SlotKey> returnKeys
            , Span<T> returnItems
            , out uint returnItemCount
        );
    }

    public interface ISlotMap<T> : IReadOnlySlotMap<T>
    {
        SlotKey Add(T item);

        void AddRange(in ReadOnlySpan<T> items, Span<SlotKey> returnKeys);

        bool Remove(SlotKey key);

        void RemoveRange(in ReadOnlySpan<SlotKey> keys);

        SlotKey Replace(SlotKey key, T item);

        void Reset();

        bool TryAdd(T item, out SlotKey key);

        bool TryReplace(SlotKey key, T item, out SlotKey newKey);

        bool TryAddRange(
              in ReadOnlySpan<T> items
            , Span<SlotKey> returnKeys
            , out uint returnKeyCount
        );
    }
}