using System;
using System.Collections;
using System.Collections.Generic;
using Collections.Extensions.SlotMap;
using UnityEngine;

namespace Project.Runtime
{
    public class ProjectRuntime : MonoBehaviour
    {
        private void Start()
        {
            var slotmap = new SlotMap<string>();
            var slotkeys = new SlotKey[10];
            
            foreach (var i in 0..9)
            {
                slotkeys[i] = slotmap.Add(i.ToString());
            }

            foreach (var i in 0..9)
            {
                Debug.Log(slotkeys[i]);
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
