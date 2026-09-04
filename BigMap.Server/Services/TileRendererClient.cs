using BigMap.Server.Options;
using Microsoft.Extensions.Options;

namespace BigMap.Server.Services;

public enum TileRendererStatus
{
    Success,
    NotFound,
    Failure
}

public sealed record TileRendererResult(TileRendererStatus Status, byte[]? Png = null);

public class TileRendererClient
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private readonly HttpClient httpClient;
    private readonly ILogger<TileRendererClient> logger;

    public TileRendererClient(HttpClient httpClient, IOptions<TileRendererOptions> options, ILogger<TileRendererClient> logger)
    {
        this.httpClient = httpClient;
        this.httpClient.BaseAddress = new Uri(options.Value.BaseUrl.TrimEnd('/') + "/");
        this.httpClient.Timeout = TimeSpan.FromSeconds(options.Value.TimeoutSeconds);
        this.logger = logger;
    }

    public virtual async Task<TileRendererResult> RenderAsync(int z, int x, int y, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.GetAsync($"render/{z}/{x}/{y}.png", cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return new(TileRendererStatus.NotFound);
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Renderer error for {Z}/{X}/{Y}: HTTP {StatusCode}", z, x, y, (int)response.StatusCode);
                return new(TileRendererStatus.Failure);
            }

            var png = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            if (png.Length == 0 || !png.AsSpan(0, Math.Min(png.Length, PngSignature.Length)).SequenceEqual(PngSignature))
            {
                logger.LogWarning("Renderer returned invalid PNG for {Z}/{X}/{Y}", z, x, y);
                return new(TileRendererStatus.Failure);
            }

            return new(TileRendererStatus.Success, png);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Renderer timeout for {Z}/{X}/{Y}", z, x, y);
            return new(TileRendererStatus.Failure);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Renderer unavailable for {Z}/{X}/{Y}", z, x, y);
            return new(TileRendererStatus.Failure);
        }
    }
}
