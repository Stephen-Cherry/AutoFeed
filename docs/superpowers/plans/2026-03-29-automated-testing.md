# Automated Testing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extract pure feeding logic into a game-DLL-free class library and add xUnit tests that run in GitHub Actions CI, enforced by branch protection on `main`.

**Architecture:** A new `AutoFeed.Core` (`netstandard2.0`) class library holds `FeedingLogic.cs` — three static methods operating on primitives only. `AutoFeed.Tests` (`net8.0`) references Core and runs on `ubuntu-latest` without game DLLs. The existing plugin adapts its three affected methods to delegate to `FeedingLogic`. A GitHub Actions workflow gates merges to `main`.

**Tech Stack:** C# / netstandard2.0 / net8.0 / xUnit 2.x / GitHub Actions

---

## File Map

| File | Action | Purpose |
|------|--------|---------|
| `AutoFeed.Core/AutoFeed.Core.csproj` | Create | netstandard2.0 class library, no game deps |
| `AutoFeed.Core/FeedingLogic.cs` | Create | Three extracted logic methods |
| `AutoFeed.Tests/AutoFeed.Tests.csproj` | Create | net8.0 xUnit test project |
| `AutoFeed.Tests/FeedingLogicTests.cs` | Create | 11 tests covering all three methods |
| `AutoFeed.csproj` | Modify | Add ProjectReference to AutoFeed.Core |
| `GlobalUsing.cs` | Modify | Add `global using AutoFeed.Core;` |
| `Extensions/MonsterAIExtensions.cs` | Modify | Replace `FeedIntervalPassed` with `FeedingLogic.ShouldFeed` |
| `Extensions/ColliderExtensions.cs` | Modify | Replace `IsNonEmptyChest` body with `FeedingLogic.IsEligibleContainer` |
| `Extensions/ContainerExtensions.cs` | Modify | Replace `TryFindMatchingItem` body with `FeedingLogic.FindConsumableInInventory` |
| `AutoFeed.sln` | Modify | Add Core and Tests projects |
| `.github/workflows/test.yml` | Create | CI workflow — runs tests on push/PR to main |

---

### Task 1: Create AutoFeed.Core with stub FeedingLogic

**Files:**
- Create: `AutoFeed.Core/AutoFeed.Core.csproj`
- Create: `AutoFeed.Core/FeedingLogic.cs`

- [ ] **Step 1: Create the project file**

Create `AutoFeed.Core/AutoFeed.Core.csproj`:

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

- [ ] **Step 2: Create FeedingLogic.cs with stubs**

Create `AutoFeed.Core/FeedingLogic.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace AutoFeed.Core;

public static class FeedingLogic
{
    public static string? FindConsumableInInventory(
        IEnumerable<string> inventoryItemNames,
        HashSet<string> consumableNames) =>
        throw new NotImplementedException();

    public static bool ShouldFeed(
        int animalId,
        float currentTime,
        IReadOnlyDictionary<int, float> lastFeedTimes,
        float interval) =>
        throw new NotImplementedException();

    public static bool IsEligibleContainer(
        string containerName,
        int itemCount,
        string prefix) =>
        throw new NotImplementedException();
}
```

- [ ] **Step 3: Build to verify the project compiles**

Run from repo root:
```bash
dotnet build AutoFeed.Core/AutoFeed.Core.csproj
```
Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add AutoFeed.Core/
git commit -m "feat: add AutoFeed.Core project with FeedingLogic stubs"
```

---

### Task 2: Create AutoFeed.Tests with all 11 failing tests

**Files:**
- Create: `AutoFeed.Tests/AutoFeed.Tests.csproj`
- Create: `AutoFeed.Tests/FeedingLogicTests.cs`

- [ ] **Step 1: Create the test project file**

Create `AutoFeed.Tests/AutoFeed.Tests.csproj`:

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

- [ ] **Step 2: Create FeedingLogicTests.cs**

Create `AutoFeed.Tests/FeedingLogicTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using AutoFeed.Core;
using Xunit;

namespace AutoFeed.Tests;

public class FeedingLogicTests
{
    // --- FindConsumableInInventory ---

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
            Array.Empty<string>(),
            new HashSet<string> { "Carrot" }
        );
        Assert.Null(result);
    }

    // --- ShouldFeed ---

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

    // --- IsEligibleContainer ---

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

- [ ] **Step 3: Run tests — verify all 11 fail with NotImplementedException**

```bash
dotnet test AutoFeed.Tests/AutoFeed.Tests.csproj --verbosity normal
```
Expected: 11 tests fail with `System.NotImplementedException`.

- [ ] **Step 4: Commit**

```bash
git add AutoFeed.Tests/
git commit -m "test: add 11 failing FeedingLogic tests"
```

---

### Task 3: Implement FeedingLogic — make all tests pass

**Files:**
- Modify: `AutoFeed.Core/FeedingLogic.cs`

- [ ] **Step 1: Replace stubs with full implementation**

Overwrite `AutoFeed.Core/FeedingLogic.cs`:

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

- [ ] **Step 2: Run tests — verify all 11 pass**

```bash
dotnet test AutoFeed.Tests/AutoFeed.Tests.csproj --verbosity normal
```
Expected: `11 passed, 0 failed`.

- [ ] **Step 3: Commit**

```bash
git add AutoFeed.Core/FeedingLogic.cs
git commit -m "feat: implement FeedingLogic — all 11 tests passing"
```

---

### Task 4: Wire plugin to AutoFeed.Core

**Files:**
- Modify: `AutoFeed.csproj`
- Modify: `GlobalUsing.cs`

- [ ] **Step 1: Add ProjectReference to AutoFeed.csproj**

In `AutoFeed.csproj`, add inside the existing `<ItemGroup>` that has the PackageReferences (or add a new `<ItemGroup>`):

