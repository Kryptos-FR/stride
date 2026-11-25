using ColorRise.Tessera.Editor.Palette;
using ColorRise.Tessera.Editor.TileMapEditor;
using Stride.Core.Mathematics;
using static ColorRise.Tessera.Editor.Tests.TestTown;

namespace ColorRise.Tessera.Editor.Tests;

/// <summary>
/// The geometry of the tools of the map editor, without a map.
/// </summary>
public sealed class TilePaintingTests
{
    private static readonly TileCell A = Cell(0, 0);
    private static readonly TileCell B = Cell(1, 0);
    private static readonly TileCell C = Cell(2, 0);
    private static readonly TileCell D = Cell(3, 0);

    [Theory]
    [InlineData(0, 0, 3, 0, 4)]
    [InlineData(0, 0, 3, 3, 4)]
    [InlineData(2, 5, -1, 4, 4)]
    [InlineData(1, 1, 1, 1, 1)]
    public void A_line_has_every_cell_between_its_ends(int x0, int y0, int x1, int y1, int count)
    {
        var line = TilePainting.Line(new Int2(x0, y0), new Int2(x1, y1)).ToList();

        Assert.Equal(count, line.Count);
        Assert.Equal(new Int2(x0, y0), line[0]);
        Assert.Equal(new Int2(x1, y1), line[^1]);
        Assert.All(line.Zip(line.Skip(1)), pair =>
        {
            Assert.NotEqual(pair.First, pair.Second);
            Assert.InRange(Math.Abs(pair.First.X - pair.Second.X), 0, 1);
            Assert.InRange(Math.Abs(pair.First.Y - pair.Second.Y), 0, 1);
        });
    }

    [Fact]
    public void A_stamp_is_centred_on_the_cursor_and_skips_its_holes()
    {
        // The bottom row A . B, the top row C D .
        var stamp = new TileStamp(new Int2(3, 2), [A, TileCell.Empty, B, C, D, TileCell.Empty]);
        var cells = new Dictionary<Int2, TileCell>();

        TilePainting.Stamp(stamp, new Int2(5, 5), cells);

        Assert.Equal(new Int2(4, 5), TilePainting.StampOrigin(stamp, new Int2(5, 5)));
        Assert.Equal(new Dictionary<Int2, TileCell> { [new Int2(4, 5)] = A, [new Int2(6, 5)] = B, [new Int2(4, 6)] = C, [new Int2(5, 6)] = D }, cells);
    }

    [Fact]
    public void A_rectangle_repeats_the_stamp_from_its_top_left_cell()
    {
        // The bottom row A B, the top row C D
        var stamp = new TileStamp(new Int2(2, 2), [A, B, C, D]);
        var expected = new Dictionary<Int2, TileCell>
        {
            [new Int2(0, 2)] = C, [new Int2(1, 2)] = D, [new Int2(2, 2)] = C,
            [new Int2(0, 1)] = A, [new Int2(1, 1)] = B, [new Int2(2, 1)] = A,
            [new Int2(0, 0)] = C, [new Int2(1, 0)] = D, [new Int2(2, 0)] = C,
        };

        Assert.Equal(expected, TilePainting.Rectangle(stamp, new Int2(0, 0), new Int2(2, 2)));
        Assert.Equal(expected, TilePainting.Rectangle(stamp, new Int2(2, 2), new Int2(0, 0)));
        Assert.Equal(expected, TilePainting.Rectangle(stamp, new Int2(0, 2), new Int2(2, 0)));
        Assert.Equal(expected, TilePainting.Rectangle(stamp, new Int2(2, 0), new Int2(0, 2)));
    }

