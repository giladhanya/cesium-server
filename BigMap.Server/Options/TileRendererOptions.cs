namespace BigMap.Server.Options;

public sealed class TileRendererOptions
{
    public const string SectionName = "TileRenderer";

    public string BaseUrl { get; set; } = "http://127.0.0.1:8081";
    public string BasePath { get; set; } = "styles/basic-preview/256";
    public string VectorPath { get; set; } = "data/v3";
    public int TimeoutSeconds { get; set; } = 30;
}
