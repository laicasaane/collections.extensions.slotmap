# Slot Map

## Acknowledgement

Inspired by:

- https://github.com/SergeyMakeev/slot_map

## Introduction

A slot map is an associative collection with persistent unique keys to access stored values. Upon adding, a key is returned that can be used to later get or remove the values. `Add`, `Remove`, `Get` methods all take O(1) time with low overhead.

Great for storing collections of objects that need stable, safe references but have no clear ownership.

The difference between a `Dictionary<TKey, TValue>` and a `SlotMap<TValue>` is that the slot map generates and returns the key when adding a value. A key is always unique and will only refer to the value that was added.

## Basic Usage

```cs
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

var newItem01 = slotmap.Get(key01);
// SlotMapException: Cannot get value because `key.Version`
// is different from the current version. key.Version: 1. Current version: 2.
```
