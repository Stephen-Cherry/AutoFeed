# Automated Testing Design

## Goal

Extract pure logic from the AutoFeed mod into a dependency-free class library so it can be unit tested in CI without requiring Valheim game DLLs.

## Scope

Three logic pieces are extracted. Everything else (Harmony patch wiring, inventory mutation, animation triggers) is out of scope — those paths are simple game API delegations with no branching logic worth testing.

| Logic | Location | What it decides |
|-------|----------|-----------------|
| Item matching | `ContainerExtensions` | Does this container's inventory contain a consumable? |
| Feed throttle | `MonsterAIExtensions` | Has enough time passed to feed this animal again? |
| Container eligibility | `ColliderExtensions` | Is this container eligible for auto-feeding? |

## Project Structure

The repo root IS the plugin directory — `AutoFeed.csproj` and `AutoFeed.sln` live at the root. New projects are siblings alongside the existing files:

```
(repo root)  ← /home/narolith/Projects/AutoFeed/
├── AutoFeed.csproj             # existing plugin (net48)
├── AutoFeed.sln                # existing solution — add new projects to this
├── GlobalUsing.cs              # add: global using AutoFeed.Core;
├── Plugin.cs
├── PluginSettings.cs
├── Extensions/                 # thin adapters calling FeedingLogic
│
├── AutoFeed.Core/              # new
│   ├── AutoFeed.Core.csproj    # netstandard2.0 — compatible with net48 (plugin) and net8 (tests)
│   └── FeedingLogic.cs
│
├── AutoFeed.Tests/             # new
│   ├── AutoFeed.Tests.csproj   # net8.0 — runs on ubuntu-latest CI without Mono
│   └── FeedingLogicTests.cs
│
└── .github/                    # new
    └── workflows/
        └── test.yml
```

**Targeting rationale:**
- `AutoFeed.Core` targets `netstandard2.0` so it is compatible with both the `net48` plugin and the `net8.0` test project.
- `AutoFeed.Tests` targets `net8.0` so tests run on a standard `ubuntu-latest` GitHub Actions runner without Mono.
- `AutoFeed` (plugin) stays on `net48` — required by BepInEx/Valheim.

## AutoFeed.Core

### AutoFeed.Core.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <AssemblyName>AutoFeed.Core</AssemblyName>
    <Nullable>enable</Nullable>
    <LangVersion>preview</LangVersion>
  </PropertyGroup>
</Project>
```

### FeedingLogic.cs

```csharp
using System.Collections.Generic;

namespace AutoFeed.Core;

public static class FeedingLogic
{
    /// <summary>
    /// Returns the name of the first inventory item that matches a consumable,
    /// or null if none found.
    /// </summary>
    public static string? FindConsumableInInventory(
        IEnumerable<string> inventoryItemNames,
        HashSet<string> consumableNames)
    {
        foreach (var name in inventoryItemNames)
            if (consumableNames.Contains(name))
                return name;
        return null;
    }

    /// <summary>
    /// Returns true if enough time has passed since this animal was last fed.
    /// </summary>
    public static bool ShouldFeed(
        int animalId,
        float currentTime,
        IReadOnlyDictionary<int, float> lastFeedTimes,
        float interval)
    {
        if (!lastFeedTimes.TryGetValue(animalId, out float lastTime))
            return true;
        return currentTime - lastTime >= interval;
    }

    /// <summary>
    /// Returns true if the container is eligible for auto-feeding.
    /// Prefix check is skipped when prefix is null or empty.
    /// </summary>
    public static bool IsEligibleContainer(
        string containerName,
        int itemCount,
        string prefix)
    {
        if (!string.IsNullOrEmpty(prefix) && !containerName.StartsWith(prefix))
            return false;
        return itemCount > 0;
    }
}
```

Note: `IReadOnlySet<T>` is not available in `netstandard2.0` (introduced in .NET 5). `HashSet<string>` is used instead — callers already construct a HashSet so there is no extra allocation, and `Contains` remains O(1).

## Plugin Changes

### AutoFeed.csproj

Add a `ProjectReference` to `AutoFeed.Core`:

```xml
<ItemGroup>
  <ProjectReference Include="..\AutoFeed.Core\AutoFeed.Core.csproj" />
</ItemGroup>
```

### GlobalUsing.cs

Add one line:

```csharp
global using AutoFeed.Core;
```

This makes `FeedingLogic` available without a per-file using in the adapter files.

### Adapter Changes

**`MonsterAIExtensions.cs`** — replace `FeedIntervalPassed` with `FeedingLogic.ShouldFeed`. Delete `FeedIntervalPassed`.

```csharp
var animalId = ___m_character.GetInstanceID();
if (FeedingLogic.ShouldFeed(animalId, Time.time, Plugin.LastFeedTimes, PluginSettings.FeedInterval))
{
    FeedAnimal(__instance, ___m_tamable, ___m_character, container, item);
    Plugin.LastFeedTimes[animalId] = Time.time;
}
```

**`ColliderExtensions.cs`** — `IsNonEmptyChest` delegates to `IsEligibleContainer`. The `?.` operator preserves the existing null-safety guard on `GetInventory()`:

```csharp
private static bool IsNonEmptyChest(Container container)
{
    var inventory = container.GetInventory();
    return FeedingLogic.IsEligibleContainer(
        container.name,
        inventory?.NrOfItems() ?? 0,
        Plugin.ChestPrefix.Value
    );
}
```

**`ContainerExtensions.cs`** — `TryFindMatchingItem` extracts item names and delegates to `FindConsumableInInventory`. `CreateItemDictionary` is unchanged.

```csharp
private static bool TryFindMatchingItem(
    List<ItemDrop.ItemData> items,
    Dictionary<string, List<ItemDrop.ItemData>> itemDropDict,
    out ItemDrop.ItemData? targetItem)
{
    var consumableNames = new HashSet<string>(itemDropDict.Keys);
    var match = FeedingLogic.FindConsumableInInventory(
        items.Select(i => i.m_shared.m_name),
        consumableNames
    );

    if (match is not null)
    {
        targetItem = itemDropDict[match].First();
        return true;
    }

    targetItem = null;
    return false;
}
```

## AutoFeed.Tests

### AutoFeed.Tests.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
    <PackageReference Include="xunit" Version="2.*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.*" PrivateAssets="all" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\AutoFeed.Core\AutoFeed.Core.csproj" />
  </ItemGroup>
</Project>
```

