using ColorRise.Tessera.Editor.Palette;
using ColorRise.Tessera.Editor.Pictures;
using ColorRise.Tessera.Editor.TileMapEditor;
using Stride.Core.Mathematics;

namespace ColorRise.Tessera.Editor.Tests;

/// <summary>
/// The tiles drawn into pictures on the CPU, as the runtime draws them on the GPU.
/// </summary>
public sealed class TileComposerTests
{
    // The tile (0, 0) of the town: red at the top left, green at the top right, blue at the bottom left, yellow at the
    // bottom right
    private static readonly Rectangle Quarters = new(0, 0, 16, 16);

    // The same cases as the runtime's TileMapDrawTests: the corners top left, top right, bottom left, bottom right
    [Theory]
    [InlineData("", "RGBY")]
    [InlineData("h", "GRYB")]
    [InlineData("v", "BYRG")]
    [InlineData("hv", "YBGR")]
    [InlineData("d", "RBGY")]
    [InlineData("dh", "BRYG")]
    [InlineData("dv", "GYRB")]
    [InlineData("dhv", "YGBR")]
    public void A_flipped_tile_has_the_corners_the_runtime_gives_it(string flip, string corners)
    {
        var target = new TilePicture(16, 16);

        TileComposer.Draw(target, new Rectangle(0, 0, 16, 16), Town(), Quarters, Flip(flip), Color.White);

        Assert.Equal(corners, Corners(target, new Rectangle(0, 0, 16, 16)));
    }

    [Fact]
    public void A_stamp_turned_clockwise_draws_the_tile_turned_clockwise()
    {
        var flip = TileStamp.Single(TestTown.Cell(0, 0)).RotateClockwise()[0, 0].Flip;
        var target = new TilePicture(16, 16);

        TileComposer.Draw(target, new Rectangle(0, 0, 16, 16), Town(), Quarters, flip, Color.White);

        Assert.Equal("BRYG", Corners(target, new Rectangle(0, 0, 16, 16)));
    }

    // The runtime stretches a tile that is not square to its quad, and turns the texture inside it
    [Fact]
    public void A_tile_that_is_not_square_is_turned_inside_its_own_box()
    {
        // 16 wide and 32 high: the quarters over the flat tile (0, 1)
        var target = new TilePicture(16, 32);

        TileComposer.Draw(target, new Rectangle(0, 0, 16, 32), Town(), new Rectangle(0, 0, 16, 32), TileFlip.Diagonal, Color.White);

        // The texel (u, v) of the screen corner (x, y) is (y, x): the left column of the box shows the top row of the tile,
        // stretched to twice its length
        Assert.Equal(Color.Red, target[0, 0]);
        Assert.Equal(Color.Red, target[0, 15]);
        Assert.Equal(Color.Lime, target[0, 16]);
        Assert.Equal(Color.Blue, target[4, 0]);
        Assert.Equal(TestTown.Flat(0, 1), target[15, 0]);
    }

    [Fact]
    public void A_tile_is_scaled_with_hard_pixels_and_clipped()
    {
        var source = new TilePicture(2, 2);
        (source[0, 0], source[1, 0], source[0, 1], source[1, 1]) = (Color.Red, Color.Lime, Color.Blue, Color.Yellow);
        var target = new TilePicture(4, 4);

        TileComposer.Draw(target, new Rectangle(0, 0, 4, 4), source, new Rectangle(0, 0, 2, 2), TileFlip.None, Color.White);

        Assert.Equal([Color.Red, Color.Red, Color.Lime, Color.Lime], Row(target, 0));
        Assert.Equal([Color.Red, Color.Red, Color.Lime, Color.Lime], Row(target, 1));
        Assert.Equal([Color.Blue, Color.Blue, Color.Yellow, Color.Yellow], Row(target, 3));

        // 16 pixels into 32: blocks of 2 by 2
        var big = new TilePicture(32, 32);
        TileComposer.Draw(big, new Rectangle(0, 0, 32, 32), Town(), Quarters, TileFlip.None, Color.White);
        Assert.Equal(Color.Red, big[15, 15]);
        Assert.Equal(Color.Lime, big[16, 15]);
        Assert.Equal(Color.Blue, big[15, 16]);

        // Half outside the target, on each side: only what is inside is drawn
        var clipped = new TilePicture(4, 4);
        TileComposer.Draw(clipped, new Rectangle(-2, -2, 4, 4), source, new Rectangle(0, 0, 2, 2), TileFlip.None, Color.White);
        TileComposer.Draw(clipped, new Rectangle(2, 2, 4, 4), source, new Rectangle(0, 0, 2, 2), TileFlip.None, Color.White);
        Assert.Equal(Color.Yellow, clipped[0, 0]);
        Assert.Equal(Color.Red, clipped[3, 3]);
        Assert.Equal(Color.Transparent, clipped[3, 0]);
    }

