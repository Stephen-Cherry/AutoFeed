# AutoFeed Performance Fixes — Design Spec

**Date:** 2026-04-01
**Status:** Approved

## Problem

The `UpdateConsumeItem` Harmony postfix fires repeatedly per tamed animal. Three hotspots cause frame drops as the world scales:

1. `GetContainersInRange` iterates every piece in `Piece.s_allPieces` with multiple `GetComponent` calls per piece — O(n) per animal per tick.
2. The sort comparator recalculates `Vector3.Distance` (involves `sqrt`) for both sides of every comparison — O(n log n) `sqrt` calls instead of O(n).
3. `CreateItemDictionary` and a `HashSet<string>` are heap-allocated on every feed check per container, creating GC pressure.

## Fix 1 — Per-Animal Container Scan Cache

**Location:** `Plugin.cs` + `Extensions/Vector3Extensions.cs`

Add a static cache to `Plugin`:

```csharp
internal static readonly Dictionary<int, (float timestamp, List<Container> containers)> ContainerCache = new();
```

Keyed by animal `GetInstanceID()`. Add a `CacheTtl` `ConfigEntry<float>` (default `5f` seconds).

Unity's `GetInstanceID()` returns a unique integer per object instance for the lifetime of the scene; dead animals are destroyed and their IDs are not reused. The cache will remain bounded to the number of animals that have been fed during the session and requires no explicit cleanup.

Update `GetContainersInRange` signature to accept animal ID and current time:

```csharp
public static List<Container> GetContainersInRange(this Vector3 center, float radiusRange, int animalId, float currentTime)
```

If `Plugin.ContainerCache.TryGetValue(animalId, out var entry)` and `currentTime - entry.timestamp < Plugin.CacheTtl.Value`, return `entry.containers`. Otherwise perform the scan **including the distance pre-computation sort from Fix 2**, store `(currentTime, sortedResult)` in the cache, and return it. The cache stores the already-sorted list so cache hits skip sorting entirely.

The call site in `UpdateConsumeItemPatch.Postfix` becomes:

```csharp
var nearbyContainers = ___m_character.gameObject.transform.position
    .GetContainersInRange(ContainerRange.Value, animalId, Time.time);
```

## Fix 2 — Pre-Compute Distances Before Sorting

**Location:** `Extensions/Vector3Extensions.cs`

Replace the sort comparator with a pre-computation step:

1. After building `result`, compute `(container, sqrMagnitude)` pairs once into a local list.
2. Sort by pre-computed `sqrMagnitude`.
3. Project back to `List<Container>`.

Also replace `Vector3.Distance` in the range check with `(piece.transform.position - center).sqrMagnitude <= radiusRange * radiusRange` to eliminate `sqrt` from the scan loop entirely.

## Fix 3 — Build Consumable Map Once at Patch Entry

**Location:** `Plugin.cs` + `Extensions/ContainerExtensions.cs`

In `UpdateConsumeItemPatch.Postfix`, build a `Dictionary<string, ItemDrop.ItemData>` once from `___m_consumeItems`:

```csharp
var consumableMap = ___m_consumeItems
    .ToDictionary(i => i.m_itemData.m_shared.m_name, i => i.m_itemData);
```

Multiple consumables with the same name are not expected in Valheim; taking one `ItemData` per name is correct. The call site in `UpdateConsumeItemPatch.Postfix` changes to:

```csharp
var foundContainerWithFood = nearbyContainers.ContainersContainItemFromList(
    consumableMap,
    out var container,
    out var item
);
```

Pass `consumableMap` through the entire call chain:

```
ContainersContainItemFromList(containers, consumableMap, out container, out item)
  → FindItemInContainers(containers, consumableMap, ...)
    → TryFindMatchingItem(items, consumableMap, ...)
```

Full updated signatures for `ContainerExtensions.cs`:

```csharp
public static bool ContainersContainItemFromList(
    this List<Container> containers,
    Dictionary<string, ItemDrop.ItemData> consumableMap,
    out Container? targetContainer,
    out ItemDrop.ItemData? targetItem)

private static bool FindItemInContainers(
    List<Container> containers,
    Dictionary<string, ItemDrop.ItemData> consumableMap,
    out Container? targetContainer,
    out ItemDrop.ItemData? targetItem)

private static bool TryFindMatchingItem(
    List<ItemDrop.ItemData> items,
    Dictionary<string, ItemDrop.ItemData> consumableMap,
    out ItemDrop.ItemData? targetItem)
{
    foreach (var item in items)
        if (consumableMap.TryGetValue(item.m_shared.m_name, out var match))
        {
            targetItem = match;
            return true;
        }
    targetItem = null;
    return false;
}
```

Remove `CreateItemDictionary` entirely. The existing `HashSet<string>` allocation inside `TryFindMatchingItem` is also removed — direct dictionary lookup replaces it.

This eliminates:
- `CreateItemDictionary` (removed entirely)
- The `new HashSet<string>(itemDropDict.Keys)` allocation per container
- The call to `FeedingLogic.FindConsumableInInventory` (replaced by inline dict lookup)

`FeedingLogic.FindConsumableInInventory` is not changed — it remains used by unit tests.

## Affected Files

| File | Change |
|------|--------|
| `Plugin.cs` | Add `ContainerCache`, `CacheTtl` config; build `consumableMap` at patch entry; pass `animalId` + `Time.time` to `GetContainersInRange`; pass `consumableMap` to `ContainersContainItemFromList` |
| `Extensions/Vector3Extensions.cs` | Add `animalId` + `currentTime` params; add cache read/write; replace `Vector3.Distance` with `sqrMagnitude`; pre-compute sort distances |
| `Extensions/ContainerExtensions.cs` | Replace `List<ItemDrop>` param with `Dictionary<string, ItemDrop.ItemData>`; remove `CreateItemDictionary`; inline dict lookup in `TryFindMatchingItem` |

## What Is Not Changing

- `AutoFeed.Core/FeedingLogic.cs` — `FindConsumableInInventory` and `ShouldFeed` unchanged
- `Extensions/MonsterAIExtensions.cs` — feed execution path is not a hotspot
- `PluginSettings.cs` — `FeedInterval` unchanged; `CacheTtl` is a new `ConfigEntry` in `Plugin.cs`
- All existing tests remain valid; new tests for cache hit/miss behavior should be added in `AutoFeed.Tests/FeedingLogicTests.cs` (or a new `ContainerCacheTests.cs`), covering: cache hit returns same list, cache miss after TTL expiry, and different animal IDs produce independent cache entries
