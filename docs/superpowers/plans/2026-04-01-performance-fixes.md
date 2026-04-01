# AutoFeed Performance Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Eliminate three frame-drop hotspots in the `UpdateConsumeItem` Harmony postfix: per-tick world piece scanning, redundant sqrt calls in sort, and per-container heap allocations.

**Architecture:** Fix 1 adds a per-animal container cache (TTL-based) so the expensive `Piece.s_allPieces` scan only runs every 5s per animal instead of every tick. Fix 2 pre-computes distances using `sqrMagnitude` before sorting to cut O(n log n) sqrt calls to O(n). Fix 3 builds the consumable dictionary once at patch entry and threads it through the call chain, removing the per-container `CreateItemDictionary` + `HashSet` allocations.

**Tech Stack:** C# / .NET 8, BepInEx 5, Harmony 2, xUnit (tests), Valheim Piece/Container/ZNetView APIs

---

## File Map

| File | Change |
|------|--------|
| `AutoFeed.Core/FeedingLogic.cs` | Add `IsCacheValid` helper (enables unit tests for cache TTL logic) |
| `AutoFeed.Tests/FeedingLogicTests.cs` | Add cache validity tests |
| `Extensions/Vector3Extensions.cs` | Replace `Vector3.Distance` with `sqrMagnitude`; pre-compute sort distances; add cache params + read/write |
| `Plugin.cs` | Add `ContainerCache` + `CacheTtl` config; update `GetContainersInRange` call site; build `consumableMap`; update `ContainersContainItemFromList` call site |
| `Extensions/ContainerExtensions.cs` | Replace `List<ItemDrop>` param with `Dictionary<string, ItemDrop.ItemData>`; remove `CreateItemDictionary`; inline dict lookup in `TryFindMatchingItem` |

---

## Task 1: Add `IsCacheValid` to FeedingLogic and test it

The cache timestamp check is pure logic — no Valheim types — so it belongs in `FeedingLogic.cs` where it can be unit-tested. Pattern mirrors `ShouldFeed`.

**Files:**
- Modify: `AutoFeed.Core/FeedingLogic.cs`
- Modify: `AutoFeed.Tests/FeedingLogicTests.cs`

- [ ] **Step 1: Write failing tests for `IsCacheValid`**

Add to the bottom of `AutoFeed.Tests/FeedingLogicTests.cs`:

```csharp
// --- IsCacheValid ---

[Fact]
public void IsCacheValid_ReturnsTrue_WhenWithinTtl()
{
    Assert.True(FeedingLogic.IsCacheValid(cachedTimestamp: 0f, currentTime: 4.9f, ttl: 5f));
}

[Fact]
public void IsCacheValid_ReturnsFalse_WhenTtlExpired()
{
    Assert.False(FeedingLogic.IsCacheValid(cachedTimestamp: 0f, currentTime: 5.1f, ttl: 5f));
}

[Fact]
public void IsCacheValid_ReturnsFalse_WhenExactlyAtBoundary()
{
    Assert.False(FeedingLogic.IsCacheValid(cachedTimestamp: 0f, currentTime: 5f, ttl: 5f));
}

[Fact]
public void IsCacheValid_ReturnsTrue_WhenCachedJustNow()
{
    Assert.True(FeedingLogic.IsCacheValid(cachedTimestamp: 10f, currentTime: 10f, ttl: 5f));
}
```

- [ ] **Step 2: Run tests — expect failure**

```bash
dotnet test AutoFeed.Tests/AutoFeed.Tests.csproj --filter "IsCacheValid" -v minimal
```

Expected: compile error — `IsCacheValid` does not exist yet.

- [ ] **Step 3: Add `IsCacheValid` to `FeedingLogic.cs`**

Add after the `ShouldFeed` method in `AutoFeed.Core/FeedingLogic.cs`:

```csharp
/// <summary>
/// Returns true if the cached value is still within the TTL window.
/// </summary>
public static bool IsCacheValid(float cachedTimestamp, float currentTime, float ttl) =>
    currentTime - cachedTimestamp < ttl;
```

- [ ] **Step 4: Run tests — expect pass**

```bash
dotnet test AutoFeed.Tests/AutoFeed.Tests.csproj --filter "IsCacheValid" -v minimal
```

Expected: all 4 tests pass.

- [ ] **Step 5: Run full test suite — no regressions**

```bash
dotnet test AutoFeed.Tests/AutoFeed.Tests.csproj -v minimal
```

Expected: all tests pass.

- [ ] **Step 6: Commit**

```bash
git add AutoFeed.Core/FeedingLogic.cs AutoFeed.Tests/FeedingLogicTests.cs
git commit -m "feat: add IsCacheValid helper to FeedingLogic with tests"
```

---

## Task 2: Fix 2 — Pre-compute distances in `GetContainersInRange`

Replace `Vector3.Distance` (sqrt) with `sqrMagnitude` in both the range filter and the sort.

