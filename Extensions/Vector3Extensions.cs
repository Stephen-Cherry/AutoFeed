namespace AutoFeed;

public static class Vector3Extensions
{
    public static List<Container> GetContainersInRange(this Vector3 center, float radiusRange)
    {
        var sqrRadius = radiusRange * radiusRange;
        var result = new List<Container>();
        foreach (var piece in Piece.s_allPieces)
        {
            if (piece == null) continue;
            var container = piece.GetComponent<Container>();
            if (container == null) continue;
            var sqrDist = (piece.transform.position - center).sqrMagnitude;
            if (sqrDist <= sqrRadius && IsValidZNetView(container.GetComponent<ZNetView>()) && IsEligibleContainer(container))
                result.Add(container);
        }

        var sorted = result
            .Select(c => (container: c, sqrDist: (c.transform.position - center).sqrMagnitude))
            .OrderBy(x => x.sqrDist)
            .Select(x => x.container)
            .ToList();

        return sorted;
    }

    private static bool IsValidZNetView(ZNetView? zNetView) =>
        zNetView is not null && zNetView.IsValid();

    private static bool IsEligibleContainer(Container container)
    {
        var inventory = container.GetInventory();
        return FeedingLogic.IsEligibleContainer(
            container.name,
            inventory?.NrOfItems() ?? 0,
            Plugin.ChestPrefix.Value
        );
    }
}
