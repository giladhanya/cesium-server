using BigMap.Server.Options;
using BigMap.Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddOptions<TileCacheOptions>()
    .Bind(builder.Configuration.GetSection(TileCacheOptions.SectionName))
    .Validate(options => options.MaxZoom is >= 0 and <= 30, "MaxZoom must be between 0 and 30.")
    .ValidateOnStart();
builder.Services.AddOptions<TileRendererOptions>()
    .Bind(builder.Configuration.GetSection(TileRendererOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<TileCacheService>();
builder.Services.AddSingleton<TileService>();
builder.Services.AddHttpClient<TileRendererClient>();
builder.Services.AddHttpClient<TileStyleClient>();
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
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();

public partial class Program;