    [Fact]
    public void Transparent_pixels_keep_what_is_under_and_the_tint_multiplies()
    {
        var target = new TilePicture(16, 16);
        Array.Fill(target.Pixels, Color.Red);

        // The tile (3, 1) of the town is fully transparent
        TileComposer.Draw(target, new Rectangle(0, 0, 16, 16), Town(), new Rectangle(48, 16, 16, 16), TileFlip.None, Color.White);
        Assert.All(target.Pixels, pixel => Assert.Equal(Color.Red, pixel));

        TileComposer.Draw(target, new Rectangle(0, 0, 16, 16), Town(), new Rectangle(16, 0, 16, 16), TileFlip.None, new Color(128, 255, 64, 255));
        var flat = TestTown.Flat(1, 0);
        Assert.Equal(new Color((byte)(flat.R * 128 / 255), flat.G, (byte)(flat.B * 64 / 255), (byte)255), target[8, 8]);

        // Half of a blue over red
        var half = new TilePicture(1, 1);
        half[0, 0] = new Color(0, 0, 255, 128);
        TileComposer.Draw(target, new Rectangle(0, 0, 1, 1), half, new Rectangle(0, 0, 1, 1), TileFlip.None, Color.White);
        var blended = target[0, 0];
        var under = target[1, 1];
        Assert.InRange(blended.R, under.R / 2 - 2, under.R / 2 + 2);
        Assert.InRange(blended.B, (255 + under.B) / 2 - 2, (255 + under.B) / 2 + 2);
        Assert.Equal(255, blended.A);
    }

    [Fact]
    public void A_missing_tile_is_a_magenta_box_with_a_cross()
    {
        var target = new TilePicture(16, 16);

        TileComposer.DrawMissing(target, new Rectangle(0, 0, 16, 16));

        Assert.Equal(new Color(255, 0, 255, 255), TileComposer.MissingColor);
        Assert.Equal(TileComposer.MissingColor, target[1, 8]);
        Assert.Equal(TileComposer.MissingColor, target[8, 1]);
        // The two diagonals
        Assert.Equal(Color.Black, target[0, 0]);
        Assert.Equal(Color.Black, target[8, 8]);
        Assert.Equal(Color.Black, target[15, 0]);
        Assert.Equal(Color.Black, target[0, 15]);

        var clipped = new TilePicture(4, 4);
        TileComposer.DrawMissing(clipped, new Rectangle(-8, -8, 16, 16));
        Assert.Equal(Color.Black, clipped[0, 0]);
        Assert.Equal(TileComposer.MissingColor, clipped[3, 0]);
    }

    // The picture of "town.png"
    private static TilePicture Town()
    {
        var picture = new TilePicture(64, 32);
        for (var y = 0; y < picture.Height; y++)
        {
            for (var x = 0; x < picture.Width; x++)
            {
                picture[x, y] = TestTown.Pixel(x, y);
            }
        }
        return picture;
    }

    private static TileFlip Flip(string letters)
    {
        var flip = TileFlip.None;
        foreach (var letter in letters)
        {
            flip |= letter switch { 'd' => TileFlip.Diagonal, 'h' => TileFlip.Horizontal, _ => TileFlip.Vertical };
        }
        return flip;
    }

    // The colours near the four corners of a box: top left, top right, bottom left, bottom right
    private static string Corners(TilePicture picture, Rectangle box)
    {
        var (left, right, top, bottom) = (box.Left + 2, box.Right - 3, box.Top + 2, box.Bottom - 3);
        return string.Concat(new[] { picture[left, top], picture[right, top], picture[left, bottom], picture[right, bottom] }.Select(Letter));
    }

    private static char Letter(Color color)
        => color == Color.Red ? 'R' : color == Color.Lime ? 'G' : color == Color.Blue ? 'B' : color == Color.Yellow ? 'Y' : '?';

    private static Color[] Row(TilePicture picture, int y) => [.. Enumerable.Range(0, picture.Width).Select(x => picture[x, y])];
}
