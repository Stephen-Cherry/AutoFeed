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
        Plugin.LastFeedTimes[___m_character.GetInstanceID()] = Time.time;
        FeedAnimal(__instance, ___m_tamable, ___m_character, container, item);
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

        var containerNview = container.GetComponent<ZNetView>();
        if (containerNview is null || !containerNview.IsValid())
            return;

        monsterAI.ConsumeItem(character);

        container.GetInventory().RemoveItem(item.m_shared.m_name, 1);
        container.Save();
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

        monsterAI.m_animator?.SetTrigger("consume");
    }

}