```xml
<ItemGroup>
  <ProjectReference Include="AutoFeed.Core\AutoFeed.Core.csproj" />
</ItemGroup>
```

- [ ] **Step 2: Add global using to GlobalUsing.cs**

Add one line to `GlobalUsing.cs` (keep alphabetical order within the BepInEx group):

```csharp
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Reflection;
global using AutoFeed.Core;
global using BepInEx;
global using BepInEx.Configuration;
global using BepInEx.Logging;
global using HarmonyLib;
global using UnityEngine;
```

- [ ] **Step 3: Commit**

```bash
git add AutoFeed.csproj GlobalUsing.cs
git commit -m "chore: wire plugin project to AutoFeed.Core"
```

---

### Task 5: Update the three adapter methods

**Files:**
- Modify: `Extensions/MonsterAIExtensions.cs`
- Modify: `Extensions/ColliderExtensions.cs`
- Modify: `Extensions/ContainerExtensions.cs`

These three changes delegate game-type logic to `FeedingLogic` and are committed together since they form one logical change.

- [ ] **Step 1: Update MonsterAIExtensions.cs**

In `FeedMonsterWithThrottling`, replace the `FeedIntervalPassed(animalId)` call:

```csharp
// Before:
if (FeedIntervalPassed(animalId))

// After:
if (FeedingLogic.ShouldFeed(animalId, Time.time, Plugin.LastFeedTimes, PluginSettings.FeedInterval))
```

Then delete the entire `FeedIntervalPassed` method (lines 57-62):

```csharp
// DELETE this method entirely:
private static bool FeedIntervalPassed(int animalId)
{
    if (!Plugin.LastFeedTimes.TryGetValue(animalId, out float lastTime))
        return true;
    return Time.time - lastTime >= PluginSettings.FeedInterval;
}
```

- [ ] **Step 2: Update ColliderExtensions.cs**

Replace the body of `IsNonEmptyChest`:

```csharp
// Before:
private static bool IsNonEmptyChest(Container container)
{
    var prefix = Plugin.ChestPrefix.Value;
    if (!string.IsNullOrEmpty(prefix) && !container.name.StartsWith(prefix))
        return false;

    var inventory = container.GetInventory();
    return inventory is not null && inventory.NrOfItems() > 0;
}

// After:
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

Note: `inventory?.NrOfItems() ?? 0` preserves the existing null-safety guard on `GetInventory()`.

- [ ] **Step 3: Update ContainerExtensions.cs**

Replace the body of `TryFindMatchingItem` (lines 47-64). The method signature is unchanged — only the body changes:

```csharp
// Before:
private static bool TryFindMatchingItem(
    List<ItemDrop.ItemData> items,
    Dictionary<string, List<ItemDrop.ItemData>> itemDropDict,
    out ItemDrop.ItemData? targetItem
)
{
    foreach (var item in items)
    {
        if (itemDropDict.TryGetValue(item.m_shared.m_name, out var matchingItems))
        {
            targetItem = matchingItems.First();
            return true;
        }
    }

    targetItem = null;
    return false;
}

// After:
private static bool TryFindMatchingItem(
    List<ItemDrop.ItemData> items,
    Dictionary<string, List<ItemDrop.ItemData>> itemDropDict,
    out ItemDrop.ItemData? targetItem
)
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

- [ ] **Step 4: Run tests — verify still 11 passing**

```bash
dotnet test AutoFeed.Tests/AutoFeed.Tests.csproj --verbosity normal
```
Expected: `11 passed, 0 failed`.

- [ ] **Step 5: Commit all three adapter changes**

```bash
git add Extensions/MonsterAIExtensions.cs Extensions/ColliderExtensions.cs Extensions/ContainerExtensions.cs
git commit -m "refactor: delegate to FeedingLogic in all three adapter methods"
```

---

### Task 6: Add projects to solution file

**Files:**
- Modify: `AutoFeed.sln`

- [ ] **Step 1: Add both projects to the solution**

Run from repo root:

```bash
dotnet sln AutoFeed.sln add AutoFeed.Core/AutoFeed.Core.csproj
dotnet sln AutoFeed.sln add AutoFeed.Tests/AutoFeed.Tests.csproj
```

- [ ] **Step 2: Verify solution builds**

```bash
dotnet build AutoFeed.sln
```
Expected: All three projects build successfully. (The plugin project requires NuGet packages to be restored — `dotnet restore` runs automatically as part of build.)

- [ ] **Step 3: Commit**

```bash
git add AutoFeed.sln
git commit -m "chore: add AutoFeed.Core and AutoFeed.Tests to solution"
```

---

### Task 7: Add GitHub Actions CI workflow

**Files:**
- Create: `.github/workflows/test.yml`

- [ ] **Step 1: Create the workflow file**

Create `.github/workflows/test.yml`:

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

- [ ] **Step 2: Commit**

```bash
git add .github/workflows/test.yml
git commit -m "ci: add GitHub Actions workflow to run tests on push and PR"
```

---

### Task 8: Push and configure branch protection

- [ ] **Step 1: Push main to origin**

```bash
git push origin main
```

Wait for the Actions run to complete at `https://github.com/<owner>/autofeed/actions`. Confirm the `test` job passes (green).

- [ ] **Step 2: Enable branch protection on GitHub (manual)**

Go to the repository on GitHub:

1. **Settings → Branches → Add branch protection rule**
2. Branch name pattern: `main`
3. Check **Require a pull request before merging**
4. Check **Require status checks to pass before merging**
5. In the status check search box, type `test` and select the job named `test` from the `Tests` workflow
6. Check **Require branches to be up to date before merging**
7. Click **Save changes**

After this, no commit can land on `main` without the `test` job passing on that branch.
