namespace AutoFeed;

public static class ContainerExtensions
{
    public static bool ContainersContainItemFromList(
        this List<Container> containers,
        List<ItemDrop> itemDrops,
        out Container? targetContainer,
        out ItemDrop.ItemData? targetItem
    ) =>
        FindItemInContainers(
            containers,
            CreateItemDictionary(itemDrops),
            out targetContainer,
            out targetItem
        );

    private static Dictionary<string, List<ItemDrop.ItemData>> CreateItemDictionary(
        List<ItemDrop> itemDrops
    ) =>
        itemDrops
            .GroupBy(i => i.m_itemData.m_shared.m_name)
            .ToDictionary(g => g.Key, g => g.Select(i => i.m_itemData).ToList());

    private static bool FindItemInContainers(
        List<Container> containers,
        Dictionary<string, List<ItemDrop.ItemData>> itemDropDict,
        out Container? targetContainer,
        out ItemDrop.ItemData? targetItem
    )
    {
        foreach (var container in containers)
        {
            var items = container.GetInventory().GetAllItems();
            if (TryFindMatchingItem(items, itemDropDict, out targetItem))
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
}