**Files:**
- Modify: `Extensions/Vector3Extensions.cs`

- [ ] **Step 1: Replace the range check and sort in `Vector3Extensions.cs`**

Current file content for reference (full method):

```csharp
public static List<Container> GetContainersInRange(this Vector3 center, float radiusRange)
{
    var result = new List<Container>();
    foreach (var piece in Piece.s_allPieces)
    {
        if (piece == null) continue;
        var container = piece.GetComponent<Container>();
        if (container == null) continue;
        var dist = Vector3.Distance(piece.transform.position, center);
        if (dist <= radiusRange && IsValidZNetView(container.GetComponent<ZNetView>()) && IsEligibleContainer(container))
            result.Add(container);
    }
    result.Sort((a, b) => Vector3.Distance(a.transform.position, center).CompareTo(Vector3.Distance(b.transform.position, center)));
    return result;
}
```

Replace the **range check line** and the **sort** only (leave the signature and rest of the loop intact for now — the signature changes in Task 3):

```csharp
public static List<Container> GetContainersInRange(this Vector3 center, float radiusRange)
{
    var sqrRadius = radiusRange * radiusRange;
    var result = new List<Container>();
    foreach (var piece in Piece.s_allPieces)
    {
        if (piece == null) continue;
        var container = piece.GetComponent<Container>();
        if (container == null) continue;
        var sqrDist = (piece.transform.position - center).sqrMagnitude;
        if (sqrDist <= sqrRadius && IsValidZNetView(container.GetComponent<ZNetView>()) && IsEligibleContainer(container))
            result.Add(container);
    }

    var sorted = result
        .Select(c => (container: c, sqrDist: (c.transform.position - center).sqrMagnitude))
        .OrderBy(x => x.sqrDist)
        .Select(x => x.container)
        .ToList();

    return sorted;
}
```

- [ ] **Step 2: Verify project builds**

```bash
dotnet build AutoFeed.Core/AutoFeed.Core.csproj -v minimal 2>&1 | tail -5
```

Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add Extensions/Vector3Extensions.cs
git commit -m "perf: replace Vector3.Distance with sqrMagnitude in container range/sort"
```

---

## Task 3: Fix 1 — Add container cache to Plugin and update `GetContainersInRange`

Wire up the cache: add the static dictionary and `CacheTtl` config to `Plugin.cs`, then update `GetContainersInRange` to accept `animalId`/`currentTime` and read/write the cache.

**Files:**
- Modify: `Plugin.cs`
- Modify: `Extensions/Vector3Extensions.cs`

- [ ] **Step 1: Add `ContainerCache` and `CacheTtl` to `Plugin.cs`**

In `Plugin.cs`, add next to the existing `LastFeedTimes` declaration:

```csharp
internal static readonly Dictionary<int, (float timestamp, List<Container> containers)> ContainerCache = new();
```

Add `CacheTtl` config in the `Awake()` method, after the `ChestPrefix` binding:

```csharp
CacheTtl = Config.Bind(
    "General",
    "Container Cache TTL",
    5f,
    "Seconds before the nearby-container list is refreshed for each animal."
);
```

Add the field declaration alongside `ContainerRange`, `ModEnabled`, `ChestPrefix`:

```csharp
public static ConfigEntry<float> CacheTtl = default!;
```

- [ ] **Step 2: Update `GetContainersInRange` signature to accept cache parameters**

In `Extensions/Vector3Extensions.cs`, update the method signature and add cache logic at the top:

```csharp
public static List<Container> GetContainersInRange(this Vector3 center, float radiusRange, int animalId, float currentTime)
{
    if (Plugin.ContainerCache.TryGetValue(animalId, out var entry)
        && FeedingLogic.IsCacheValid(entry.timestamp, currentTime, Plugin.CacheTtl.Value))
        return entry.containers;

    var sqrRadius = radiusRange * radiusRange;
    var result = new List<Container>();
    foreach (var piece in Piece.s_allPieces)
    {
        if (piece == null) continue;
        var container = piece.GetComponent<Container>();
        if (container == null) continue;
        var sqrDist = (piece.transform.position - center).sqrMagnitude;
        if (sqrDist <= sqrRadius && IsValidZNetView(container.GetComponent<ZNetView>()) && IsEligibleContainer(container))
            result.Add(container);
    }

    var sorted = result
        .Select(c => (container: c, sqrDist: (c.transform.position - center).sqrMagnitude))
        .OrderBy(x => x.sqrDist)
        .Select(x => x.container)
        .ToList();

    Plugin.ContainerCache[animalId] = (currentTime, sorted);
    return sorted;
}
```

- [ ] **Step 3: Update the call site in `Plugin.cs` Postfix**

The existing call (around line 76):

```csharp
var nearbyContainers =
    ___m_character.gameObject.transform.position.GetContainersInRange(
        ContainerRange.Value
    );
