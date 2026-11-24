using Stride.Core.Mathematics;

namespace ColorRise.Tessera.Tests;

public sealed class TileMapTests
{
    [Fact]
    public void Layers_are_found_by_name_and_by_index()
    {
        var map = new TileMap();
        var ground = map.Layers.Add("Ground");
        var top = map.Layers.Add("Top");

        Assert.Same(ground, map.Layers[0]);
        Assert.Same(top, map.Layers["Top"]);
        Assert.True(map.Layers.TryGet("Ground", out var found));
        Assert.Same(ground, found);
        Assert.False(map.Layers.TryGet("Water", out _));
        Assert.Throws<KeyNotFoundException>(() => map.Layers["Water"]);
        Assert.Equal(1, map.Layers.IndexOf(top));
        Assert.Same(map, top.Map);
    }

    [Fact]
    public void A_layer_inserted_joins_the_map_and_a_layer_removed_leaves_it()
    {
        var map = new TileMap();
        map.Layers.Add("Top");
        var ground = new TileLayer { Name = "Ground" };

        map.Layers.Insert(0, ground);
        Assert.Same(ground, map.Layers[0]);
        Assert.Same(map, ground.Map);

        Assert.True(map.Layers.Remove(ground));
        Assert.Null(ground.Map);
        Assert.Single(map.Layers);
    }

    [Fact]
    public void A_layer_of_another_map_is_refused()
    {
        var layer = new TileMap().Layers.Add("Ground");
        Assert.Throws<ArgumentException>(() => new TileMap().Layers.Insert(0, layer));
    }

    [Fact]
    public void A_tile_set_added_twice_keeps_its_slot()
    {
        var map = new TileMap();
        var a = new TileSet();
        var b = new TileSet();
        Assert.Equal(1, map.AddTileSet(a));
        Assert.Equal(2, map.AddTileSet(b));
        Assert.Equal(1, map.AddTileSet(a));
        Assert.Equal(2, map.GetSlot(b));
        Assert.Equal(0, map.GetSlot(new TileSet()));
    }

    [Fact]
    public void A_tile_set_added_after_a_set_slot_takes_the_next_one()
    {
        var map = new TileMap();
        map.SetTileSet(7, new TileSet());
        Assert.Equal(8, map.AddTileSet(new TileSet()));
        Assert.Throws<ArgumentOutOfRangeException>(() => map.SetTileSet(0, new TileSet()));
    }

    [Fact]
    public void GetTile_is_null_for_what_the_map_does_not_have()
    {
        var (map, set, _) = TileLayerTests.NewMap();
        Assert.Null(map.GetTile(TileCell.Empty));
        Assert.Null(map.GetTile(new TileCell(9, TileId.FromGrid(1, 1, 0))));
        Assert.Null(map.GetTile(new TileCell(1, TileId.FromGrid(1, 9, 9))));
        Assert.Same(set.GetTile(TileId.FromGrid(1, 1, 0)), map.GetTile(new TileCell(1, TileId.FromGrid(1, 1, 0))));
    }

    [Fact]
    public void A_clone_has_its_own_cells_and_the_same_tile_sets()
    {
        var (map, set, layer) = TileLayerTests.NewMap();
        map.CellSize = new Vector2(2, 0.5f);
        map.Size = new Int2(40, 30);
        layer.Opacity = 0.5f;
        layer.SetCell(new Int2(3, 4), set, TileId.FromGrid(1, 1, 0));

        var clone = map.Clone();
        clone.Layers["Ground"].SetCell(new Int2(3, 4), set, TileId.FromGrid(1, 2, 0));

        Assert.Equal(new TileCell(1, TileId.FromGrid(1, 1, 0)), layer.GetCell(new Int2(3, 4)));
        Assert.Equal(new TileCell(1, TileId.FromGrid(1, 2, 0)), clone.Layers[0].GetCell(new Int2(3, 4)));
        Assert.Same(set, clone.TileSets[1]);
        Assert.Same(clone, clone.Layers[0].Map);
        Assert.Equal((new Vector2(2, 0.5f), new Int2(40, 30), 0.5f), (clone.CellSize, clone.Size, clone.Layers[0].Opacity));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-1, 1)]
    [InlineData(float.NaN, 1)]
    [InlineData(float.PositiveInfinity, 1)]
    public void A_cell_size_that_is_not_positive_is_refused(float x, float y)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new TileMap { CellSize = new Vector2(x, y) });

    [Fact]
    public void A_negative_size_is_refused()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new TileMap { Size = new Int2(-1, 4) });
}
