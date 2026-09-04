namespace BigMap.Server.Services;

public static class TileCoordinateValidator
{
    public static bool IsValid(int z, int x, int y, int maxZoom)
    {
        if (z < 0 || z > maxZoom || x < 0 || y < 0)
        {
            return false;
        }

        var tileCount = 1L << z;
        return x < tileCount && y < tileCount;
    }
}