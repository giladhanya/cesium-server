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
    private readonly TilePbfClient pbfClient;
    private readonly VectorTileRenderer vectorRenderer;
    private readonly TileCacheOptions cacheOptions;
    private readonly ILogger<TileService> logger;
    private readonly ConcurrentDictionary<string, Lazy<Task<TileResult>>> inFlightTiles = new();

    public TileService(
        TileCacheService cache,
        TileRendererClient renderer,
        TilePbfClient pbfClient,
        VectorTileRenderer vectorRenderer,
        IOptions<TileCacheOptions> cacheOptions,
        ILogger<TileService> logger)
    {
        this.cache = cache;
        this.renderer = renderer;
        this.pbfClient = pbfClient;
        this.vectorRenderer = vectorRenderer;
        this.cacheOptions = cacheOptions.Value;
        this.logger = logger;
    }

    public async Task<TileResult> GetTileAsync(TileLayer layer, int z, int x, int y, CancellationToken cancellationToken)
    {
        var cachedPng = await cache.TryGetPngAsync(layer, z, x, y, cancellationToken);
        if (cachedPng is not null)
        {
            return new(TileResultStatus.Success, cachedPng);
        }

        var key = $"{cacheOptions.StyleVersion}/{layer.ToRouteName()}/{z}/{x}/{y}";
        var inFlight = inFlightTiles.GetOrAdd(
            key,
            _ => new Lazy<Task<TileResult>>(
                () => RenderAndCacheAsync(layer, z, x, y, cancellationToken),
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

    private async Task<TileResult> RenderAndCacheAsync(TileLayer layer, int z, int x, int y, CancellationToken cancellationToken)
    {
        var cachedPng = await cache.TryGetPngAsync(layer, z, x, y, cancellationToken);
        if (cachedPng is not null)
        {
            return new(TileResultStatus.Success, cachedPng);
        }

        logger.LogInformation("Rendering: {Layer}/{Z}/{X}/{Y}", layer, z, x, y);
        var pbf = await cache.TryGetPbfAsync(z, x, y, cancellationToken);
        if (pbf is null)
        {
            var downloaded = await pbfClient.GetAsync(z, x, y, cancellationToken);
            if (downloaded.Status == TileRendererStatus.NotFound)
            {
                return new(TileResultStatus.NotFound);
            }

            if (downloaded.Status != TileRendererStatus.Success || downloaded.Pbf is null)
            {
                return new(TileResultStatus.RendererFailure);
            }

            pbf = downloaded.Pbf;
            await cache.SavePbfAsync(z, x, y, pbf, cancellationToken);
        }

        try
        {
            var vector = vectorRenderer.Render(pbf, layer);
            await cache.SavePngAsync(layer, z, x, y, vector, cancellationToken);
            return new(TileResultStatus.Success, vector);
        }
        catch (InvalidDataException exception)
        {
            logger.LogWarning(exception, "Invalid vector tile for {Layer}/{Z}/{X}/{Y}", layer, z, x, y);
            return new(TileResultStatus.RendererFailure);
        }
    }

    public async Task<TileResult> GetBaseTileAsync(int z, int x, int y, CancellationToken cancellationToken)
    {
        var cached = await cache.TryGetBasePngAsync(z, x, y, cancellationToken);
        if (cached is not null)
        {
            return new(TileResultStatus.Success, cached);
        }

        var key = $"{cacheOptions.StyleVersion}/base/{z}/{x}/{y}";
        var inFlight = inFlightTiles.GetOrAdd(key, _ => new Lazy<Task<TileResult>>(
            () => RenderAndCacheBaseAsync(z, x, y, cancellationToken),
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

    private async Task<TileResult> RenderAndCacheBaseAsync(int z, int x, int y, CancellationToken cancellationToken)
    {
        var rendered = await renderer.RenderBaseAsync(z, x, y, cancellationToken);
        if (rendered.Status == TileRendererStatus.NotFound)
        {
            return new(TileResultStatus.NotFound);
        }

        if (rendered.Status != TileRendererStatus.Success || rendered.Png is null)
        {
            return new(TileResultStatus.RendererFailure);
        }

        await cache.SaveBasePngAsync(z, x, y, rendered.Png, cancellationToken);
        return new(TileResultStatus.Success, rendered.Png);
    }
}
