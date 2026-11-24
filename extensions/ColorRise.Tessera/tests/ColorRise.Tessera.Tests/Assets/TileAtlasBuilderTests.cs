using ColorRise.Tessera.Assets;
using Stride.Core.IO;
using Stride.Core.Mathematics;
using Stride.Graphics;

namespace ColorRise.Tessera.Tests.Assets;

public sealed class TileAtlasBuilderTests
{
    private static readonly UFile File = new("tiles.png");
    private static readonly Color Red = new(255, 0, 0, 255);
    private static readonly Color Blue = new(0, 0, 255, 255);
    private static readonly Color Magenta = new(255, 0, 255, 255);

    [Fact]
    public void Each_tile_has_a_border_of_two_pixels_that_repeats_its_edge()
    {
        using var image = Pixels(32, 16, (x, _) => x < 16 ? Red : Blue);
        using var atlas = TileAtlasBuilder.Build([Slice(0, 0, 16, 16), Slice(16, 0, 16, 16)], _ => image, null);

        var (texture, r) = atlas.Placements[TileId.FromGrid(1, 0, 0)];
        var pixels = atlas.Textures[texture].PixelBuffer[0];
        foreach (var (x, y) in new[] { (r.X - 1, r.Y), (r.X - 2, r.Y + 5), (r.Right, r.Y + 3), (r.Right + 1, r.Bottom - 1), (r.X + 4, r.Y - 2), (r.X + 4, r.Bottom + 1) })
        {
            Assert.Equal(Red, pixels.GetPixel<Color>(x, y));
        }
    }

    [Fact]
    public void The_corner_of_a_border_repeats_the_corner_pixel()
    {
        var (white, green) = (new Color(255, 255, 255, 255), new Color(0, 255, 0, 255));
        using var image = Pixels(4, 4, (x, y) => (x, y) switch
        {
            (0, 0) => Red,
            (3, 0) => green,
            (0, 3) => Blue,
            (3, 3) => white,
            _ => new Color(0, 0, 0, 255),
        });
        using var atlas = TileAtlasBuilder.Build([Slice(0, 0, 4, 4)], _ => image, null);

        var (texture, r) = atlas.Placements[TileId.FromGrid(1, 0, 0)];
        var pixels = atlas.Textures[texture].PixelBuffer[0];
        Assert.Equal(Red, pixels.GetPixel<Color>(r.X - 2, r.Y - 2));
        Assert.Equal(green, pixels.GetPixel<Color>(r.Right + 1, r.Y - 1));
        Assert.Equal(Blue, pixels.GetPixel<Color>(r.X - 1, r.Bottom + 1));
        Assert.Equal(white, pixels.GetPixel<Color>(r.Right + 1, r.Bottom + 1));
    }

    [Fact]
    public void A_tile_keeps_its_size_and_is_not_rotated()
    {
        using var image = Pixels(16, 24, Gradient);
        using var atlas = TileAtlasBuilder.Build([Slice(0, 0, 16, 24)], _ => image, null);

        var (texture, r) = atlas.Placements[TileId.FromGrid(1, 0, 0)];
        Assert.Equal((16, 24), (r.Width, r.Height));
        Assert.Equal(Gradient(15, 0), atlas.Textures[texture].PixelBuffer[0].GetPixel<Color>(r.X + 15, r.Y));
    }

    [Fact]
    public void The_pixels_of_a_tile_are_those_of_its_slice()
    {
        using var image = Pixels(32, 16, Gradient);
        using var atlas = TileAtlasBuilder.Build([Slice(16, 0, 16, 16)], _ => image, null);

        var (texture, r) = atlas.Placements[TileId.FromGrid(1, 1, 0)];
        var pixels = atlas.Textures[texture].PixelBuffer[0];
        for (var y = 0; y < 16; y++)
        {
            for (var x = 0; x < 16; x++)
            {
                Assert.Equal(Gradient(16 + x, y), pixels.GetPixel<Color>(r.X + x, r.Y + y));
            }
        }
    }

