namespace AutoFeed;

public static class MonsterAIExtensions
{
    public static void FeedMonsterWithThrottling(
        this MonsterAI __instance,
        Tameable ___m_tamable,
        Character ___m_character,
        Container container,
        ItemDrop.ItemData item
    )
    {
        if (FeedIntervalPassed())
        {
            FeedAnimal(__instance, ___m_tamable, ___m_character, container, item);
            Plugin.LastFeedTime = Time.time;
        }
    }

    public static void FeedAnimal(
        this MonsterAI monsterAI,
        Tameable tamable,
        Character character,
        Container container,
        ItemDrop.ItemData item
    )
    {
        if (tamable is null || monsterAI is null || !tamable.IsHungry())
            return;

        monsterAI.ConsumeItem(character);

        container.GetInventory().RemoveItem(item.m_shared.m_name, 1);
        typeof(Inventory)
            .GetMethod("Changed", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(container.GetInventory(), []);
        typeof(Container)
            .GetMethod("Save", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(container, []);
    }

    public static void ConsumeItem(this MonsterAI monsterAI, Character character)
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

    private static bool FeedIntervalPassed() =>
        Time.time - Plugin.LastFeedTime >= PluginSettings.FeedInterval;
}
