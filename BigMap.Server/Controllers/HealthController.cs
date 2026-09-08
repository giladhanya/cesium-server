using BigMap.Server.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BigMap.Server.Controllers;

[ApiController]
public sealed class HealthController : ControllerBase
{
    private readonly IHttpClientFactory httpClientFactory;

    public HealthController(IHttpClientFactory httpClientFactory)
    {
        this.httpClientFactory = httpClientFactory;
    }

    [HttpGet("/health")]
    public async Task<IActionResult> GetHealth(CancellationToken cancellationToken)
    {
        var rendererStatus = "ok";
        try
        {
            using var client = httpClientFactory.CreateClient("health-renderer");
            client.Timeout = TimeSpan.FromSeconds(2);
            using var response = await client.GetAsync("health", cancellationToken);
            rendererStatus = response.IsSuccessStatusCode ? "ok" : "unavailable";
        }
        catch (HttpRequestException)
        {
            rendererStatus = "unavailable";
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            rendererStatus = "unavailable";
        }

        return Ok(new { status = "ok", renderer = rendererStatus });
    }
}
