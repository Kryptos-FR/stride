using ColorRise.Tessera.Rendering;
using Stride.Core.Mathematics;
using Stride.Graphics;

namespace ColorRise.Tessera.Tests;

/// <summary>
/// Where the cells of a frame are drawn, without a GPU: the tiles have a region and a texture that stands in, which has no
/// size, so their texture coordinates are not looked at here (the draw tests see them).
/// </summary>
public sealed class FlipbookQuadsTests
{
    private const float Tolerance = 1e-5f;

    [Fact]
    public void The_pivot_of_the_first_cell_is_on_the_entity()
    {
        var flipbook = Flipbook(Set(4, 4));
        var frame = Frame(Cell(0));

        Assert.True(FlipbookQuads.TryCell(flipbook, frame, 0, Matrix.Identity, false, out var quad, out var tile));

        Assert.Same(flipbook.TileSet!.GetTile(TileId.FromGrid(1, 0, 0)), tile);
        AssertCorners(quad, new Vector2(-0.5f, 0), new Vector2(0.5f, 1));
    }

    // Review Focus 4
    [Fact]
    public void A_tall_tile_stands_on_its_feet()
    {
        // 4 by 6 pixels at 4 a unit, in a set whose tiles are 4 by 4: the frame's size comes from its tile
        var flipbook = Flipbook(Set(4, 6));

        Assert.True(FlipbookQuads.TryCell(flipbook, Frame(Cell(0)), 0, Matrix.Identity, false, out var quad, out _));

        AssertCorners(quad, new Vector2(-0.5f, 0), new Vector2(0.5f, 1.5f));
    }

    [Fact]
    public void A_second_cell_is_placed_by_its_offset_in_pixels()
    {
        var flipbook = Flipbook(Set(4, 4));
        var frame = Frame(Cell(0), Cell(1, new Int2(0, 4)));

        Assert.True(FlipbookQuads.TryCell(flipbook, frame, 1, Matrix.Identity, false, out var quad, out var tile));

        Assert.Equal(TileId.FromGrid(1, 1, 0), tile.Id);
        AssertCorners(quad, new Vector2(-0.5f, 1), new Vector2(0.5f, 2));
    }

    [Fact]
    public void FlipX_mirrors_around_the_pivot()
    {
        var flipbook = Flipbook(Set(4, 4));
        flipbook.Pivot = Vector2.Zero;
        var frame = Frame(Cell(0), Cell(1, new Int2(4, 0)));

        Assert.True(FlipbookQuads.TryCell(flipbook, frame, 0, Matrix.Identity, true, out var first, out _));
        Assert.True(FlipbookQuads.TryCell(flipbook, frame, 1, Matrix.Identity, true, out var second, out _));

        // The quads keep their corners in their places on screen, the bottom-left one at the left
        AssertCorners(first, new Vector2(-1, 0), new Vector2(0, 1));
        AssertCorners(second, new Vector2(-2, 0), new Vector2(-1, 1));
    }

    [Fact]
    public void The_entitys_world_moves_the_quads()
    {
        var flipbook = Flipbook(Set(4, 4));
        var world = Matrix.Scaling(2) * Matrix.Translation(10, 20, 3);

        Assert.True(FlipbookQuads.TryCell(flipbook, Frame(Cell(0)), 0, world, false, out var quad, out _));

        AssertCorners(quad, new Vector2(9, 20), new Vector2(11, 22), z: 3);
    }

    [Fact]
    public void The_bounds_cover_the_cells_that_can_be_drawn()
    {
        var flipbook = Flipbook(Set(4, 4));
        var world = Matrix.Translation(10, 20, 3);

        var two = FlipbookQuads.Bounds(flipbook, Frame(Cell(0), Cell(1, new Int2(4, 4))), world, false);
        Assert.NotNull(two);
        AssertBox(two.Value, new Vector3(9.5f, 20, 3), new Vector3(11.5f, 22, 3));

        // A cell whose tile the set does not have is left out; a frame of none has no bounds
        var oneLeft = FlipbookQuads.Bounds(flipbook, Frame(Cell(0), Cell(9, new Int2(4, 4))), world, false);
        Assert.NotNull(oneLeft);
        AssertBox(oneLeft.Value, new Vector3(9.5f, 20, 3), new Vector3(10.5f, 21, 3));
        Assert.Null(FlipbookQuads.Bounds(flipbook, Frame(Cell(9)), world, false));
        Assert.Null(FlipbookQuads.Bounds(flipbook, Frame(), world, false));

        var mirrored = FlipbookQuads.Bounds(flipbook, Frame(Cell(0), Cell(1, new Int2(4, 4))), world, true);
        Assert.NotNull(mirrored);
        AssertBox(mirrored.Value, new Vector3(8.5f, 20, 3), new Vector3(10.5f, 22, 3));
    }

