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
    public void ShouldFeed_ReturnsTrue_WhenExactlyAtIntervalBoundary()
    {
        var result = FeedingLogic.ShouldFeed(
            animalId: 1,
            currentTime: 1.0f,
            lastFeedTimes: new Dictionary<int, float> { [1] = 0.5f },
            interval: 0.5f
        );
        Assert.True(result);
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
}
