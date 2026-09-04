namespace BigMap.Server.Options;

public sealed class TileCacheOptions
{
    public const string SectionName = "TileCache";

    public string RootPath { get; set; } = "cache";
    public string StyleVersion { get; set; } = "bigmap-v1";
    public int MaxZoom { get; set; } = 14;
}
