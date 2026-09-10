using BigMap.Server.Options;
using Microsoft.Extensions.Options;

namespace BigMap.Server.Services;

public sealed class TileCacheService
{
    private readonly TileCacheOptions options;
    private readonly ILogger<TileCacheService> logger;

    public TileCacheService(IOptions<TileCacheOptions> options, ILogger<TileCacheService> logger)
    {
        this.options = options.Value;
        this.logger = logger;
    }

    public Task<byte[]?> TryGetPngAsync(string layer, int z, int x, int y, CancellationToken cancellationToken) =>
        TryGetFileAsync(GetRenderedPath(layer, z, x, y, ".png"), layer, z, x, y, cancellationToken);

    public Task<byte[]?> TryGetBasePngAsync(int z, int x, int y, CancellationToken cancellationToken) =>
        TryGetFileAsync(GetBasePath(z, x, y, ".png"), "base", z, x, y, cancellationToken);

    private async Task<byte[]?> TryGetFileAsync(string path, string layer, int z, int x, int y, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            logger.LogInformation("Cache MISS: {Layer}/{Z}/{X}/{Y}", layer, z, x, y);
            return null;
        }

        logger.LogInformation("Cache HIT: {Layer}/{Z}/{X}/{Y}", layer, z, x, y);
        return await File.ReadAllBytesAsync(path, cancellationToken);
    }

    public Task SavePngAsync(string layer, int z, int x, int y, byte[] content, CancellationToken cancellationToken) =>
        SaveFileAsync(GetRenderedPath(layer, z, x, y, ".png"), layer, z, x, y, content, cancellationToken);

    public Task SaveBasePngAsync(int z, int x, int y, byte[] content, CancellationToken cancellationToken) =>
        SaveFileAsync(GetBasePath(z, x, y, ".png"), "base", z, x, y, content, cancellationToken);

    private async Task SaveFileAsync(string path, string layer, int z, int x, int y, byte[] content, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path)!;
        var dir = Directory.CreateDirectory(directory);
        logger.LogInformation("Saving: {Layer}/{Z}/{X}/{Y} to {Directory}", layer, z, x, y, dir.FullName);
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";

        await File.WriteAllBytesAsync(temporaryPath, content, cancellationToken);
        File.Move(temporaryPath, path, overwrite: true);
        logger.LogInformation("Saved: {Layer}/{Z}/{X}/{Y}", layer, z, x, y);
    }

    private string GetRenderedPath(string layer, int z, int x, int y, string extension) => Path.Combine(
        options.RootPath,
        options.StyleVersion,
        layer,
        z.ToString(System.Globalization.CultureInfo.InvariantCulture),
        $"{x.ToString(System.Globalization.CultureInfo.InvariantCulture)}_{y.ToString(System.Globalization.CultureInfo.InvariantCulture)}{extension}");

    private string GetBasePath(int z, int x, int y, string extension) => Path.Combine(
        options.RootPath,
        options.StyleVersion,
        "base",
        z.ToString(System.Globalization.CultureInfo.InvariantCulture),
        $"{x.ToString(System.Globalization.CultureInfo.InvariantCulture)}_{y.ToString(System.Globalization.CultureInfo.InvariantCulture)}{extension}");

}
