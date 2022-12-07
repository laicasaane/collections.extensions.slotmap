# Slot Map

## Acknowledgement

This library cannot exist without the knowledge and inspiration from:

- https://github.com/SergeyMakeev/slot_map
- https://floooh.github.io/2018/06/17/handles-vs-pointers.html

## Introduction

A slot map is an associative collection with persistent unique keys to access stored values. Upon adding, a key is returned that can be used to later get or remove the values. `Add`, `Remove`, `Get` methods all take O(1) time with low overhead.

Great for storing collections of objects that need stable, safe references but have no clear ownership.

The difference between a `Dictionary<TKey, TValue>` and a `SlotMap<TValue>` is that the slot map generates and returns the key when adding a value. A key is always unique and will only refer to the value that was added.

## Installation

### Install via OpenUPM

You can install this package from the [Open UPM](https://openupm.com/packages/com.laicasaane.collections.extensions.slotmap/) registry.

More details [here](https://github.com/openupm/openupm-cli#installation).

```
openupm add org.nuget.system.runtime.compilerservices.unsafe
openupm add com.laicasaane.collections.extensions.slotmaps
```


### Install via Package Manager

1. Open the **Poject Settings** window
2. Navigate to the **Package Manager** section
3. Add a new **Scoped Registry**

```
"name": "Unity NuGet",
"url": "https://unitynuget-registry.azurewebsites.net",
"scopes": [
    "org.nuget"
]
```

4. Open the **Package Manager** window
5. Select the **Add package from git URL** option from the `+` dropdown
6. Enter this git url:

```
https://github.com/laicasaane/collections.extensions.slotmap.git?path=Packages/com.laicasaane.collections.extensions.slotmap
```

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
