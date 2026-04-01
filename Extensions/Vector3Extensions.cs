namespace AutoFeed;

public static class Vector3Extensions
{
    public static List<Container> GetContainersInRange(this Vector3 center, float radiusRange)
    {
        var result = new List<Container>();
        foreach (var piece in Piece.s_allPieces)
        {
            if (piece == null) continue;
            var container = piece.GetComponent<Container>();
            if (container == null) continue;
            var dist = Vector3.Distance(piece.transform.position, center);
            if (dist <= radiusRange && IsValidZNetView(container.GetComponent<ZNetView>()) && IsEligibleContainer(container))
                result.Add(container);
        }
        result.Sort((a, b) => Vector3.Distance(a.transform.position, center).CompareTo(Vector3.Distance(b.transform.position, center)));
        return result;
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
