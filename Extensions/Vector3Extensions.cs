namespace AutoFeed;

public static class Vector3Extensions
{
    public static List<Container> GetContainersInRange(this Vector3 center, float radiusRange)
    {
        try
        {
            Collider[] collidersInRange = Physics.OverlapSphere(
                center,
                Mathf.Max(radiusRange, 0),
                LayerMask.GetMask(PluginSettings.ChestLayer)
            );

            return collidersInRange.GetContainersOrderedByDistanceFromCenter(center);
        }
        catch
        {
            return [];
        }
    }
}
