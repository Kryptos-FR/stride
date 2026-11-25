using Stride.Core.Mathematics;

namespace ColorRise.Tessera.Tests;

public sealed class TileLayerTests
{
    private static readonly TileId A = TileId.FromGrid(1, 1, 0);
    private static readonly TileId B = TileId.FromGrid(1, 2, 0);

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(15, 15, 0, 0)]
    [InlineData(16, 0, 1, 0)]
    [InlineData(-1, -1, -1, -1)]
    [InlineData(-16, -17, -1, -2)]
    [InlineData(int.MinValue, int.MaxValue, -134217728, 134217727)]
    public void ChunkOf_rounds_down(int x, int y, int cx, int cy)
        => Assert.Equal(new Int2(cx, cy), TileLayer.ChunkOf(new Int2(x, y)));

    [Fact]
    public void A_chunk_gives_its_cells_to_read_and_counts_them()
    {
        var cell = new TileCell(1, A);
        var layer = new TileLayer();
        layer.SetCell(new Int2(17, 2), cell);
        var chunk = layer.GetChunk(new Int2(1, 0))!;
        Assert.Equal(TileChunk.Size * TileChunk.Size, chunk.Cells.Length);
        Assert.Equal(cell, chunk.Cells[2 * TileChunk.Size + 1]);
        Assert.Equal(1, chunk.Count);
        // Read only from outside: a cell changes through TileLayer.SetCell, which keeps Count and the saved content right
        Assert.True(typeof(TileChunk).GetProperty(nameof(TileChunk.Cells))!.PropertyType == typeof(ReadOnlySpan<TileCell>));
    }

    [Fact]
    public void A_cell_set_is_the_cell_got()
    {
        var (_, set, layer) = NewMap();
        foreach (var cell in new[] { new Int2(0, 0), new Int2(-1, -1), new Int2(100, -37), new Int2(int.MaxValue, int.MinValue) })
        {
            layer.SetCell(cell, set, A, TileFlip.Vertical);
            Assert.Equal(new TileCell(1, A, TileFlip.Vertical), layer.GetCell(cell));
        }
        Assert.Equal(4, layer.Chunks.Count);
    }

    [Fact]
    public void A_cell_never_set_is_empty()
    {
        var (_, _, layer) = NewMap();
        Assert.Equal(TileCell.Empty, layer.GetCell(new Int2(5, 5)));
        Assert.Empty(layer.Chunks);
        Assert.Null(layer.GetChunk(Int2.Zero));
    }

    [Fact]
    public void Clearing_the_last_cell_of_a_chunk_removes_the_chunk()
    {
        var (_, set, layer) = NewMap();
        layer.SetCell(new Int2(3, 3), set, A);
        layer.SetCell(new Int2(4, 3), set, A);
        var changes = new List<TileChunkChangedEventArgs>();
        layer.Changed += (_, change) => changes.Add(change);

        layer.ClearCell(new Int2(3, 3));
        Assert.Equal(1, layer.GetChunk(Int2.Zero)!.Count);
        layer.SetCell(new Int2(4, 3), TileCell.Empty);

        Assert.Empty(layer.Chunks);
        Assert.Equal([false, true], changes.Select(change => change.Removed));
        Assert.Equal(Int2.Zero, changes[1].Chunk.Position);
    }

    [Fact]
    public void Changed_is_raised_once_per_real_change()
    {
        var (_, set, layer) = NewMap();
        var count = 0;
        layer.Changed += (_, _) => count++;
        var cell = new Int2(7, -2);

        layer.SetCell(cell, set, A);
        Assert.Equal(1, count);
        layer.SetCell(cell, set, A);
        Assert.Equal(1, count);
        layer.SetCell(cell, set, B);
        Assert.Equal(2, count);
        layer.ClearCell(cell);
        Assert.Equal(3, count);
        layer.ClearCell(cell);
        Assert.Equal(3, count);
    }

    [Fact]
    public void The_index_of_a_cell_in_its_chunk_has_y_up()
    {
        var (_, set, layer) = NewMap();
        layer.SetCell(new Int2(2, 15), set, A);
        Assert.False(layer.GetChunk(Int2.Zero)!.Cells[15 * TileChunk.Size + 2].IsEmpty);

        layer.SetCell(new Int2(-1, -1), set, A);
        var chunk = layer.GetChunk(new Int2(-1, -1))!;
        Assert.False(chunk.Cells[15 * TileChunk.Size + 15].IsEmpty);
        Assert.Equal(new Int2(-1, -1), chunk.Position);
        Assert.Equal(TileChunk.Size * TileChunk.Size, chunk.Cells.Length);
    }

    [Fact]
    public void Setting_a_tile_of_a_tile_set_the_map_does_not_have_throws()
    {
        var (_, _, layer) = NewMap();
        Assert.Throws<ArgumentException>(() => layer.SetCell(Int2.Zero, new TileSet(), A));
    }

    [Fact]
    public void Setting_a_tile_on_a_layer_without_a_map_throws()
        => Assert.Throws<InvalidOperationException>(() => new TileLayer().SetCell(Int2.Zero, new TileSet(), A));

    [Fact]
    public void A_layer_set_by_cell_value_needs_no_map()
    {
        var layer = new TileLayer();
        layer.SetCell(Int2.Zero, new TileCell(4, A));
        Assert.Equal(new TileCell(4, A), layer.GetCell(Int2.Zero));
    }

    // A map with one tile set in slot 1 and one layer "Ground"
    internal static (TileMap Map, TileSet Set, TileLayer Layer) NewMap()
    {
        var map = new TileMap();
        var set = new TileSet();
        set.Tiles[A] = new Tile { Id = A };
        set.Tiles[B] = new Tile { Id = B };
        map.AddTileSet(set);
        return (map, set, map.Layers.Add("Ground"));
    }
}
