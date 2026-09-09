using BigMap.Server.Services;
using Microsoft.AspNetCore.Mvc;
using BigMap.Server.Options;
using Microsoft.Extensions.Options;

namespace BigMap.Server.Controllers;

[ApiController]
[Route("api/tiles")]
public sealed class TilesController : ControllerBase
{
    private readonly TileService tileService;
    private readonly IConfiguration configuration;
    private readonly TileRendererOptions rendererOptions;

    public TilesController(
        TileService tileService,
        IConfiguration configuration,
        IOptions<TileRendererOptions> rendererOptions)
    {
        this.tileService = tileService;
        this.configuration = configuration;
        this.rendererOptions = rendererOptions.Value;
    }

    [HttpGet("layers")]
    public IActionResult GetLayers()
    {
        return Ok(Enum.GetValues<TileLayer>().Select(layer => layer.ToRouteName()));
    }

    [HttpGet("/api/base-tiles/{z:int}/{x:int}/{y:int}.png")]
    public async Task<IActionResult> GetBaseTile(int z, int x, int y, CancellationToken cancellationToken)
    {
        var maxZoom = configuration.GetValue<int>("TileCache:MaxZoom");
        if (!TileCoordinateValidator.IsValid(z, x, y, maxZoom))
        {
            return BadRequest("Invalid tile coordinates.");
        }

        var result = await tileService.GetBaseTileAsync(z, x, y, cancellationToken);
        if (result.Status == TileResultStatus.Success)
        {
            Response.Headers.CacheControl = "public, max-age=86400";
        }

        return result.Status switch
        {
            TileResultStatus.Success => File(result.Png!, "image/png", enableRangeProcessing: false),
            TileResultStatus.NotFound => NotFound(),
            _ => StatusCode(StatusCodes.Status502BadGateway, "Tile renderer is unavailable.")
        };
    }

    [HttpGet("{layer}/{z:int}/{x:int}/{y:int}.png")]
    public async Task<IActionResult> GetTile(TileLayer layer, int z, int x, int y, CancellationToken cancellationToken)
    {
        var layerName = layer.ToRouteName();
        if (!rendererOptions.Layers.ContainsKey(layerName))
        {
            return StatusCode(StatusCodes.Status500InternalServerError, "Tile layer is not configured.");
        }

        var maxZoom = configuration.GetValue<int>("TileCache:MaxZoom");
        if (!TileCoordinateValidator.IsValid(z, x, y, maxZoom))
        {
            return BadRequest("Invalid tile coordinates.");
        }

        var result = await tileService.GetTileAsync(layer, z, x, y, cancellationToken);
        if (result.Status == TileResultStatus.Success)
        {
            Response.Headers.CacheControl = "public, max-age=86400";
        }

        return result.Status switch
        {
            TileResultStatus.Success => File(result.Png!, "image/png", enableRangeProcessing: false),
            TileResultStatus.NotFound => NotFound(),
            _ => StatusCode(StatusCodes.Status502BadGateway, "Tile renderer is unavailable.")
        };
    }
}
