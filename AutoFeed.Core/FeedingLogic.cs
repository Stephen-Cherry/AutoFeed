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
