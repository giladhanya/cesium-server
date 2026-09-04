using BigMap.Server.Options;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace BigMap.Server.Services;

public enum TileResultStatus
{
    Success,
    NotFound,
    RendererFailure
}

public sealed record TileResult(TileResultStatus Status, byte[]? Png = null);

public sealed class TileService
{
    private readonly TileCacheService cache;
    private readonly TileRendererClient renderer;
    private readonly TileCacheOptions cacheOptions;
    private readonly ILogger<TileService> logger;
    private readonly ConcurrentDictionary<string, Lazy<Task<TileResult>>> inFlightTiles = new();

    public TileService(
        TileCacheService cache,
        TileRendererClient renderer,
        IOptions<TileCacheOptions> cacheOptions,
        ILogger<TileService> logger)
    {
        this.cache = cache;
        this.renderer = renderer;
        this.cacheOptions = cacheOptions.Value;
        this.logger = logger;
    }

    public async Task<TileResult> GetTileAsync(int z, int x, int y, CancellationToken cancellationToken)
    {
        var cached = await cache.TryGetAsync(z, x, y, cancellationToken);
        if (cached is not null)
        {
            return new(TileResultStatus.Success, cached);
        }

        var key = $"{cacheOptions.StyleVersion}/{z}/{x}/{y}";
        var inFlight = inFlightTiles.GetOrAdd(
            key,
            _ => new Lazy<Task<TileResult>>(
                () => RenderAndCacheAsync(z, x, y, cancellationToken),
                LazyThreadSafetyMode.ExecutionAndPublication));
        try
        {
            return await inFlight.Value;
        }
        finally
        {
            inFlightTiles.TryRemove(new KeyValuePair<string, Lazy<Task<TileResult>>>(key, inFlight));
        }
    }

    private async Task<TileResult> RenderAndCacheAsync(int z, int x, int y, CancellationToken cancellationToken)
    {
        var cached = await cache.TryGetAsync(z, x, y, cancellationToken);
        if (cached is not null)
        {
            return new(TileResultStatus.Success, cached);
        }

        logger.LogInformation("Rendering: {Z}/{X}/{Y}", z, x, y);
        var rendered = await renderer.RenderAsync(z, x, y, cancellationToken);
        if (rendered.Status == TileRendererStatus.NotFound)
        {
            return new(TileResultStatus.NotFound);
        }

        if (rendered.Status != TileRendererStatus.Success || rendered.Png is null)
        {
            return new(TileResultStatus.RendererFailure);
        }

        logger.LogInformation("Rendered: {Z}/{X}/{Y}", z, x, y);
        await cache.SaveAsync(z, x, y, rendered.Png, cancellationToken);
        return new(TileResultStatus.Success, rendered.Png);
    }
}
