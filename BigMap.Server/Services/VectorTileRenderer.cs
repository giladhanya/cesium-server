using System.IO.Compression;
using BigMap.Server.Options;
using Mapbox.Vector.Tile;
using SkiaSharp;

namespace BigMap.Server.Services;

public sealed class VectorTileRenderer
{
    private const float TileSize = 256;
    private const float DefaultExtent = 4096;
    public byte[] Render(byte[] pbf, string layer)
    {
        var layers = ParseLayers(pbf).Where(value => string.Equals(value.Name, layer, StringComparison.OrdinalIgnoreCase));
        var features = layers.SelectMany(value => value.VectorTileFeatures);

        using var bitmap = new SKBitmap((int)TileSize, (int)TileSize);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        using var paint = CreatePaint(layer);
        foreach (var feature in features)
        {
            DrawFeature(canvas, paint, feature);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    public IReadOnlyList<string> GetLayerNames(byte[] pbf) =>
        ParseLayers(pbf).Select(value => value.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    private static IEnumerable<VectorTileLayer> ParseLayers(byte[] pbf)
    {
        using var input = new MemoryStream(pbf);
        using Stream vectorData = IsGzip(pbf) ? new GZipStream(input, CompressionMode.Decompress) : input;
        return VectorTileParser.Parse(vectorData).ToArray();
    }

    private static SKPaint CreatePaint(string layer) => new()
    {
        Color = layer.Contains("water", StringComparison.OrdinalIgnoreCase)
            ? new SKColor(55, 145, 220, 210)
            : layer.Contains("name", StringComparison.OrdinalIgnoreCase) || layer.Contains("place", StringComparison.OrdinalIgnoreCase)
                ? new SKColor(35, 35, 35, 220)
                : new SKColor(105, 155, 85, 180),
        StrokeWidth = layer.Equals("boundary", StringComparison.OrdinalIgnoreCase) ? 2 : 1,
        IsAntialias = true
    };

    private static bool IsGzip(byte[] data) => data.Length >= 2 && data[0] == 0x1F && data[1] == 0x8B;

    private static void DrawFeature(SKCanvas canvas, SKPaint paint, VectorTileFeature feature)
    {
        foreach (var segment in feature.Geometry)
        {
            using var path = CreatePath(segment, feature.Extent, feature.GeometryType == Tile.GeomType.Polygon);
            if (feature.GeometryType == Tile.GeomType.Polygon)
            {
                paint.Style = SKPaintStyle.Fill;
                canvas.DrawPath(path, paint);
            }
            else if (feature.GeometryType == Tile.GeomType.LineString)
            {
                paint.Style = SKPaintStyle.Stroke;
                canvas.DrawPath(path, paint);
            }
            else
            {
                if (segment.Count == 0)
                {
                    continue;
                }

                var coordinate = segment[0];
                var scale = TileSize / (feature.Extent == 0 ? DefaultExtent : feature.Extent);
                canvas.DrawCircle(new SKPoint(coordinate.X * scale, coordinate.Y * scale), 2.5f, paint);
            }
        }
    }

    private static SKPath CreatePath(IReadOnlyList<Coordinate> segment, uint extent, bool closePath)
    {
        using var builder = new SKPathBuilder();
        var scale = TileSize / (extent == 0 ? DefaultExtent : extent);
        var first = true;
        foreach (var coordinate in segment)
        {
            var point = new SKPoint(coordinate.X * scale, coordinate.Y * scale);
            if (first)
            {
                builder.MoveTo(point);
                first = false;
            }
            else
            {
                builder.LineTo(point);
            }
        }

        if (closePath && segment.Count > 0)
        {
            builder.Close();
        }

        var path = builder.Detach();
        path.FillType = SKPathFillType.EvenOdd;
        return path;
    }

}