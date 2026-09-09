namespace BigMap.Server.Options;

public enum TileLayer
{
    Boundary,
    Landcover,
    Place,
    Water,
    WaterName
}

public static class TileLayerExtensions
{
    public static string ToRouteName(this TileLayer layer) => layer switch
    {
        TileLayer.WaterName => "water_name",
        _ => layer.ToString().ToLowerInvariant()
    };

    public static bool TryParse(string value, out TileLayer layer)
    {
        foreach (var candidate in Enum.GetValues<TileLayer>())
        {
            if (string.Equals(candidate.ToRouteName(), value, StringComparison.OrdinalIgnoreCase))
            {
                layer = candidate;
                return true;
            }
        }

        layer = default;
        return false;
    }
}