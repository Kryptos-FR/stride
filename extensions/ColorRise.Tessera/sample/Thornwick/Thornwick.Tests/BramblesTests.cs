using ColorRise.Tessera;
using Stride.Core.Mathematics;
using static Thornwick.Tests.TestMaps;

namespace Thornwick.Tests;

/// <summary>
/// The bushes the hero cuts, read from tile data: the kind "bush", and the property "cut" that names what is left.
/// </summary>
public sealed class BramblesTests
{
    [Fact]
    public void A_cut_bush_becomes_the_tile_its_property_names()
    {
        var map = Room(6, 4);
        map.Layers["Details"].SetCell(new Int2(2, 1), new TileCell(1, Bush, TileFlip.Horizontal));
        var place = Place(map);
        var changes = 0;
        map.Layers["Details"].Changed += (_, _) => changes++;
        Assert.False(place.IsFree(new Int2(2, 1)));

        Assert.True(Brambles.TryCut(map, new Int2(2, 1)));

        Assert.Equal(new TileCell(1, Stump, TileFlip.Horizontal), map.Layers["Details"].GetCell(new Int2(2, 1)));
        Assert.True(place.IsFree(new Int2(2, 1)));
        Assert.Equal(1, changes);
    }

    [Fact]
    public void A_bush_without_the_property_leaves_an_empty_cell()
    {
        var map = Room(6, 4);
        Put(map, Hedge, new Int2(2, 1));

        Assert.True(Brambles.TryCut(map, new Int2(2, 1)));

        Assert.True(map.Layers["Details"].GetCell(new Int2(2, 1)).IsEmpty);
    }

    // Review Focus 5
    [Theory]
    [InlineData("stump")]
    [InlineData("")]
    [InlineData("1:9,9")]
    public void A_property_that_names_no_tile_empties_the_cell(string cut)
    {
        foreach (var value in new object[] { cut, 5 })
        {
            var map = Room(6, 4);
            Tiles(map).Tiles[Bush].Properties["cut"] = value;
            Put(map, Bush, new Int2(2, 1));

            Assert.True(Brambles.TryCut(map, new Int2(2, 1)));

            Assert.True(map.Layers["Details"].GetCell(new Int2(2, 1)).IsEmpty, $"cut = {value}");
        }
    }

    // Review Focus 5
    [Fact]
    public void A_cell_without_a_bush_is_left_alone()
    {
        var map = Room(6, 4);
        Put(map, Wall, new Int2(3, 1));
        var changes = 0;
        foreach (var layer in map.Layers)
        {
            layer.Changed += (_, _) => changes++;
        }
        // A floor, a wall, and a cell outside the frame, where no layer has a tile
        foreach (var cell in new[] { new Int2(1, 1), new Int2(3, 1), new Int2(-1, 7) })
        {
            Assert.False(Brambles.TryCut(map, cell), $"{cell}");
        }
        Assert.Equal(0, changes);
    }

    [Fact]
    public void Only_the_topmost_bush_is_cut()
    {
        var map = Room(6, 4);
        var top = map.Layers.Add("Top");
        Put(map, Bush, new Int2(2, 1));
        top.SetCell(new Int2(2, 1), Tiles(map), Bush);

        Assert.True(Brambles.TryCut(map, new Int2(2, 1)));

        Assert.Equal(Stump, top.GetCell(new Int2(2, 1)).Tile);
        Assert.Equal(Bush, map.Layers["Details"].GetCell(new Int2(2, 1)).Tile);
    }

    [Fact]
    public void The_count_is_the_cells_that_hold_a_bush()
    {
        var map = Room(40, 4);
        Put(map, Bush, new Int2(2, 1), new Int2(30, 2));
        Put(map, Hedge, new Int2(5, 3));

        Assert.Equal(3, Brambles.Count(map));

        Brambles.TryCut(map, new Int2(30, 2));
        Assert.Equal(2, Brambles.Count(map));
        Assert.Equal(0, Brambles.Count(new TileMap()));
    }
}
