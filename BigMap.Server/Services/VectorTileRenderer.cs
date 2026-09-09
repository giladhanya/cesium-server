using System.IO.Compression;
using BigMap.Server.Options;
using Mapbox.Vector.Tile;
using SkiaSharp;

namespace BigMap.Server.Services;

public sealed class VectorTileRenderer
{
    private const float TileSize = 256;
    private const float DefaultExtent = 4096;

    public byte[] Render(byte[] pbf, TileLayer layer)
    {
        using var input = new MemoryStream(pbf);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        var layers = VectorTileParser.Parse(gzip);
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

    private static SKPaint CreatePaint(TileLayer layer) => new()
    {
        Style = layer is TileLayer.Landcover or TileLayer.Water
            ? SKPaintStyle.Fill
            : SKPaintStyle.Stroke,
        Color = layer switch
        {
            TileLayer.Water => new SKColor(55, 145, 220, 210),
            TileLayer.WaterName or TileLayer.Place => new SKColor(35, 35, 35, 220),
            _ => new SKColor(105, 155, 85, 180)
        },
        StrokeWidth = layer == TileLayer.Boundary ? 2 : 1,
        IsAntialias = true
    };

    private static void DrawFeature(SKCanvas canvas, SKPaint paint, VectorTileFeature feature)
    {
        foreach (var segment in feature.Geometry)
        {
            using var path = CreatePath(segment, feature.Extent);
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

    private static SKPath CreatePath(IReadOnlyList<Coordinate> segment, uint extent)
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

        return builder.Detach();
    }

}