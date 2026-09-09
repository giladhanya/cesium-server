using BigMap.Server.Options;
using Microsoft.Extensions.Options;

namespace BigMap.Server.Services;

public class TilePbfClient
{
    private readonly HttpClient httpClient;
    private readonly TileRendererOptions options;
    private readonly ILogger<TilePbfClient> logger;

    public TilePbfClient(
        HttpClient httpClient,
        IOptions<TileRendererOptions> options,
        ILogger<TilePbfClient> logger)
    {
        this.httpClient = httpClient;
        this.options = options.Value;
        this.logger = logger;
        this.httpClient.BaseAddress = new Uri(this.options.BaseUrl.TrimEnd('/') + "/");
        this.httpClient.Timeout = TimeSpan.FromSeconds(this.options.TimeoutSeconds);
    }

    public virtual async Task<(TileRendererStatus Status, byte[]? Pbf)> GetAsync(
        int z,
        int x,
        int y,
        CancellationToken cancellationToken)
    {
        try
        {
            var path = $"{options.VectorPath.Trim('/')}/{z}/{x}/{y}.pbf";
            using var response = await httpClient.GetAsync(path, cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return (TileRendererStatus.NotFound, null);
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Vector renderer error for {Z}/{X}/{Y}: HTTP {StatusCode}", z, x, y, (int)response.StatusCode);
                return (TileRendererStatus.Failure, null);
            }

            return (TileRendererStatus.Success, await response.Content.ReadAsByteArrayAsync(cancellationToken));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Vector renderer timeout for {Z}/{X}/{Y}", z, x, y);
            return (TileRendererStatus.Failure, null);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Vector renderer unavailable for {Z}/{X}/{Y}", z, x, y);
            return (TileRendererStatus.Failure, null);
        }
    }
}