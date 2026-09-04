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

    public async Task<byte[]?> TryGetAsync(int z, int x, int y, CancellationToken cancellationToken)
    {
        var path = GetPath(z, x, y);
        if (!File.Exists(path))
        {
            logger.LogInformation("Cache MISS: {Z}/{X}/{Y}", z, x, y);
            return null;
        }

        logger.LogInformation("Cache HIT: {Z}/{X}/{Y}", z, x, y);
        return await File.ReadAllBytesAsync(path, cancellationToken);
    }

    public async Task SaveAsync(int z, int x, int y, byte[] png, CancellationToken cancellationToken)
    {
        var path = GetPath(z, x, y);
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = path + ".tmp";

        await File.WriteAllBytesAsync(temporaryPath, png, cancellationToken);
        File.Move(temporaryPath, path, overwrite: true);
        logger.LogInformation("Saved: {Z}/{X}/{Y}", z, x, y);
    }

    private string GetPath(int z, int x, int y) => Path.Combine(
        options.RootPath,
        options.StyleVersion,
        z.ToString(System.Globalization.CultureInfo.InvariantCulture),
        x.ToString(System.Globalization.CultureInfo.InvariantCulture),
        $"{y}.png");
}
