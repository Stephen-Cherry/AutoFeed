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

    /// <summary>
    /// Returns true if the cached value is still within the TTL window.
    /// </summary>
    public static bool IsCacheValid(float cachedTimestamp, float currentTime, float ttl) =>
        currentTime - cachedTimestamp < ttl;
}
