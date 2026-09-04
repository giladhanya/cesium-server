using BigMap.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace BigMap.Server.Controllers;

[ApiController]
[Route("api/tiles")]
public sealed class TilesController : ControllerBase
{
    private readonly TileService tileService;
    private readonly IConfiguration configuration;

    public TilesController(TileService tileService, IConfiguration configuration)
    {
        this.tileService = tileService;
        this.configuration = configuration;
    }

    [HttpGet("{z:int}/{x:int}/{y:int}.png")]
    public async Task<IActionResult> GetTile(int z, int x, int y, CancellationToken cancellationToken)
    {
        var maxZoom = configuration.GetValue<int>("TileCache:MaxZoom");
        if (!TileCoordinateValidator.IsValid(z, x, y, maxZoom))
        {
            return BadRequest("Invalid tile coordinates.");
        }

        var result = await tileService.GetTileAsync(z, x, y, cancellationToken);
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
