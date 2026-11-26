using ColorRise.Tessera;
using ColorRise.Tessera.Assets;
using Stride.Core.Mathematics;

namespace Thornwick.Tests;

/// <summary>
/// The house, painted in the map editor of Game Studio: what the game needs of it is there.
/// </summary>
public sealed class HouseTests
{
    private const string Url = "Maps/House";

    [Fact]
    public void Every_cell_of_the_house_names_a_tile_that_exists()
    {
        var errors = new List<string>();
        var map = SampleAssets.Map(Url, errors);

        Assert.Empty(errors);
        Assert.Empty(TileAssetChecks.Of(SampleAssets.MapAsset(Url)));
        foreach (var layer in map.Layers)
        {
            foreach (var (cell, value) in MapCells.Of(layer))
            {
                Assert.True(map.GetTile(value) is not null, $"the cell {cell} of {layer.Name} names {value.Slot}/{value.Tile}, which is no tile");
            }
        }
    }

    [Fact]
    public void The_house_is_a_room_with_one_door_back_to_the_town()
    {
        var map = SampleAssets.Map(Url);
        var house = TestMaps.Place(map, "House");

        // A view of 26 by 15 cells shows the room whole
        Assert.InRange(map.Size.X, 8, 16);
        Assert.InRange(map.Size.Y, 6, 12);
        Assert.Equal(["Ground", "Details"], map.Layers.Select(layer => layer.Name));
        var ground = map.Layers["Ground"];
        for (var y = 0; y < map.Size.Y; y++)
        {
            for (var x = 0; x < map.Size.X; x++)
            {
                Assert.False(ground.GetCell(new Int2(x, y)).IsEmpty, $"Ground has no tile at ({x}, {y})");
            }
        }
        foreach (var layer in map.Layers)
        {
            Assert.DoesNotContain(MapCells.Of(layer), pair => !house.InFrame(pair.Cell));
        }
        var door = Assert.Single(Doors.Of(map));
        Assert.Equal("Town", door.To);
        var arrival = Doors.Arrival(house, "Town");
        Assert.NotNull(arrival);
        var reached = Reach.From(house, arrival.Value.Cell).Count;
        Assert.True(reached >= 20, $"the hero reaches {reached} cells");
    }

    [Fact]
    public void The_house_is_drawn_to_a_picture()
    {
        var map = SampleAssets.Map(Url);

        var path = MapPicture.Save(Url);

        Assert.True(File.Exists(path));
        var (pixels, width, height) = SampleAssets.Image(path);
        Assert.Equal((map.Size.X * 16, map.Size.Y * 16), (width, height));
        Assert.DoesNotContain(pixels, pixel => pixel.A != 255);
    }
}
