using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace AutoFeed;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    public static ConfigEntry<float> containerRange = default!;
    public static ConfigEntry<bool> modEnabled = default!;
    private static float lastFeed;
    private static int feedCount;

    private void Awake()
    {
        containerRange = Config.Bind(
            "General",
            "Container Range",
            5f,
            "The range in which the plugin will look for containers to feed from."
        );
        modEnabled = Config.Bind("General", "Enabled", true, "Enable or disable the plugin.");

        if (!modEnabled.Value)
            return;

        Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
    }

    [HarmonyPatch(typeof(MonsterAI), "UpdateConsumeItem")]
    static class UpdateConsumeItem_Patch
    {
        static void Postfix(
            MonsterAI __instance,
            ZNetView ___m_nview,
            Character ___m_character,
            Tameable ___m_tamable,
            List<ItemDrop> ___m_consumeItems,
            bool __result
        )
        {
            if (!modEnabled.Value)
                return;

            if (__result)
                return;

            if (___m_character is null)
                return;

            if (___m_nview is null || !___m_nview.IsOwner())
                return;

            if (___m_tamable is null || !___m_tamable.IsHungry())
                return;

            if (___m_consumeItems is null || ___m_consumeItems?.Count == 0)
                return;

            var nearbyContainers = GetNearbyContainers(
                ___m_character.gameObject.transform.position,
                containerRange.Value
            );

            if (
                ContainersContainItemFromList(
                    nearbyContainers,
                    ___m_consumeItems!,
                    out var container,
                    out var item
                )
            )
            {
                FeedMonsterWithThrottling(
                    __instance,
                    ___m_tamable,
                    ___m_character,
                    container!,
                    item!
                );
            }
        }
    }

    private static List<Container> GetNearbyContainers(Vector3 center, float range)
    {
        try
        {
            var colliders = Physics.OverlapSphere(
                center,
                Mathf.Max(range, 0),
                LayerMask.GetMask("piece")
            );
            var containers = colliders
                .Select(collider => collider.GetComponentInParent<Container>())
                .Where(container =>
                    container != null
                    && container.GetComponent<ZNetView>()?.IsValid() == true
                    && IsNonEmptyChest(container)
                )
                .OrderBy(container => Vector3.Distance(container.transform.position, center));
            return [.. containers];
        }
        catch
        {
            return [];
        }
    }

    private static bool IsNonEmptyChest(Container container) =>
        (container.name.StartsWith("piece_chest") || container.name.StartsWith("Container"))
        && container.GetInventory() != null;

    private static async void FeedMonsterWithThrottling(
        MonsterAI __instance,
        Tameable ___m_tamable,
        Character ___m_character,
        Container container,
        ItemDrop.ItemData item
    )
    {
        if (Time.time - lastFeed < 0.1)
        {
            feedCount++;
            await FeedAnimal(
                __instance,
                ___m_tamable,
                ___m_character,
                container,
                item,
                feedCount * 33
            );
        }
        else
        {
            feedCount = 0;
            lastFeed = Time.time;
            await FeedAnimal(__instance, ___m_tamable, ___m_character, container, item, 0);
        }
    }

    private static bool ContainersContainItemFromList(
        List<Container> containers,
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

    private static async Task FeedAnimal(
        MonsterAI monsterAI,
        Tameable tamable,
        Character character,
        Container container,
        ItemDrop.ItemData item,
        int delay
    )
    {
        await Task.Delay(delay);

        if (tamable is null || monsterAI is null || !tamable.IsHungry())
            return;

        ConsumeItem(monsterAI, character);

        container.GetInventory().RemoveItem(item.m_shared.m_name, 1);
        typeof(Inventory)
            .GetMethod("Changed", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(container.GetInventory(), []);
        typeof(Container)
            .GetMethod("Save", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(container, []);
    }

    private static void ConsumeItem(MonsterAI monsterAI, Character character)
    {
        monsterAI.m_onConsumedItem?.Invoke(null);

        (character as Humanoid)?.m_consumeItemEffects.Create(
            basePos: character.transform.position,
            baseRot: Quaternion.identity,
            scale: 1f,
            variant: -1
        );

        Traverse
            .Create(monsterAI)
            .Field("m_animator")
            .GetValue<ZSyncAnimation>()
            .SetTrigger("consume");
    }
}
