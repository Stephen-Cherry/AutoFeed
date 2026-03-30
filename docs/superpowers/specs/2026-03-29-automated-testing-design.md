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

```
AutoFeed/
├── AutoFeed.Core/
│   ├── AutoFeed.Core.csproj    # net48, no game DLL refs
│   └── FeedingLogic.cs         # all extracted logic
│
├── AutoFeed/                   # existing plugin
│   ├── AutoFeed.csproj         # references AutoFeed.Core + game DLLs
│   ├── Plugin.cs
│   ├── PluginSettings.cs
│   └── Extensions/             # thin adapters calling FeedingLogic
│
└── AutoFeed.Tests/
    ├── AutoFeed.Tests.csproj   # references AutoFeed.Core only
    └── FeedingLogicTests.cs
```

CI installs only the .NET SDK. No game DLLs required anywhere in the test pipeline.

## AutoFeed.Core

### AutoFeed.Core.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <AssemblyName>AutoFeed.Core</AssemblyName>
    <Nullable>enable</Nullable>
    <LangVersion>preview</LangVersion>
  </PropertyGroup>
</Project>
```

### FeedingLogic.cs

```csharp
namespace AutoFeed.Core;

public static class FeedingLogic
{
    /// <summary>
    /// Returns the name of the first inventory item that matches a consumable,
    /// or null if none found.
    /// </summary>
    public static string? FindConsumableInInventory(
        IEnumerable<string> inventoryItemNames,
        IReadOnlySet<string> consumableNames)
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

## Adapter Changes

### MonsterAIExtensions.cs

Replace `FeedIntervalPassed` call with `FeedingLogic.ShouldFeed`. Delete `FeedIntervalPassed`.

```csharp
var animalId = ___m_character.GetInstanceID();
if (FeedingLogic.ShouldFeed(animalId, Time.time, Plugin.LastFeedTimes, PluginSettings.FeedInterval))
{
    FeedAnimal(__instance, ___m_tamable, ___m_character, container, item);
    Plugin.LastFeedTimes[animalId] = Time.time;
}
```

### ColliderExtensions.cs

`IsNonEmptyChest` delegates to `IsEligibleContainer`:

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

### ContainerExtensions.cs

`TryFindMatchingItem` extracts item names and delegates to `FindConsumableInInventory`:

```csharp
private static bool TryFindMatchingItem(
    List<ItemDrop.ItemData> items,
    Dictionary<string, List<ItemDrop.ItemData>> itemDropDict,
    out ItemDrop.ItemData? targetItem)
{
    var consumableNames = itemDropDict.Keys.ToHashSet();
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

`CreateItemDictionary` is unchanged.

## AutoFeed.Tests

### AutoFeed.Tests.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <IsPackable>false</IsPackable>
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

**FindConsumableInInventory (3 tests):**
- Returns matched name when inventory contains consumable
- Returns null when no match
- Returns null when inventory empty

**ShouldFeed (4 tests):**
- Returns true when animal never fed
- Returns true when interval elapsed
- Returns false when interval not elapsed
- Tracks animals independently (animal 1 throttled, animal 2 not)

**IsEligibleContainer (4 tests):**
- Returns true when prefix matches and has items
- Returns false when empty
- Returns false when prefix mismatch
- Returns true when prefix empty (allows any container)

## CI Integration

Add a GitHub Actions workflow that runs `dotnet test AutoFeed.Tests` on push and pull request. Branch protection on `main` requires this check to pass before merge.

## Solution File

`AutoFeed.sln` must be updated to include `AutoFeed.Core` and `AutoFeed.Tests` projects.
