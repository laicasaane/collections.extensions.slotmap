using System;
using System.Collections.Generic;

namespace Collections.Extensions.SlotMaps
{
    public interface IReadOnlySlotMap<TValue> : IEnumerable<KeyValuePair<SlotKey, TValue>>
    {
        uint FreeIndicesLimit { get; }

        uint PageSize { get; }

        int PageCount { get; }

        uint SlotCount { get; }

        uint TombstoneCount { get; }

        bool Contains(SlotKey key);

        TValue Get(SlotKey key);

        void GetRange(in ReadOnlySpan<SlotKey> keys, Span<TValue> returnItems);

        ref readonly TValue GetRef(SlotKey key);

        ref readonly TValue GetRefNotThrow(SlotKey key);

        bool TryGet(SlotKey key, out TValue item);

        bool TryGetRange(
              in ReadOnlySpan<SlotKey> keys
            , Span<SlotKey> returnKeys
            , Span<TValue> returnValues
            , out uint returnItemCount
        );
    }

    public interface ISlotMap<TValue> : IReadOnlySlotMap<TValue>
    {
        SlotKey Add(TValue value);

        void AddRange(in ReadOnlySpan<TValue> values, Span<SlotKey> returnKeys);

        bool Remove(SlotKey key);

        void RemoveRange(in ReadOnlySpan<SlotKey> keys);

        SlotKey Replace(SlotKey key, TValue value);

        void Reset();

        bool TryAdd(TValue value, out SlotKey key);

        bool TryReplace(SlotKey key, TValue value, out SlotKey newKey);

        bool TryAddRange(
              in ReadOnlySpan<TValue> values
            , Span<SlotKey> returnKeys
            , out uint returnKeyCount
        );
    }
}