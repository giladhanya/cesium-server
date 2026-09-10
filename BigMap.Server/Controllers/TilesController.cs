using BigMap.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace BigMap.Server.Controllers;

[ApiController]
[Route("api/tiles")]
public sealed class TilesController : ControllerBase
{
    private readonly TileService tileService;
    private readonly int maxZoom;

    public TilesController(
        TileService tileService,
        IConfiguration configuration)
    {
        this.tileService = tileService;
        this.maxZoom = configuration.GetValue<int>("TileCache:MaxZoom");
    }

    [HttpGet("layers")]
    public Task<IActionResult> GetLayers(CancellationToken cancellationToken)
    {
        return GetLayers(0, 0, 0, cancellationToken);
    }

    [HttpGet("layers/{z:int}/{x:int}/{y:int}")]
    public async Task<IActionResult> GetLayers(int z, int x, int y, CancellationToken cancellationToken)
    {
        if (!TileCoordinateValidator.IsValid(z, x, y, maxZoom))
        {
            return BadRequest("Invalid tile coordinates.");
        }

        var layers = await tileService.GetVectorLayerNamesAsync(z, x, y, cancellationToken);
        return layers is null ? NotFound() : Ok(layers);
    }

    [HttpGet("/api/base-tiles/{z:int}/{x:int}/{y:int}.png")]
    public async Task<IActionResult> GetBaseTile(int z, int x, int y, CancellationToken cancellationToken)
    {
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
    public async Task<IActionResult> GetTile(string layer, int z, int x, int y, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(layer) || layer.Contains('/') || layer.Contains('\\'))
        {
            return BadRequest("Invalid tile layer name.");
        }

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
