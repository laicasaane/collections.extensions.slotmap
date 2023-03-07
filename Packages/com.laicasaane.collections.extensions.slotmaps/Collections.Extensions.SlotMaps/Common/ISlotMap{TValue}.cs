using System;
using System.Collections.Generic;

namespace Collections.Extensions.SlotMaps
{
    public interface IReadOnlySlotMap<TValue> : IEnumerable<KeyValuePair<SlotKey, TValue>>
    {
        uint FreeIndicesLimit { get; }

        uint SlotCount { get; }

        uint TombstoneCount { get; }

        bool Contains(in SlotKey key);

        TValue Get(in SlotKey key);

        void GetRange(in ReadOnlySpan<SlotKey> keys, in Span<TValue> returnItems);

        bool TryGet(in SlotKey key, out TValue value);

        bool TryGetRange(
              in ReadOnlySpan<SlotKey> keys
            , in Span<SlotKey> returnKeys
            , in Span<TValue> returnValues
            , out uint returnItemCount
        );

        SlotKey UpdateVersion(in SlotKey key);

        bool TryUpdateVersion(in SlotKey key, out SlotKey newKey);
    }

    public interface ISlotMap<TValue> : IReadOnlySlotMap<TValue>
    {
        SlotKey Add(TValue value);

        void AddRange(in ReadOnlySpan<TValue> values, in Span<SlotKey> returnKeys);

        bool Remove(in SlotKey key);

        void RemoveRange(in ReadOnlySpan<SlotKey> keys);

        SlotKey Replace(in SlotKey key, TValue value);

        void Reset();

        bool TryAdd(TValue value, out SlotKey key);

        bool TryReplace(in SlotKey key, TValue value, out SlotKey newKey);

        bool TryAddRange(
              in ReadOnlySpan<TValue> values
            , in Span<SlotKey> returnKeys
            , out uint returnKeyCount
        );
    }
}