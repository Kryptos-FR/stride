using Stride.Core.Mathematics;
using Stride.Engine;

namespace ColorRise.Tessera.Tests;

public sealed class TileMapComponentTests
{
    private static readonly TileId Rock = TileId.FromGrid(1, 0, 0);
    private static readonly TileId Grass = TileId.FromGrid(1, 1, 0);

    [Fact]
    public void World_and_cells_convert_with_y_up()
    {
        var map = Place(new TileMapComponent { Map = new TileMap() }, new Vector3(10, 20, 0));
        Assert.Equal(new Int2(2, 3), map.WorldToCell(new Vector3(12.5f, 23.5f, 0)));
        Assert.Equal(new Int2(-1, -1), map.WorldToCell(new Vector3(9.99f, 19.99f, 0)));
        Assert.Equal(new Vector3(12, 23, 0), map.CellToWorld(new Int2(2, 3)));
        Assert.Equal(new Vector3(12.5f, 23.5f, 0), map.CellToWorld(new Int2(2, 3), TileAnchor.Center));
    }

    [Fact]
    public void A_cell_size_scales_the_grid()
    {
        var map = Place(new TileMapComponent { Map = new TileMap { CellSize = new Vector2(2, 0.5f) } }, Vector3.Zero);
        Assert.Equal(new Int2(2, 2), map.WorldToCell(new Vector3(5, 1.2f, 0)));
        Assert.Equal(new Vector3(4, 1, 0), map.CellToWorld(new Int2(2, 2)));
    }

    [Fact]
    public void A_turned_and_scaled_entity_converts_back()
    {
        var map = new TileMapComponent { Map = new TileMap() };
        var entity = new Entity { map };
        entity.Transform.Rotation = Quaternion.RotationZ(MathUtil.PiOverTwo);
        entity.Transform.Scale = new Vector3(2);
        entity.Transform.Position = new Vector3(3, -4, 1);
        entity.Transform.UpdateWorldMatrix();

        for (var y = -20; y <= 20; y++)
        {
            for (var x = -20; x <= 20; x++)
            {
                var cell = new Int2(x, y);
                Assert.Equal(cell, map.WorldToCell(map.CellToWorld(cell, TileAnchor.Center)));
            }
        }
    }

    [Fact]
    public void The_z_of_a_layer_is_its_index_times_the_spacing()
    {
        var tileMap = new TileMap();
        tileMap.Layers.Add("Ground");
        tileMap.Layers.Add("Details");
        tileMap.Layers.Add("Overhead");
        var map = new TileMapComponent { Map = tileMap };

        Assert.Equal(0f, map.GetLayerZ("Ground"));
        Assert.Equal(0.1f, map.GetLayerZ("Details"));
        Assert.Equal(0.2f, map.GetLayerZ("Overhead"));
        Assert.Throws<KeyNotFoundException>(() => map.GetLayerZ("Sky"));
    }

    [Fact]
    public void A_solid_tile_on_any_colliding_layer_makes_the_cell_solid()
    {
        var (map, set) = MapWithTiles();
        map.Map!.Layers.Add("Ground").SetCell(new Int2(1, 1), set, Grass);
        map.Map.Layers.Add("Walls").SetCell(new Int2(1, 1), set, Rock);
        map.Map.Layers["Ground"].SetCell(new Int2(2, 1), set, Grass);

        Assert.True(map.IsSolid(new Int2(1, 1)));
        Assert.False(map.IsSolid(new Int2(2, 1)));
        Assert.False(map.IsSolid(new Int2(3, 1)));
    }

    [Fact]
    public void A_layer_that_does_not_collide_is_ignored()
    {
        var (map, set) = MapWithTiles();
        var overhead = map.Map!.Layers.Add("Overhead");
        overhead.Collision = false;
        overhead.SetCell(new Int2(1, 1), set, Rock);
        Assert.False(map.IsSolid(new Int2(1, 1)));
    }

    [Fact]
    public void A_cell_whose_tile_set_or_tile_is_gone_is_not_solid()
    {
        var (map, _) = MapWithTiles();
        var ground = map.Map!.Layers.Add("Ground");
        ground.SetCell(new Int2(0, 0), new TileCell(9, Rock));
        ground.SetCell(new Int2(1, 0), new TileCell(1, TileId.FromGrid(1, 9, 9)));
        Assert.False(map.IsSolid(new Int2(0, 0)));
        Assert.False(map.IsSolid(new Int2(1, 0)));
        Assert.False(map.OverlapsSolid(new RectangleF(-5, -5, 10, 10)));
    }

