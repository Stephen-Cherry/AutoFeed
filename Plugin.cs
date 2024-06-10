namespace AutoFeed;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
[BepInDependency(Jotunn.Main.ModGuid)]
[Jotunn.Utils.NetworkCompatibility(
    Jotunn.Utils.CompatibilityLevel.EveryoneMustHaveMod,
    Jotunn.Utils.VersionStrictness.Minor
)]
public class Plugin : BaseUnityPlugin
{
    public static float LastFeedTime = 0f;
    public static ConfigEntry<float> ContainerRange = default!;
    public static ConfigEntry<bool> ModEnabled = default!;

    private void Awake()
    {
        ContainerRange = Config.Bind(
            "General",
            "Container Range",
            10f,
            "The radiusRange in which the tames monster will look for containers to feed from."
        );
        ModEnabled = Config.Bind("General", "Enabled", true, "Whether the mod is enabled.");

        if (ModEnabled.Value)
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
    }

    [HarmonyPatch(typeof(MonsterAI), "UpdateConsumeItem")]
    static class UpdateConsumeItemPatch
    {
        static void Postfix(
            MonsterAI __instance,
            Character ___m_character,
            Tameable ___m_tamable,
            List<ItemDrop> ___m_consumeItems,
            bool __result
        )
        {
            bool ModEnabled() => Plugin.ModEnabled.Value;
            bool HasFoundFood() => __result;
            bool HasCharacterData() => ___m_character is not null;
            bool HasValidFoodTypes() =>
                ___m_consumeItems is not null && ___m_consumeItems.Count > 0;
            bool IsTamedAndHungry() => ___m_tamable is not null && ___m_tamable.IsHungry();

            if (
                !ModEnabled()
                || !HasCharacterData()
                || !IsTamedAndHungry()
                || !HasValidFoodTypes()
                || HasFoundFood()
            )
                return;

            var nearbyContainers =
                ___m_character.gameObject.transform.position.GetContainersInRange(
                    ContainerRange.Value
                );

            var foundContainerWithFood = nearbyContainers.ContainersContainItemFromList(
                ___m_consumeItems,
                out var container,
                out var item
            );

            if (foundContainerWithFood)
            {
                __instance.FeedMonsterWithThrottling(
                    ___m_tamable,
                    ___m_character,
                    container!,
                    item!
                );
            }
        }
    }
}
