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

            var slotmap = new SparseSlotMap<string>(32);
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

            Debug.Log("SPARSE PAGES");

            var sparsePages = slotmap.SparsePages.Span;
            var sparseLength = sparsePages.Length;

            for (var i = 0; i < sparseLength; i++)
            {
                ref readonly var page = ref sparsePages[i];
                var metas = page.Metas.Span;
                var denseIndices = page.DenseIndices.Span;
                var length = metas.Length;

                for (var k = 0; k < length; k++)
                {
                    Debug.Log($"{metas[k]} == {denseIndices[k]}");
                }
            }

            Debug.Log("DENSE PAGES");

            var densePages = slotmap.DensePages.Span;
            var denseLength = densePages.Length;

            for (var i = 0; i < denseLength; i++)
            {
                ref readonly var page = ref densePages[i];
                var sparseIndices = page.SparseIndices.Span;
                var items = page.Items.Span;
                var length = sparseIndices.Length;

                for (var k = 0; k < length; k++)
                {
                    Debug.Log($"{sparseIndices[k]} == {items[k]}");
                }
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