```

Replace with:

```csharp
var nearbyContainers =
    ___m_character.gameObject.transform.position.GetContainersInRange(
        ContainerRange.Value, animalId, Time.time
    );
```

Note: `animalId` is already declared earlier in the Postfix at the line `var animalId = ___m_character.GetInstanceID();` (this line exists in the current code and is unchanged). `Time.time` is already available via global usings.

- [ ] **Step 4: Verify project builds**

```bash
dotnet build AutoFeed.Core/AutoFeed.Core.csproj -v minimal 2>&1 | tail -5
```

Expected: `Build succeeded.`

- [ ] **Step 5: Run full test suite — no regressions**

```bash
dotnet test AutoFeed.Tests/AutoFeed.Tests.csproj -v minimal
```

Expected: all tests pass.

- [ ] **Step 6: Commit**

```bash
git add Plugin.cs Extensions/Vector3Extensions.cs
git commit -m "perf: add per-animal container scan cache with configurable TTL"
```

---

## Task 4: Fix 3 — Thread consumable map through ContainerExtensions

Replace the `List<ItemDrop>` → `CreateItemDictionary` pipeline with a `Dictionary<string, ItemDrop.ItemData>` passed from patch entry, eliminating per-container allocations.

**Files:**
- Modify: `Extensions/ContainerExtensions.cs`
- Modify: `Plugin.cs`

- [ ] **Step 1: Rewrite `ContainerExtensions.cs`**

Replace the entire file content:

```csharp
namespace AutoFeed;

public static class ContainerExtensions
{
    public static bool ContainersContainItemFromList(
        this List<Container> containers,
        Dictionary<string, ItemDrop.ItemData> consumableMap,
        out Container? targetContainer,
        out ItemDrop.ItemData? targetItem
    ) =>
        FindItemInContainers(containers, consumableMap, out targetContainer, out targetItem);

    private static bool FindItemInContainers(
        List<Container> containers,
        Dictionary<string, ItemDrop.ItemData> consumableMap,
        out Container? targetContainer,
        out ItemDrop.ItemData? targetItem
    )
    {
        foreach (var container in containers)
        {
            var items = container.GetInventory().GetAllItems();
            if (TryFindMatchingItem(items, consumableMap, out targetItem))
            {
                targetContainer = container;
                return true;
            }
        }

        targetContainer = null;
        targetItem = null;
        return false;
    }

    private static bool TryFindMatchingItem(
        List<ItemDrop.ItemData> items,
        Dictionary<string, ItemDrop.ItemData> consumableMap,
        out ItemDrop.ItemData? targetItem
    )
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
}
```

- [ ] **Step 2: Update the call site in `Plugin.cs` Postfix**

In the Postfix, add `consumableMap` build just before the `GetContainersInRange` call (or anywhere before `ContainersContainItemFromList`):

```csharp
var consumableMap = ___m_consumeItems
    .ToDictionary(i => i.m_itemData.m_shared.m_name, i => i.m_itemData);
```

Then update the `ContainersContainItemFromList` call:

```csharp
var foundContainerWithFood = nearbyContainers.ContainersContainItemFromList(
    consumableMap,
    out var container,
    out var item
);
```

The old call (currently around line 80–84 in `Plugin.cs`) looks like:

```csharp
var foundContainerWithFood = nearbyContainers.ContainersContainItemFromList(
    ___m_consumeItems,
    out var container,
    out var item
);
```

Replace it with the new call above (passing `consumableMap` instead of `___m_consumeItems`).

- [ ] **Step 3: Verify project builds**

```bash
dotnet build AutoFeed.Core/AutoFeed.Core.csproj -v minimal 2>&1 | tail -5
```

Expected: `Build succeeded.`

- [ ] **Step 4: Run full test suite — no regressions**

```bash
dotnet test AutoFeed.Tests/AutoFeed.Tests.csproj -v minimal
```

Expected: all tests pass.

- [ ] **Step 5: Commit**

```bash
git add Extensions/ContainerExtensions.cs Plugin.cs
git commit -m "perf: build consumable map once at patch entry, thread through call chain"
```

---

> **Note on test coverage:** `GetContainersInRange` cache hit/miss behaviour cannot be unit tested — it depends on Valheim types (`Container`, `Piece`, `ZNetView`) that are unavailable outside the game runtime. The `IsCacheValid` tests in Task 1 cover the TTL logic in isolation, which is the only pure-C# piece of the cache. Integration verification happens by running the mod on the server.

## Task 5: Final verification

- [ ] **Step 1: Run full test suite one final time**

```bash
dotnet test AutoFeed.Tests/AutoFeed.Tests.csproj -v normal
```

Expected: all tests pass, no warnings about missing references.

- [ ] **Step 2: Verify build is clean**

```bash
dotnet build AutoFeed.Core/AutoFeed.Core.csproj -v minimal 2>&1
```

Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`
