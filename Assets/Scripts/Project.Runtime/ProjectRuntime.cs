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
            const int MAX_INDEX = 63;

            var slotmap = new SlotMap<string>(32);
            var slotkeys = new SlotKey[MAX_INDEX + 1];

            foreach (var i in 0..MAX_INDEX)
            {
                slotkeys[i] = slotmap.Add(i.ToString());
            }

            foreach (var i in 0..MAX_INDEX)
            {
                var key = slotkeys[i];
                var address = SlotAddress.FromIndex(key.Index, slotmap.PageSize);
                Debug.Log($"{key} = {key.Raw} :: {address} = {address.Raw}");
            }

            const int RANDOM_MAX_INDEX = 15;
            var randomKeys = new SlotKey[RANDOM_MAX_INDEX + 1];

            foreach (var i in 0..RANDOM_MAX_INDEX)
            {
                var randomIndex = UnityEngine.Random.Range(0, MAX_INDEX);
                var randomKey = slotkeys[randomIndex];
                randomKeys[i] = randomKey;

                if (slotmap.Remove(randomKey))
                {
                    Debug.Log($"Remove {randomKey}");
                }
            }

            foreach (var i in 0..RANDOM_MAX_INDEX)
            {
                var randomKey = randomKeys[i];

                Debug.Log($"Contains `{randomKey}` = {slotmap.Contains(randomKey)}");
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
