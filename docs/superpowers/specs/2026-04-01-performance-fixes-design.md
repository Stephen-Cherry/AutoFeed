# AutoFeed Performance Fixes — Design Spec

**Date:** 2026-04-01
**Status:** Approved

## Problem

The `UpdateConsumeItem` Harmony postfix fires repeatedly per tamed animal. Three hotspots cause frame drops as the world scales:

1. `GetContainersInRange` iterates every piece in `Piece.s_allPieces` with multiple `GetComponent` calls per piece — O(n) per animal per tick.
2. The sort comparator recalculates `Vector3.Distance` (involves `sqrt`) for both sides of every comparison — O(n log n) `sqrt` calls instead of O(n).
3. `CreateItemDictionary` and a `HashSet<string>` are heap-allocated on every feed check, creating GC pressure.

## Fix 1 — Per-Animal Container Scan Cache

**Location:** `Plugin.cs` + `Vector3Extensions.cs`

Add a static cache to `Plugin`:

```csharp
internal static readonly Dictionary<int, (float timestamp, List<Container> containers)> ContainerCache = new();
```

Keyed by animal `GetInstanceID()`. Add a `CacheTtl` `ConfigEntry<float>` (default `5f` seconds).

In `GetContainersInRange`, accept the animal ID and current time. If a cache entry exists and `currentTime - timestamp < CacheTtl`, return the cached list. Otherwise perform the scan, store the result, and return it.

Cache entries for dead animals expire naturally after `CacheTtl` without explicit cleanup.

## Fix 2 — Pre-Compute Distances Before Sorting

**Location:** `Vector3Extensions.cs`

Replace the sort comparator with a pre-computation step:

1. After building `result`, compute `(container, sqrMagnitude)` pairs once.
2. Sort by pre-computed `sqrMagnitude`.
3. Project back to `List<Container>`.

Also replace `Vector3.Distance` in the range check with `(piece.transform.position - center).sqrMagnitude <= radiusRange * radiusRange` to eliminate `sqrt` from the scan loop entirely.

## Fix 3 — Build Consumable HashSet Once at Patch Entry

**Location:** `Plugin.cs` + `ContainerExtensions.cs`

In `UpdateConsumeItemPatch.Postfix`, build `HashSet<string> consumableNames` once from `___m_consumeItems`:

```csharp
var consumableNames = new HashSet<string>(___m_consumeItems.Select(i => i.m_itemData.m_shared.m_name));
```

Pass `consumableNames` through the call chain:

- `ContainersContainItemFromList(containers, consumableNames, out container, out item)`
- `FindItemInContainers` → `TryFindMatchingItem`

Remove `CreateItemDictionary` entirely. `TryFindMatchingItem` uses `consumableNames` (HashSet) to find the matching name, then retrieves the `ItemData` by looking it up in the original `___m_consumeItems` list (passed through or re-queried). This eliminates the `Dictionary<string, List<ItemDrop.ItemData>>` allocation and the per-container `HashSet` reconstruction.

## Affected Files

| File | Change |
|------|--------|
| `Plugin.cs` | Add `ContainerCache`, `CacheTtl` config; build `consumableNames` at patch entry; pass animal ID + time to range query |
| `Extensions/Vector3Extensions.cs` | Accept cache parameters; add sqrMagnitude range check; pre-compute sort distances |
| `Extensions/ContainerExtensions.cs` | Replace `CreateItemDictionary` with `HashSet<string>` parameter; remove dictionary allocation |

## What Is Not Changing

- `FeedingLogic.cs` — `FindConsumableInInventory` already accepts `HashSet<string>`, no changes needed
- `MonsterAIExtensions.cs` — feed execution path is not a hotspot
- `PluginSettings.cs` — `FeedInterval` remains unchanged; `CacheTtl` is a new `ConfigEntry` in `Plugin.cs`
- All existing tests remain valid; new tests for cache behavior should be added in `AutoFeed.Tests`
