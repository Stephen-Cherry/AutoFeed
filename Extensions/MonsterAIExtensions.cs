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
        var animalId = ___m_character.GetInstanceID();
        if (FeedingLogic.ShouldFeed(animalId, Time.time, Plugin.LastFeedTimes, PluginSettings.FeedInterval))
        {
            FeedAnimal(__instance, ___m_tamable, ___m_character, container, item);
            Plugin.LastFeedTimes[animalId] = Time.time;
        }
    }

    private static void FeedAnimal(
        MonsterAI monsterAI,
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
        Traverse.Create(container.GetInventory()).Method("Changed").GetValue();
        Traverse.Create(container).Method("Save").GetValue();
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

        var animator = Traverse
            .Create(monsterAI)
            .Field("m_animator")
            .GetValue<ZSyncAnimation>();
        animator?.SetTrigger("consume");
    }

}
