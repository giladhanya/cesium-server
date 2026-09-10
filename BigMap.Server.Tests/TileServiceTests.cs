using BigMap.Server.Options;
using System.IO.Compression;
using BigMap.Server.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BigMap.Server.Tests;

public sealed class TileServiceTests
{
    [Fact]
    public async Task CacheHitDoesNotCallRenderer()
    {
        var root = CreateTempDirectory();
        await File.WriteAllBytesAsync(Path.Combine(root, "bigmap-v1", "base", "0", "0_0.png"), Png());
        var renderer = new FakeRenderer();
        var service = CreateService(root, renderer);

        var result = await service.GetBaseTileAsync(0, 0, 0, CancellationToken.None);

        Assert.Equal(TileResultStatus.Success, result.Status);
        Assert.Equal(0, renderer.Calls);
    }

    [Fact]
    public async Task ConcurrentMissesRenderOnceAndSavePng()
    {
        var root = CreateTempDirectory();
        var renderer = new FakeRenderer { Delay = TimeSpan.FromMilliseconds(50) };
        var service = CreateService(root, renderer);

        var results = await Task.WhenAll(
            service.GetBaseTileAsync(1, 0, 1, CancellationToken.None),
            service.GetBaseTileAsync(1, 0, 1, CancellationToken.None));

        Assert.All(results, result => Assert.Equal(TileResultStatus.Success, result.Status));
        Assert.Equal(1, renderer.Calls);
        Assert.True(File.Exists(Path.Combine(root, "bigmap-v1", "base", "1", "0_1.png")));
    }

    [Fact]
    public async Task DifferentLayersUseSeparateCacheEntries()
    {
        var root = CreateTempDirectory();
        var renderer = new FakeRenderer();
        var service = CreateService(root, renderer);

        var baseResult = await service.GetBaseTileAsync(1, 0, 1, CancellationToken.None);
        var landcoverResult = await service.GetTileAsync("landuse", 1, 0, 1, CancellationToken.None);

        Assert.Equal(TileResultStatus.Success, baseResult.Status);
        Assert.Equal(TileResultStatus.Success, landcoverResult.Status);
        Assert.Equal(1, renderer.Calls);
        Assert.True(File.Exists(Path.Combine(root, "bigmap-v1", "base", "1", "0_1.png")));
        Assert.True(File.Exists(Path.Combine(root, "bigmap-v1", "landuse", "1", "0_1.png")));
        Assert.True(File.Exists(Path.Combine(root, "bigmap-v1", "pbf", "1", "0_1.pbf")));
    }

    [Fact]
    public async Task ConcurrentLayersCanSaveSharedPbf()
    {
        var root = CreateTempDirectory();
        var service = CreateService(root, new FakeRenderer());

        var results = await Task.WhenAll(
            service.GetTileAsync("landuse", 1, 0, 1, CancellationToken.None),
            service.GetTileAsync("boundary", 1, 0, 1, CancellationToken.None));

        Assert.All(results, result => Assert.Equal(TileResultStatus.Success, result.Status));
        Assert.True(File.Exists(Path.Combine(root, "bigmap-v1", "pbf", "1", "0_1.pbf")));
    }

    [Fact]
    public async Task RendererNotFoundReturnsNotFound()
    {
        var renderer = new FakeRenderer { Status = TileRendererStatus.NotFound };
        var service = CreateService(CreateTempDirectory(), renderer);

        var result = await service.GetBaseTileAsync(0, 0, 0, CancellationToken.None);

        Assert.Equal(TileResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task RendererFailureReturnsFailure()
    {
        var renderer = new FakeRenderer { Status = TileRendererStatus.Failure };
        var service = CreateService(CreateTempDirectory(), renderer);

        var result = await service.GetBaseTileAsync(0, 0, 0, CancellationToken.None);

        Assert.Equal(TileResultStatus.RendererFailure, result.Status);
    }

    [Theory]
    [InlineData(-1, 0, 0, false)]
    [InlineData(0, 1, 0, false)]
    [InlineData(2, 0, 4, false)]
    [InlineData(2, 3, 3, true)]
    public void InvalidCoordinatesAreRejected(int z, int x, int y, bool expected)
    {
        Assert.Equal(expected, TileCoordinateValidator.IsValid(z, x, y, 14));
    }

    private static TileService CreateService(string root, FakeRenderer fakeRenderer)
    {
        var cache = new TileCacheService(
            Microsoft.Extensions.Options.Options.Create(new TileCacheOptions { RootPath = root }),
            NullLogger<TileCacheService>.Instance);
        return new TileService(
            cache,
            fakeRenderer,
            new FakePbfClient(),
            new VectorTileRenderer(),
            Microsoft.Extensions.Options.Options.Create(new TileCacheOptions { RootPath = root }),
            NullLogger<TileService>.Instance);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "bigmap-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(path, "bigmap-v1", "base", "0"));
        return path;
    }

    private static byte[] Png() => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private sealed class FakeRenderer : TileRendererClient
    {
        public FakeRenderer() : base(
            new HttpClient(new FakeHandler()),
            Microsoft.Extensions.Options.Options.Create(new TileRendererOptions()),
            NullLogger<TileRendererClient>.Instance)
        {
        }

        public int Calls { get; private set; }
        public TimeSpan Delay { get; init; }
        public TileRendererStatus Status { get; init; } = TileRendererStatus.Success;

        public override async Task<TileRendererResult> RenderBaseAsync(int z, int x, int y, CancellationToken cancellationToken)
        {
            Calls++;
            await Task.Delay(Delay, cancellationToken);
            return new(Status, Status == TileRendererStatus.Success ? Png() : null);
        }
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
    }

    private sealed class FakePbfClient : TilePbfClient
    {
        public FakePbfClient() : base(
            new HttpClient(new FakeHandler()),
            Microsoft.Extensions.Options.Options.Create(new TileRendererOptions()),
            NullLogger<TilePbfClient>.Instance)
        {
        }

        public override Task<(TileRendererStatus Status, byte[]? Pbf)> GetAsync(int z, int x, int y, CancellationToken cancellationToken) =>
            Task.FromResult<(TileRendererStatus, byte[]?)>((TileRendererStatus.Success, EmptyPbf()));

        private static byte[] EmptyPbf()
        {
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
            {
            }

            return output.ToArray();
        }
    }
}
