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
    private static float lastFeedTime = 0f;
    private static readonly float feedInterval = 0.1f;

    private void Awake()
    {
        containerRange = Config.Bind(
            "General",
            "Container Range",
            10f,
            "The radiusRange in which the tames monster will look for containers to feed from."
        );
        modEnabled = Config.Bind("General", "Enabled", true, "Whether the mod is enabled.");

        if (modEnabled.Value)
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
    }

    [HarmonyPatch(typeof(MonsterAI), "UpdateConsumeItem")]
    static class UpdateConsumeItemPatch
    {
        static async void Postfix(
            MonsterAI __instance,
            ZNetView ___m_nview,
            Character ___m_character,
            Tameable ___m_tamable,
            List<ItemDrop> ___m_consumeItems,
            bool __result
        )
        {
            bool ModEnabled() => modEnabled.Value;
            bool HasFoundFood() => __result;
            bool HasCharacterData() => ___m_character is not null;
            bool HasValidFoodTypes() =>
                ___m_consumeItems is not null && ___m_consumeItems.Count > 0;
            bool IsTamedAndHungry() => ___m_tamable is not null && ___m_tamable.IsHungry();
            bool IsViewOwner() => ___m_nview is not null && ___m_nview.IsOwner();

            if (
                !ModEnabled()
                || !IsViewOwner()
                || !HasCharacterData()
                || !IsTamedAndHungry()
                || !HasValidFoodTypes()
                || HasFoundFood()
            )
                return;

            var nearbyContainers = GetContainersInRange(
                ___m_character.gameObject.transform.position,
                containerRange.Value
            );

            var foundContainerWithFood = ContainersContainItemFromList(
                nearbyContainers,
                ___m_consumeItems,
                out var container,
                out var item
            );

            if (foundContainerWithFood)
            {
                await FeedMonsterWithThrottling(
                    __instance,
                    ___m_tamable,
                    ___m_character,
                    container!,
                    item!
                );
            }
        }
    }

    private static List<Container> GetContainersInRange(Vector3 center, float radiusRange)
    {
        try
        {
            Collider[] collidersInRange = Physics.OverlapSphere(
                center,
                Mathf.Max(radiusRange, 0),
                LayerMask.GetMask("piece")
            );

            var containers = collidersInRange
                .Select(collider => collider.GetComponentInParent<Container>())
                .Where(container =>
                    container is not null
                    && IsValidZNetView(container.GetComponent<ZNetView>())
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

    private static bool IsValidZNetView(ZNetView? zNetView) =>
        zNetView is not null && zNetView.IsValid();

    private static bool IsNonEmptyChest(Container container) =>
        container.name.StartsWith("piece_chest") && container.GetInventory() is not null;

    private static async Task FeedMonsterWithThrottling(
        MonsterAI __instance,
        Tameable ___m_tamable,
        Character ___m_character,
        Container container,
        ItemDrop.ItemData item
    )
    {
        if (FeedIntervalPasssed())
        {
            await FeedAnimal(__instance, ___m_tamable, ___m_character, container, item, 0);
            lastFeedTime = Time.time;
        }
    }

    private static bool FeedIntervalPasssed() => Time.time - lastFeedTime >= feedInterval;

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
