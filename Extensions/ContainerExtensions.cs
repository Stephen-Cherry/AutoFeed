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
