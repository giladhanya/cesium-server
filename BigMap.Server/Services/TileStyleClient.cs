using BigMap.Server.Options;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BigMap.Server.Services;

public sealed class TileStyleClient
{
    private readonly HttpClient httpClient;
    private readonly ILogger<TileStyleClient> logger;
    private readonly SemaphoreSlim cacheLock = new(1, 1);
    private IReadOnlyDictionary<string, double>? minZoomByLayer;
    private IReadOnlyList<string>? layerNames;
    private StyleReference[]? styles;

    public TileStyleClient(
        HttpClient httpClient,
        IOptions<TileRendererOptions> options,
        ILogger<TileStyleClient> logger)
    {
        this.httpClient = httpClient;
        this.httpClient.BaseAddress = new Uri(options.Value.BaseUrl.TrimEnd('/') + "/");
        this.httpClient.Timeout = TimeSpan.FromSeconds(options.Value.TimeoutSeconds);
        this.logger = logger;
    }

    public async Task<IReadOnlyDictionary<string, double>> GetMinZoomByLayerAsync(CancellationToken cancellationToken)
    {
        if (minZoomByLayer is not null)
        {
            return minZoomByLayer;
        }

        await cacheLock.WaitAsync(cancellationToken);
        try
        {
            if (minZoomByLayer is not null)
            {
                return minZoomByLayer;
            }

            var styles = await GetStylesAsync(cancellationToken);
            var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var style in styles)
            {
                if (string.IsNullOrWhiteSpace(style.Id) || string.IsNullOrWhiteSpace(style.Url))
                {
                    continue;
                }

                var definition = await httpClient.GetFromJsonAsync<StyleDefinition>(style.Url, cancellationToken);
                var matchingLayers = definition?.Layers?.Where(layer =>
                    string.Equals(layer.Id, style.Id, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(layer.SourceLayer, style.Id, StringComparison.OrdinalIgnoreCase));
                if (matchingLayers?.Any() == true)
                {
                    result[style.Id] = matchingLayers.Min(layer => layer.MinZoom ?? 0);
                }
            }

            minZoomByLayer = result;
            return result;
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Could not load tile layer styles.");
            return new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            cacheLock.Release();
        }
    }

    public async Task<IReadOnlyList<string>> GetLayerNamesAsync(CancellationToken cancellationToken)
    {
        if (layerNames is not null)
        {
            return layerNames;
        }

        await cacheLock.WaitAsync(cancellationToken);
        try
        {
            if (layerNames is not null)
            {
                return layerNames;
            }

            var styles = await GetStylesAsync(cancellationToken);
            layerNames = styles
                .Where(style => !string.IsNullOrWhiteSpace(style.Id))
                .Select(style => style.Id!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return layerNames;
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Could not load tile layer names.");
            return [];
        }
        finally
        {
            cacheLock.Release();
        }
    }

    private async Task<StyleReference[]> GetStylesAsync(CancellationToken cancellationToken)
    {
        if (styles is not null)
        {
            return styles;
        }

        styles = await httpClient.GetFromJsonAsync<StyleReference[]>("styles.json", cancellationToken) ?? [];
        return styles;
    }

    private sealed record StyleReference(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("url")] string? Url);

    private sealed record StyleDefinition(
        [property: JsonPropertyName("layers")] StyleLayer[]? Layers);

    private sealed record StyleLayer(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("source-layer")] string? SourceLayer,
        [property: JsonPropertyName("minzoom")] double? MinZoom);
}