    // Review Focus 1
    [Fact]
    public void A_cell_that_cannot_be_drawn_gives_no_quad()
    {
        var flipbook = Flipbook(Set(4, 4));
        flipbook.TileSet!.GetTile(TileId.FromGrid(1, 1, 0))!.Texture = null;
        var frame = Frame(Cell(9), Cell(1), Cell(0));

        // A tile the set does not have, a tile without a texture, a cell past the end
        Assert.False(FlipbookQuads.TryCell(flipbook, frame, 0, Matrix.Identity, false, out _, out _));
        Assert.False(FlipbookQuads.TryCell(flipbook, frame, 1, Matrix.Identity, false, out _, out _));
        Assert.False(FlipbookQuads.TryCell(flipbook, frame, 3, Matrix.Identity, false, out _, out _));
        // The first cell's tile is gone: the set's tile size places the frame
        Assert.True(FlipbookQuads.TryCell(flipbook, frame, 2, Matrix.Identity, false, out var quad, out _));
        AssertCorners(quad, new Vector2(-0.5f, 0), new Vector2(0.5f, 1));

        // A pixels per unit that is not above 0, and no tile set
        flipbook.TileSet.PixelsPerUnit = 0;
        Assert.False(FlipbookQuads.TryCell(flipbook, frame, 2, Matrix.Identity, false, out _, out _));
        Assert.Null(FlipbookQuads.Bounds(flipbook, frame, Matrix.Identity, false));
        flipbook.TileSet = null;
        Assert.False(FlipbookQuads.TryCell(flipbook, frame, 2, Matrix.Identity, false, out _, out _));
        Assert.Null(FlipbookQuads.Bounds(flipbook, frame, Matrix.Identity, false));
    }

    // A set of tiles 1:0,0 to 1:3,0 of the given size in pixels, in a set whose tiles are 4 by 4 at 4 pixels a unit
    private static TileSet Set(int width, int height)
    {
        var set = new TileSet { PixelsPerUnit = 4, TileSize = new Int2(4, 4) };
        for (var column = 0; column < 4; column++)
        {
            var id = TileId.FromGrid(1, column, 0);
            set.Tiles[id] = new Tile { Id = id, Texture = new Texture(), Region = new Rectangle(column * width, 0, width, height) };
        }
        return set;
    }

    private static Flipbook Flipbook(TileSet set) => new() { TileSet = set };

    private static FlipbookFrame Frame(params FlipbookCell[] cells)
    {
        var frame = new FlipbookFrame();
        frame.Cells.AddRange(cells);
        return frame;
    }

    private static FlipbookCell Cell(int column, Int2 offset = default) => new() { Tile = TileId.FromGrid(1, column, 0), Offset = offset };

    private static void AssertCorners(in TileQuad quad, Vector2 bottomLeft, Vector2 topRight, float z = 0)
    {
        AssertNear(new Vector3(bottomLeft, z), quad.BottomLeft);
        AssertNear(new Vector3(topRight.X, bottomLeft.Y, z), quad.BottomRight);
        AssertNear(new Vector3(topRight, z), quad.TopRight);
        AssertNear(new Vector3(bottomLeft.X, topRight.Y, z), quad.TopLeft);
    }

    private static void AssertBox(BoundingBoxExt box, Vector3 minimum, Vector3 maximum)
    {
        AssertNear(minimum, box.Minimum);
        AssertNear(maximum, box.Maximum);
    }

    private static void AssertNear(Vector3 expected, Vector3 actual)
        => Assert.True(Vector3.Distance(expected, actual) < Tolerance, $"Expected {expected}, got {actual}.");
}