### FeedingLogicTests.cs (11 tests)

```csharp
using System.Collections.Generic;
using AutoFeed.Core;
using Xunit;

namespace AutoFeed.Tests;

public class FeedingLogicTests
{
    // FindConsumableInInventory

    [Fact]
    public void FindConsumable_ReturnsMatchedName_WhenInventoryContainsConsumable()
    {
        var result = FeedingLogic.FindConsumableInInventory(
            new[] { "Raspberry", "Mushroom", "Carrot" },
            new HashSet<string> { "Carrot", "Turnip" }
        );
        Assert.Equal("Carrot", result);
    }

    [Fact]
    public void FindConsumable_ReturnsNull_WhenNoMatch()
    {
        var result = FeedingLogic.FindConsumableInInventory(
            new[] { "Raspberry", "Mushroom" },
            new HashSet<string> { "Carrot", "Turnip" }
        );
        Assert.Null(result);
    }

    [Fact]
    public void FindConsumable_ReturnsNull_WhenInventoryEmpty()
    {
        var result = FeedingLogic.FindConsumableInInventory(
            System.Array.Empty<string>(),
            new HashSet<string> { "Carrot" }
        );
        Assert.Null(result);
    }

    // ShouldFeed

    [Fact]
    public void ShouldFeed_ReturnsTrue_WhenAnimalNeverFed()
    {
        var result = FeedingLogic.ShouldFeed(
            animalId: 1,
            currentTime: 5f,
            lastFeedTimes: new Dictionary<int, float>(),
            interval: 0.1f
        );
        Assert.True(result);
    }

    [Fact]
    public void ShouldFeed_ReturnsTrue_WhenIntervalElapsed()
    {
        var result = FeedingLogic.ShouldFeed(
            animalId: 1,
            currentTime: 5.2f,
            lastFeedTimes: new Dictionary<int, float> { [1] = 5.0f },
            interval: 0.1f
        );
        Assert.True(result);
    }

    [Fact]
    public void ShouldFeed_ReturnsFalse_WhenIntervalNotElapsed()
    {
        var result = FeedingLogic.ShouldFeed(
            animalId: 1,
            currentTime: 5.05f,
            lastFeedTimes: new Dictionary<int, float> { [1] = 5.0f },
            interval: 0.1f
        );
        Assert.False(result);
    }

    [Fact]
    public void ShouldFeed_TracksAnimalsIndependently()
    {
        var lastFeedTimes = new Dictionary<int, float> { [1] = 5.0f };

        Assert.False(FeedingLogic.ShouldFeed(1, 5.05f, lastFeedTimes, 0.1f));
        Assert.True(FeedingLogic.ShouldFeed(2, 5.05f, lastFeedTimes, 0.1f));
    }

    // IsEligibleContainer

    [Fact]
    public void IsEligibleContainer_ReturnsTrue_WhenPrefixMatchesAndHasItems()
    {
        Assert.True(FeedingLogic.IsEligibleContainer("piece_chest_wood", 3, "piece_chest"));
    }

    [Fact]
    public void IsEligibleContainer_ReturnsFalse_WhenEmpty()
    {
        Assert.False(FeedingLogic.IsEligibleContainer("piece_chest_wood", 0, "piece_chest"));
    }

    [Fact]
    public void IsEligibleContainer_ReturnsFalse_WhenPrefixMismatch()
    {
        Assert.False(FeedingLogic.IsEligibleContainer("Cart", 5, "piece_chest"));
    }

    [Fact]
    public void IsEligibleContainer_ReturnsTrue_WhenPrefixEmpty_AllowsAnyContainer()
    {
        Assert.True(FeedingLogic.IsEligibleContainer("Cart", 5, ""));
    }
}
```

## CI Workflow

**File:** `.github/workflows/test.yml`

```yaml
name: Tests

on:
  push:
    branches: [ main ]
  pull_request:
    branches: [ main ]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Restore
        run: dotnet restore AutoFeed.Tests/AutoFeed.Tests.csproj

      - name: Test
        run: dotnet test AutoFeed.Tests/AutoFeed.Tests.csproj --no-restore --verbosity normal
```

Note: Only `AutoFeed.Tests` is restored and tested in CI. The plugin project (`AutoFeed`) is not built in CI because it requires game DLLs that are not available.

## Solution File

`AutoFeed.sln` already exists at the repo root. Add both new projects to it:

```bash
dotnet sln AutoFeed.sln add AutoFeed.Core/AutoFeed.Core.csproj
dotnet sln AutoFeed.sln add AutoFeed.Tests/AutoFeed.Tests.csproj
```

## Branch Protection

After the workflow is merged to `main`, enable branch protection on GitHub:

- Settings → Branches → Add rule for `main`
- Require status checks: select the `test` job
- Require branches to be up to date before merging
