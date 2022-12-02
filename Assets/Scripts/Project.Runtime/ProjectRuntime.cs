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
            var slotmap = new SlotMap<int>();
            var key01 = slotmap.Add(8);
            var key02 = slotmap.Add(9);
            var key03 = slotmap.Add(22);

            slotmap.Remove(key02);

            Debug.Assert(slotmap.Contains(key02)); // false
            Debug.Assert(slotmap.Contains(key03)); // true

            var item03 = slotmap.Get(key03);
            Debug.Assert(item03 == 22); // true

            ref readonly var item01 = ref slotmap.GetRef(key01);
            Debug.Assert(item01 == 9); // false

            var newKey01 = slotmap.Replace(key01, 53);
            Debug.Assert(slotmap.Get(newKey01) == 53); // true

            var newItem01 = slotmap.Get(key01); // exception: wrong version
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
