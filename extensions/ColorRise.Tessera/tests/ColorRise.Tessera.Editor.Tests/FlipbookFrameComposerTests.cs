using ColorRise.Tessera.Editor.FlipbookEditor;
using ColorRise.Tessera.Editor.Pictures;
using ColorRise.Tessera.Editor.TileMapEditor;
using Stride.Core.Mathematics;

namespace ColorRise.Tessera.Editor.Tests;

/// <summary>
/// The picture of a flipbook frame, made from tiles of pictures the test draws: where it is around the pivot, and its pixels.
/// </summary>
public sealed class FlipbookFrameComposerTests
{
    // A tile of 16 by 16 pixels whose quarters are red (top left), lime (top right), blue (bottom left) and yellow
    private static readonly TileId Square = TileId.Parse("1:0,0");
    // A tile of 16 by 24 pixels: its top row red, the rest blue
    private static readonly TileId Tall = TileId.Parse("2:0,0");
    // A tile of 16 by 16 pixels, all white
    private static readonly TileId White = TileId.Parse("1:1,0");
    private static readonly Int2 TileSize = new(16, 16);

    [Fact]
    public void The_pivot_is_the_origin_of_the_bounds()
    {
        var picture = Compose(Frame((Square, Int2.Zero, TileFlip.None)), new Vector2(0.5f, 0))!;

        Assert.Equal(new Rectangle(-8, 0, 16, 16), picture.Bounds);
        Assert.False(picture.HasMissing);
        Assert.Equal((16, 16), (picture.Picture.Width, picture.Picture.Height));
        Assert.Equal(Color.Red, picture.Picture[2, 2]);
        Assert.Equal(Color.Yellow, picture.Picture[13, 13]);
    }

    // Review Focus 4
    [Fact]
    public void A_tall_frame_stands_on_the_pivot()
    {
        var picture = Compose(Frame((Tall, Int2.Zero, TileFlip.None)), new Vector2(0.5f, 0))!;

        Assert.Equal(new Rectangle(-8, 0, 16, 24), picture.Bounds);
        Assert.Equal((16, 24), (picture.Picture.Width, picture.Picture.Height));
        Assert.All(Enumerable.Range(0, 16), x => Assert.Equal(Color.Red, picture.Picture[x, 0]));
        Assert.Equal(Color.Blue, picture.Picture[8, 1]);
        Assert.Equal(Color.Blue, picture.Picture[8, 23]);
    }

    [Fact]
    public void A_pivot_at_the_corner_puts_the_frame_beside_it()
    {
        Assert.Equal(new Rectangle(0, 0, 16, 16), Compose(Frame((Square, Int2.Zero, TileFlip.None)), Vector2.Zero)!.Bounds);
        Assert.Equal(new Rectangle(-16, -16, 16, 16), Compose(Frame((Square, Int2.Zero, TileFlip.None)), Vector2.One)!.Bounds);
        // The pivot is measured on the first cell: a tall tile moves it further
        Assert.Equal(new Rectangle(-16, -24, 16, 24), Compose(Frame((Tall, Int2.Zero, TileFlip.None)), Vector2.One)!.Bounds);
    }

    [Fact]
    public void A_flipped_cell_is_drawn_flipped()
    {
        var picture = Compose(Frame((Square, Int2.Zero, TileFlip.Horizontal)), new Vector2(0.5f, 0))!;

        Assert.Equal(Color.Lime, picture.Picture[2, 2]);
        Assert.Equal(Color.Red, picture.Picture[13, 2]);
        Assert.Equal(Color.Yellow, picture.Picture[2, 13]);
    }

