namespace AutoFeed;

public static class ColliderExtensions
{
    public static List<Container> GetContainersOrderedByDistanceFromCenter(
        this Collider[] colliders,
        Vector3 center
    )
    {
        var containers = colliders
            .Select(collider => collider.GetComponentInParent<Container>())
            .Where(container =>
                container is not null
                && IsValidZNetView(container.GetComponent<ZNetView>())
                && IsNonEmptyChest(container)
            )
            .OrderBy(container => Vector3.Distance(container.transform.position, center));
        return [.. containers];
    }

    private static bool IsValidZNetView(ZNetView? zNetView) =>
        zNetView is not null && zNetView.IsValid();

    private static bool IsNonEmptyChest(Container container) =>
        container.name.StartsWith(PluginSettings.ChestPrefix)
        && container.GetInventory() is not null;
}