    [Fact]
    public void Tiles_that_do_not_fit_one_texture_go_to_another()
    {
        using var image = Pixels(16 * 20, 16 * 15, (_, _) => Red);
        var slices = TileSlicer.Grid(new GridTileSetSource { Id = 1, Image = File }, new Int2(16, 16), new Int2(16 * 20, 16 * 15));
        var reads = 0;

        using var atlas = TileAtlasBuilder.Build(slices, _ => { reads++; return image; }, null, maxSize: 256);

        Assert.Equal(1, reads);
        Assert.True(atlas.Textures.Count >= 2);
        Assert.All(atlas.Textures, texture => Assert.True(texture.Description.Width <= 256 && texture.Description.Height <= 256));
        Assert.Equal(300, atlas.Placements.Count);
        foreach (var group in atlas.Placements.Values.GroupBy(placement => placement.Texture))
        {
            // With their borders, no two tiles of a texture share a pixel
            var regions = group.Select(placement => Inflate(placement.Region, TileAtlasBuilder.Border)).ToList();
            for (var i = 0; i < regions.Count; i++)
            {
                for (var j = i + 1; j < regions.Count; j++)
                {
                    Assert.False(regions[i].Intersects(regions[j]), $"{regions[i]} and {regions[j]}");
                }
            }
        }
    }

    [Fact]
    public void An_empty_position_is_left_out()
    {
        using var image = Pixels(64, 16, (x, _) => x is >= 32 and < 48 ? new Color(255, 255, 255, 0) : Red);
        var slices = TileSlicer.Grid(new GridTileSetSource { Id = 1, Image = File }, new Int2(16, 16), new Int2(64, 16));

        using var atlas = TileAtlasBuilder.Build(slices, _ => image, null);

        Assert.Equal(3, atlas.Placements.Count);
        Assert.DoesNotContain(TileId.FromGrid(1, 2, 0), atlas.Placements.Keys);
    }

    [Fact]
    public void The_color_key_becomes_transparent()
    {
        // The first tile is the key but one red pixel, the second is the key only
        using var image = Pixels(32, 16, (x, y) => (x, y) == (3, 3) ? Red : Magenta);
        using var atlas = TileAtlasBuilder.Build([Slice(0, 0, 16, 16), Slice(16, 0, 16, 16)], _ => image, Magenta);

        var (texture, r) = Assert.Single(atlas.Placements).Value;
        var pixels = atlas.Textures[texture].PixelBuffer[0];
        Assert.Equal(Color.Transparent, pixels.GetPixel<Color>(r.X, r.Y));
        Assert.Equal(Color.Transparent, pixels.GetPixel<Color>(r.X - 2, r.Y));
        Assert.Equal(Red, pixels.GetPixel<Color>(r.X + 3, r.Y + 3));
    }

    [Fact]
    public void No_slice_gives_no_texture()
    {
        using var atlas = TileAtlasBuilder.Build([], _ => throw new InvalidOperationException("no image is read"), null);
        Assert.Empty(atlas.Textures);
        Assert.Empty(atlas.Placements);
    }

    [Fact]
    public void A_tile_larger_than_a_texture_is_refused()
    {
        using var image = Pixels(64, 64, (_, _) => Red);
        var error = Assert.Throws<ArgumentException>(() => TileAtlasBuilder.Build([Slice(0, 0, 64, 64)], _ => image, null, maxSize: 64));
        Assert.Contains("1:0,0", error.Message, StringComparison.Ordinal);
    }

    // The tile of source 1 whose region in the image starts at (x, y): its column and row are the region's
    private static TileSlice Slice(int x, int y, int width, int height) => new(TileId.FromGrid(1, x / 16, y / 16), File, new Rectangle(x, y, width, height));

    private static Color Gradient(int x, int y) => new((byte)(x * 7), (byte)(y * 9), (byte)(x + y), 255);

    private static Rectangle Inflate(Rectangle region, int by) => new(region.X - by, region.Y - by, region.Width + 2 * by, region.Height + 2 * by);

    private static Image Pixels(int width, int height, Func<int, int, Color> pixel)
    {
        var image = Image.New2D(width, height, 1, PixelFormat.R8G8B8A8_UNorm);
        var buffer = image.PixelBuffer[0];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                buffer.SetPixel(x, y, pixel(x, y));
            }
        }
        return image;
    }
}
