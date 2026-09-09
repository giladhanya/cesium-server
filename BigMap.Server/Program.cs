using BigMap.Server.Options;
using BigMap.Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOptions<TileCacheOptions>()
    .Bind(builder.Configuration.GetSection(TileCacheOptions.SectionName))
    .Validate(options => options.MaxZoom is >= 0 and <= 30, "MaxZoom must be between 0 and 30.")
    .ValidateOnStart();
builder.Services.AddOptions<TileRendererOptions>()
    .Bind(builder.Configuration.GetSection(TileRendererOptions.SectionName))
    .Validate(options => Enum.GetValues<TileLayer>().All(layer =>
        options.Layers.ContainsKey(layer.ToRouteName())),
        "Every TileLayer enum value must have a renderer mapping.")
    .Validate(options => options.Layers.Keys.All(layer =>
        TileLayerExtensions.TryParse(layer, out _)),
        "TileRenderer:Layers contains an unknown layer.")
    .ValidateOnStart();
builder.Services.AddSingleton<TileCacheService>();
builder.Services.AddSingleton<TileService>();
builder.Services.AddHttpClient<TileRendererClient>();
builder.Services.AddHttpClient<TilePbfClient>();
builder.Services.AddSingleton<VectorTileRenderer>();
builder.Services.AddHttpClient("health-renderer", (serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<TileRendererOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
});
builder.Services.AddCors(options => options.AddPolicy("DevelopmentCesium", policy =>
{
    var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
    if (origins.Length > 0)
    {
        policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
    }
}));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseCors("DevelopmentCesium");
}

app.MapControllers();

app.Run();

public partial class Program;
