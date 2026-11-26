using ColorRise.Tessera;
using ColorRise.Tessera.Assets;
using Stride.Core.IO;
using Stride.Core.Mathematics;
using Stride.Graphics;

namespace Thornwick.Tests;

/// <summary>
/// A map of the game drawn to a picture from the images of its tile sets, without the engine, for whoever draws a map to look at.
/// </summary>
internal static class MapPicture
{
    /// <summary>
    /// Draws the layers of a map of the game inside its frame, the bottom layer first, from the images of its tile sets: an
    /// animated tile by its first frame, the flips as the game draws them, a cell on as many pixels as its tile has. The top
    /// row of the frame is the top of the picture.
    /// </summary>
    /// <exception cref="NotSupportedException">The map has no frame, or its tile sets have tiles of different sizes.</exception>
    public static (Color[] Pixels, int Width, int Height) Draw(string url)
    {
        var asset = SampleAssets.MapAsset(url);
        var map = SampleAssets.Map(url);
        if (map.Size.X <= 0 || map.Size.Y <= 0)
        {
            throw new NotSupportedException($"The map {url} has no frame.");
        }

        var images = new Dictionary<ushort, SlotImages>();
        foreach (var reference in asset.TileSets)
        {
            if (reference.TileSet is { } tileSet)
            {
                images[(ushort)reference.Slot] = SlotImages.Of(SampleAssets.UrlOf(tileSet));
            }
        }
        var sizes = images.Values.Select(slot => slot.TileSize).Distinct().ToList();
        if (sizes.Count != 1)
        {
            throw new NotSupportedException($"The tile sets of {url} do not have one tile size.");
        }

        var tileSize = sizes[0];
        var (width, height) = (map.Size.X * tileSize.X, map.Size.Y * tileSize.Y);
        var pixels = new Color[width * height];
        foreach (var layer in map.Layers)
        {
            for (var y = 0; y < map.Size.Y; y++)
            {
                for (var x = 0; x < map.Size.X; x++)
                {
                    var cell = layer.GetCell(new Int2(x, y));
                    if (map.GetTile(cell) is not { } tile || !images.TryGetValue(cell.Slot, out var slot))
                    {
                        continue;
                    }
                    var shown = tile.Animation.Count > 0 ? map.TileSets[cell.Slot].Tiles.GetValueOrDefault(tile.Animation[0].Tile) ?? tile : tile;
                    var left = x * tileSize.X;
                    var top = (map.Size.Y - 1 - y) * tileSize.Y;
                    DrawTile(pixels, width, left, top, slot, shown, cell.Flip);
                }
            }
        }
        return (pixels, width, height);
    }

    /// <summary>
    /// Saves the picture as <c>pictures/&lt;name&gt;.png</c> beside the test assembly, and gives the path.
    /// </summary>
    public static string Save(string url)
    {
        var (pixels, width, height) = Draw(url);
        var folder = Path.Combine(AppContext.BaseDirectory, "pictures");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, Path.GetFileName(url) + ".png");
        using var image = Image.New2D(width, height, 1, PixelFormat.R8G8B8A8_UNorm);
        image.PixelBuffer[0].SetPixels(pixels);
        using var stream = File.Create(path);
        image.Save(stream, ImageFileType.Png);
        return path;
    }

    // The tile's pixels over those under it, its alpha blending them. The screen pixel (sx, sy) of the cell finds its texel
    // by undoing the flips in the order TileQuads undoes them: vertical, horizontal, then diagonal.
    private static void DrawTile(Color[] pixels, int width, int left, int top, SlotImages slot, Tile tile, TileFlip flip)
    {
        if (!slot.Sources.TryGetValue(tile.Id.Source, out var source))
        {
            return;
        }
        var region = tile.Region;
        for (var sy = 0; sy < region.Height; sy++)
        {
            for (var sx = 0; sx < region.Width; sx++)
            {
                var (tx, ty) = (sx, sy);
                if ((flip & TileFlip.Vertical) != 0)
                {
                    ty = region.Height - 1 - ty;
                }
                if ((flip & TileFlip.Horizontal) != 0)
                {
                    tx = region.Width - 1 - tx;
                }
                if ((flip & TileFlip.Diagonal) != 0)
                {
                    (tx, ty) = (ty, tx);
                }
                var texel = source.Pixels[(region.Y + ty) * source.Width + region.X + tx];
                if (slot.ColorKey is { } key && texel == key)
                {
                    continue;
                }
                ref var under = ref pixels[(top + sy) * width + left + sx];
                under = Blend(texel, under);
            }
        }
    }

    private static Color Blend(Color over, Color under)
    {
        if (over.A == 255 || under.A == 0)
        {
            return over;
        }
        if (over.A == 0)
        {
            return under;
        }
        var a = over.A / 255f;
        var alpha = a + under.A / 255f * (1 - a);
        byte Mix(byte top, byte bottom) => (byte)Math.Round((top * a + bottom * under.A / 255f * (1 - a)) / alpha);
        return new Color(Mix(over.R, under.R), Mix(over.G, under.G), Mix(over.B, under.B), (byte)Math.Round(alpha * 255));
    }

    // The images of the sources of a tile set, by the id of the source, its tile size and its colour key
    private sealed record SlotImages(Int2 TileSize, Color? ColorKey, Dictionary<int, (Color[] Pixels, int Width, int Height)> Sources)
    {
        public static SlotImages Of(string url)
        {
            var asset = SampleAssets.TileSetAsset(url);
            var folder = new UFile(SampleFiles.Asset(url, TileSetAsset.FileExtension)).GetParent();
            var sources = new Dictionary<int, (Color[] Pixels, int Width, int Height)>();
            foreach (var source in asset.Sources)
            {
                if (source is GridTileSetSource { Image: { } file })
                {
                    sources[source.Id] = SampleAssets.Image(UPath.Combine(folder, file).ToOSPath());
                }
            }
            return new SlotImages(asset.TileSize, asset.ColorKeyEnabled ? asset.ColorKeyColor : null, sources);
        }
    }
}
