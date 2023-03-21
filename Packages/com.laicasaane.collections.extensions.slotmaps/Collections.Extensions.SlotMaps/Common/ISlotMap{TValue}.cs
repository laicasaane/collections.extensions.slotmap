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

        bool TryGet(in SlotKey key, out TValue value);

        SlotKey UpdateVersion(in SlotKey key);

        bool TryUpdateVersion(in SlotKey key, out SlotKey newKey);
    }

    public interface ISlotMap<TValue> : IReadOnlySlotMap<TValue>
    {
        SlotKey Add(TValue value);

        bool Remove(in SlotKey key);

        SlotKey Replace(in SlotKey key, TValue value);

        void Reset();

        bool TryAdd(TValue value, out SlotKey key);

        bool TryReplace(in SlotKey key, TValue value, out SlotKey newKey);
    }
}