    [Fact]
    public void Overlap_finds_a_solid_cell_under_a_rectangle()
    {
        var (map, set) = MapWithTiles();
        map.Map!.Layers.Add("Walls").SetCell(new Int2(2, 3), set, Rock);

        Assert.True(map.OverlapsSolid(new RectangleF(2.4f, 3.4f, 0.2f, 0.2f)));
        Assert.True(map.OverlapsSolid(new RectangleF(1.2f, 3.2f, 0.9f, 0.2f)));
        Assert.False(map.OverlapsSolid(new RectangleF(1, 3, 1, 1)));
        Assert.False(map.OverlapsSolid(new RectangleF(0, 0, 1, 1)));
    }

    [Fact]
    public void Overlap_of_a_huge_rectangle_ends()
    {
        var (map, set) = MapWithTiles();
        map.Map!.Layers.Add("Walls").SetCell(new Int2(-1000, 5000), set, Rock);
        Assert.True(map.OverlapsSolid(new RectangleF(-1e9f, -1e9f, 2e9f, 2e9f)));
    }

    [Fact]
    public void Overlap_of_a_rectangle_that_is_not_a_number_is_false()
    {
        var (map, set) = MapWithTiles();
        map.Map!.Layers.Add("Walls").SetCell(new Int2(40, 40), set, Rock);
        Assert.False(map.OverlapsSolid(new RectangleF(float.NaN, 0, 1, 1)));
        Assert.False(map.OverlapsSolid(new RectangleF(0, 0, float.PositiveInfinity, 1)));
    }

    [Fact]
    public void Overlap_on_an_entity_scaled_to_zero_is_false()
    {
        var (map, set) = MapWithTiles();
        map.Map!.Layers.Add("Walls").SetCell(new Int2(40, 40), set, Rock);
        map.Entity.Transform.Scale = Vector3.Zero;
        map.Entity.Transform.UpdateWorldMatrix();
        Assert.False(map.OverlapsSolid(new RectangleF(0, 0, 1, 1)));
    }

    // A map of 512 by 512 cells on four layers has 4096 chunks: a small query reads the chunks under it, not all of them
    [Fact]
    public void A_small_overlap_on_a_big_map_reads_only_the_chunks_under_it()
    {
        var (map, set) = MapWithTiles();
        for (var l = 0; l < 4; l++)
        {
            var layer = map.Map!.Layers.Add($"Layer {l}");
            for (var y = 0; y < 512; y += 16)
            {
                for (var x = 0; x < 512; x += 16)
                {
                    layer.SetCell(new Int2(x, y), set, Grass);
                }
            }
        }

        var time = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < 20000; i++)
        {
            Assert.False(map.OverlapsSolid(new RectangleF(100.2f + i % 7, 200.2f, 0.6f, 0.6f)));
        }
        Assert.True(time.ElapsedMilliseconds < 500, $"20000 small queries took {time.ElapsedMilliseconds} ms.");
    }

    // As a prefab's instances do: the map and its tile sets are content, shared and not copied
    [Fact]
    public void A_cloned_entity_shares_the_map_and_its_tile_sets()
    {
        var (map, set) = MapWithTiles();
        map.Map!.Layers.Add("Walls").SetCell(new Int2(1, 1), set, Rock);

        var clone = map.Entity.Clone().Get<TileMapComponent>();

        Assert.Same(map.Map, clone.Map);
        Assert.Same(set, clone.Map!.TileSets[1]);
    }

    [Fact]
    public void A_component_without_a_map_answers_without_throwing()
    {
        var map = Place(new TileMapComponent(), new Vector3(10, 20, 0));
        Assert.False(map.IsSolid(new Int2(0, 0)));
        Assert.False(map.OverlapsSolid(new RectangleF(0, 0, 10, 10)));
        Assert.Equal(new Int2(0, 0), map.WorldToCell(new Vector3(12.5f, 23.5f, 0)));
    }

    private static TileMapComponent Place(TileMapComponent map, Vector3 position)
    {
        var entity = new Entity { map };
        entity.Transform.Position = position;
        entity.Transform.UpdateWorldMatrix();
        return map;
    }

    // A component at the origin whose map has a tile set in slot 1: Rock is solid, Grass is not
    private static (TileMapComponent Map, TileSet Set) MapWithTiles()
    {
        var set = new TileSet();
        set.Tiles[Rock] = new Tile { Id = Rock, Collision = TileCollision.Solid };
        set.Tiles[Grass] = new Tile { Id = Grass };
        var tileMap = new TileMap();
        tileMap.AddTileSet(set);
        return (Place(new TileMapComponent { Map = tileMap }, Vector3.Zero), set);
    }
}