    [Fact]
    public void A_second_cell_is_placed_by_its_offset()
    {
        var picture = Compose(Frame((Square, Int2.Zero, TileFlip.None), (White, new Int2(0, 16), TileFlip.None)), new Vector2(0.5f, 0))!;

        Assert.Equal(new Rectangle(-8, 0, 16, 32), picture.Bounds);
        // y up: the second cell is above the first
        Assert.Equal(Color.White, picture.Picture[8, 8]);
        Assert.Equal(Color.Red, picture.Picture[2, 18]);
        Assert.Equal(Color.Yellow, picture.Picture[13, 29]);

        // A cell to the right and down widens the picture both ways
        var wide = Compose(Frame((Square, Int2.Zero, TileFlip.None), (White, new Int2(16, -8), TileFlip.None)), new Vector2(0.5f, 0))!;
        Assert.Equal(new Rectangle(-8, -8, 32, 24), wide.Bounds);
        Assert.Equal(Color.White, wide.Picture[24, 20]);
        Assert.Equal(Color.Transparent, wide.Picture[24, 2]);
        Assert.Equal(Color.Red, wide.Picture[2, 2]);
    }

    // Review Focus 1
    [Fact]
    public void A_missing_tile_is_a_marked_box_and_the_picture_says_so()
    {
        var picture = Compose(Frame((TileId.Parse("1:9,9"), Int2.Zero, TileFlip.None)), new Vector2(0.5f, 0))!;

        Assert.True(picture.HasMissing);
        Assert.Equal(new Rectangle(-8, 0, 16, 16), picture.Bounds);
        Assert.Equal(TileComposer.MissingColor, picture.Picture[8, 2]);
        Assert.Equal(Color.Black, picture.Picture[0, 0]);

        // Beside a tile that is there
        var mixed = Compose(Frame((Square, Int2.Zero, TileFlip.None), (TileId.Parse("1:9,9"), new Int2(16, 0), TileFlip.None)), Vector2.Zero)!;
        Assert.True(mixed.HasMissing);
        Assert.Equal(Color.Red, mixed.Picture[2, 2]);
        Assert.Equal(TileComposer.MissingColor, mixed.Picture[24, 2]);
    }

    [Fact]
    public void A_frame_without_a_cell_has_no_picture()
    {
        Assert.Null(Compose(new FlipbookFrame(), new Vector2(0.5f, 0)));
    }

    private static FlipbookFramePicture? Compose(FlipbookFrame frame, Vector2 pivot) => FlipbookFrameComposer.Compose(frame, pivot, Find, TileSize);

    private static FlipbookFrame Frame(params (TileId Tile, Int2 Offset, TileFlip Flip)[] cells)
    {
        var frame = new FlipbookFrame();
        foreach (var (tile, offset, flip) in cells)
        {
            frame.Cells.Add(new FlipbookCell { Tile = tile, Offset = offset, Flip = flip });
        }
        return frame;
    }

    private static TileSlot? Find(TileId tile)
    {
        if (tile == Square)
        {
            return Slot(tile, Draw(16, 16, (x, y) => (x < 8, y < 8) switch
            {
                (true, true) => Color.Red,
                (false, true) => Color.Lime,
                (true, false) => Color.Blue,
                _ => Color.Yellow,
            }));
        }
        if (tile == Tall)
        {
            return Slot(tile, Draw(16, 24, (_, y) => y == 0 ? Color.Red : Color.Blue));
        }
        return tile == White ? Slot(tile, Draw(16, 16, (_, _) => Color.White)) : null;
    }

    // A tile in the middle of a larger picture, so that the region counts
    private static TileSlot Slot(TileId tile, (TilePicture Picture, Rectangle Region) drawn)
        => new(tile, new Rectangle(0, 0, drawn.Region.Width, drawn.Region.Height), drawn.Picture, drawn.Region, null);

    private static (TilePicture Picture, Rectangle Region) Draw(int width, int height, Func<int, int, Color> pixel)
    {
        var picture = new TilePicture(width + 8, height + 8);
        for (var y = 0; y < picture.Height; y++)
        {
            for (var x = 0; x < picture.Width; x++)
            {
                picture[x, y] = x >= 4 && y >= 4 && x < width + 4 && y < height + 4 ? pixel(x - 4, y - 4) : Color.Gray;
            }
        }
        return (picture, new Rectangle(4, 4, width, height));
    }
}
