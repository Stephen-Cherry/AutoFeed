namespace AutoFeed;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
[BepInDependency(Jotunn.Main.ModGuid)]
[Jotunn.Utils.NetworkCompatibility(
    Jotunn.Utils.CompatibilityLevel.EveryoneMustHaveMod,
    Jotunn.Utils.VersionStrictness.Minor
)]
public class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log = null!;
    private static Harmony? _harmony;

    public static readonly Dictionary<int, float> LastFeedTimes = new();
    internal static readonly Dictionary<int, (float timestamp, List<Container> containers)> ContainerCache = new();

    public static ConfigEntry<float> ContainerRange = default!;
    public static ConfigEntry<bool> ModEnabled = default!;
    public static ConfigEntry<string> ChestPrefix = default!;
    public static ConfigEntry<float> CacheTtl = default!;

    private void Awake()
    {
        Log = Logger;
        Log.LogInfo($"AutoFeed v{PluginInfo.PLUGIN_VERSION} loading...");

        ContainerRange = Config.Bind(
            "General",
            "Container Range",
            10f,
            "The radius in which a tamed creature will look for containers to feed from."
        );
        ModEnabled = Config.Bind("General", "Enabled", true, "Whether the mod is enabled.");
        ChestPrefix = Config.Bind(
            "General",
            "Chest Prefix",
            "piece_chest",
            "Name prefix for containers eligible for auto-feeding. Leave empty to allow all containers."
        );
        CacheTtl = Config.Bind(
            "General",
            "Container Cache TTL",
            5f,
            "Seconds before the nearby-container list is refreshed for each animal."
        );

        _harmony = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
        Log.LogInfo("AutoFeed loaded.");
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
                Player.m_localPlayer is null
                || !ModEnabled()
                || !HasCharacterData()
                || !IsTamedAndHungry()
                || !HasValidFoodTypes()
                || HasFoundFood()
            )
                return;

            var animalId = ___m_character.GetInstanceID();
            if (!FeedingLogic.ShouldFeed(animalId, Time.time, Plugin.LastFeedTimes, PluginSettings.FeedInterval))
                return;

            var consumableMap = ___m_consumeItems
                .ToDictionary(i => i.m_itemData.m_shared.m_name, i => i.m_itemData);

            var nearbyContainers =
                ___m_character.gameObject.transform.position.GetContainersInRange(
                    ContainerRange.Value, animalId, Time.time
                );

            var foundContainerWithFood = nearbyContainers.ContainersContainItemFromList(
                consumableMap,
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
