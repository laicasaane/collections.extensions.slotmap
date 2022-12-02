using System;
using System.Collections;
using System.Collections.Generic;
using Collections.Extensions.SlotMaps;
using UnityEngine;

namespace Project.Runtime
{
    public class ProjectRuntime : MonoBehaviour
    {
        private void Start()
        {
            const int MAX_INDEX = 33;

            var slotmap = new SparseSlotMap<int>(16);
            var slotkeys = new SlotKey[MAX_INDEX + 1];

            foreach (var i in 0..MAX_INDEX)
            {
                slotkeys[i] = slotmap.Add(i);
            }

            foreach (var i in 0..MAX_INDEX)
            {
                var key = slotkeys[i];
                var address = SlotAddress.FromIndex(key.Index, slotmap.PageSize);
                Debug.Log($"Add: {key} :: {address} == {i}");
            }

            var indicesToRemove = new uint[] { 1, 8, 20, 8, 5, 29 };

            foreach (var index in indicesToRemove)
            {
                ref var key = ref slotkeys[index];

                if (slotmap.Remove(key))
                {
                    var address = SlotAddress.FromIndex(key.Index, slotmap.PageSize);
                    Debug.Log($"Remove: {key} :: {address}");
                }
            }

            foreach (var (key, item) in slotmap)
            {
                Debug.Log($"{key} == {item}");
            }
        }
    }

    public static class RangeEx
    {
        public static RangeEnumerator GetEnumerator(this Range range)
        {
            if (range.Start.IsFromEnd || range.End.IsFromEnd)
            {
                throw new ArgumentException(nameof(range));
            }

            return new RangeEnumerator(range.Start.Value, range.End.Value);
        }

        public struct RangeEnumerator : IEnumerator<int>
        {
            private readonly int _start;
            private readonly int _end;
            private int _current;

            public RangeEnumerator(int start, int end)
            {
                _start = start;
                _end = end;
                _current = start - 1;
            }

            public int Current => _current;

            object IEnumerator.Current => Current;

            public bool MoveNext() => ++_current < _end;

            public void Dispose() { }

            public void Reset()
            {
                _current = _start - 1;
            }
        }
    }
}