    [Fact]
    public void A_fill_takes_the_cells_that_touch_by_a_side()
    {
        // A at (0, 0), (1, 0) and (2, 1), which touches (1, 0) by a corner only; B elsewhere
        var a = new HashSet<Int2> { new(0, 0), new(1, 0), new(2, 1) };

        var cells = TilePainting.Fill(cell => a.Contains(cell) ? A : B, new Int2(0, 0), new Rectangle(0, 0, 4, 4), TileStamp.Single(C));

        Assert.Equal(new Dictionary<Int2, TileCell> { [new Int2(0, 0)] = C, [new Int2(1, 0)] = C }, cells);
    }

    [Fact]
    public void A_fill_stops_at_its_bounds()
    {
        var cells = TilePainting.Fill(_ => TileCell.Empty, new Int2(3, 3), new Rectangle(0, 0, 16, 16), TileStamp.Single(C));

        Assert.Equal(256, cells.Count);
        Assert.All(cells.Keys, cell => Assert.True(cell.X is >= 0 and < 16 && cell.Y is >= 0 and < 16, $"{cell}"));
        Assert.Empty(TilePainting.Fill(_ => TileCell.Empty, new Int2(16, 3), new Rectangle(0, 0, 16, 16), TileStamp.Single(C)));
    }

    [Fact]
    public void A_fill_repeats_the_stamp_from_the_cell_zero()
    {
        // The bottom row A B C, the top row D C B
        var stamp = new TileStamp(new Int2(3, 2), [A, B, C, D, C, B]);

        var cells = TilePainting.Fill(_ => TileCell.Empty, new Int2(-1, -1), new Rectangle(-3, -2, 6, 4), stamp);

        Assert.Equal(24, cells.Count);
        Assert.Equal(A, cells[new Int2(-3, -2)]);
        Assert.Equal(B, cells[new Int2(-2, 0)]);
        Assert.Equal(B, cells[new Int2(-1, -1)]);
        Assert.Equal(A, cells[new Int2(0, 0)]);
        Assert.Equal(B, cells[new Int2(2, 1)]);
    }

    [Fact]
    public void A_fill_over_the_same_tile_as_the_stamp_changes_nothing()
    {
        Assert.Empty(TilePainting.Fill(_ => A, new Int2(3, 3), new Rectangle(0, 0, 16, 16), TileStamp.Single(A)));
    }

    // A used width of 0: the layer has no tile
    [Theory]
    [InlineData(8, 8, 0, 0, 2, 2, 0, 0, 8, 8)]
    [InlineData(0, 0, 2, 2, 5, 5, 0, 0, 5, 5)]
    [InlineData(0, 0, -20, 3, 4, 4, -32, 0, 16, 16)]
    [InlineData(0, 0, 40, 40, 0, 0, 32, 32, 16, 16)]
    public void The_bounds_of_a_fill_are_the_frame_then_the_used_cells_then_the_chunk(int sizeX, int sizeY, int startX, int startY, int usedWidth, int usedHeight, int x, int y, int width, int height)
    {
        Rectangle? used = usedWidth > 0 ? new Rectangle(0, 0, usedWidth, usedHeight) : null;

        Assert.Equal(new Rectangle(x, y, width, height), TilePainting.FillBounds(new Int2(sizeX, sizeY), used, new Int2(startX, startY)));
    }

    [Fact]
    public void A_capture_keeps_empty_cells_as_holes_and_the_top_row_on_top()
    {
        // The top row C ., the bottom row A B
        var cells = new Dictionary<Int2, TileCell> { [new Int2(0, 1)] = C, [new Int2(0, 0)] = A, [new Int2(1, 0)] = B };

        var stamp = TilePainting.Capture(cell => cells.GetValueOrDefault(cell), new Int2(1, 1), new Int2(0, 0));

        Assert.Equal(new Int2(2, 2), stamp.Size);
        Assert.Equal(C, stamp[0, 1]);
        Assert.Equal(TileCell.Empty, stamp[1, 1]);
        Assert.Equal(A, stamp[0, 0]);
        Assert.Equal(B, stamp[1, 0]);
    }
}
