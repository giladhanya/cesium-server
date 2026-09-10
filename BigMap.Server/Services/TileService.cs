using BigMap.Server.Options;
using Microsoft.Extensions.Options;
using SkiaSharp;
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
    private static readonly Lazy<byte[]> transparentTile = new(CreateTransparentTile);
    private readonly TileCacheService cache;
    private readonly TileRendererClient renderer;
    private readonly TileStyleClient styleClient;
    private readonly TileCacheOptions cacheOptions;
    private readonly ILogger<TileService> logger;
    private readonly ConcurrentDictionary<string, Lazy<Task<TileResult>>> inFlightTiles = new();

    public TileService(
        TileCacheService cache,
        TileRendererClient renderer,
        TileStyleClient styleClient,
        IOptions<TileCacheOptions> cacheOptions,
        ILogger<TileService> logger)
    {
        this.cache = cache;
        this.renderer = renderer;
        this.styleClient = styleClient;
        this.cacheOptions = cacheOptions.Value;
        this.logger = logger;
    }

    public async Task<TileResult> GetTileAsync(string layer, int z, int x, int y, CancellationToken cancellationToken)
    {
        var minZoomByLayer = await styleClient.GetMinZoomByLayerAsync(cancellationToken);
        if (minZoomByLayer.TryGetValue(layer, out var minZoom) && z < minZoom)
        {
            return new(TileResultStatus.Success, transparentTile.Value);
        }

        var cachedPng = await cache.TryGetPngAsync(layer, z, x, y, cancellationToken);
        if (cachedPng is not null)
        {
            return new(TileResultStatus.Success, cachedPng);
        }

        var key = $"{cacheOptions.StyleVersion}/{layer}/{z}/{x}/{y}";
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

    private async Task<TileResult> RenderAndCacheAsync(string layer, int z, int x, int y, CancellationToken cancellationToken)
    {
        var cachedPng = await cache.TryGetPngAsync(layer, z, x, y, cancellationToken);
        if (cachedPng is not null)
        {
            return new(TileResultStatus.Success, cachedPng);
        }

        logger.LogInformation("Rendering: {Layer}/{Z}/{X}/{Y}", layer, z, x, y);
        var rendered = await renderer.RenderLayerAsync(layer, z, x, y, cancellationToken);
        if (rendered.Status == TileRendererStatus.NotFound)
        {
            return new(TileResultStatus.NotFound);
        }

        if (rendered.Status != TileRendererStatus.Success || rendered.Png is null)
        {
            return new(TileResultStatus.RendererFailure);
        }

        await cache.SavePngAsync(layer, z, x, y, rendered.Png, cancellationToken);
        return new(TileResultStatus.Success, rendered.Png);
    }

    public async Task<IReadOnlyList<string>?> GetVectorLayerNamesAsync(CancellationToken cancellationToken)
    {
        return await styleClient.GetLayerNamesAsync(cancellationToken);
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

    private static byte[] CreateTransparentTile()
    {
        using var bitmap = new SKBitmap(256, 256);
        bitmap.Erase(SKColors.Transparent);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
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
