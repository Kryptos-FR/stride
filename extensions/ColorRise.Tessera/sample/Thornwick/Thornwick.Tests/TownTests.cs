using ColorRise.Tessera;
using ColorRise.Tessera.Assets;
using Stride.Core.Mathematics;

namespace Thornwick.Tests;

/// <summary>
/// The town, as tools/MakeMaps.cs writes it from tools/maps/Town.txt: what the game needs of it is there.
/// </summary>
public sealed class TownTests
{
    private const string Url = "Maps/Town";

    [Fact]
    public void Every_cell_of_the_town_names_a_tile_that_exists()
    {
        var errors = new List<string>();
        var map = SampleAssets.Map(Url, errors);

        Assert.Empty(errors);
        Assert.Empty(TileAssetChecks.Of(SampleAssets.MapAsset(Url)));
        foreach (var layer in map.Layers)
        {
            foreach (var (cell, value) in Cells(layer))
            {
                Assert.True(map.GetTile(value) is not null, $"the cell {cell} of {layer.Name} names {value.Slot}/{value.Tile}, which is no tile");
            }
        }
    }

    [Fact]
    public void The_town_is_sixty_by_forty_with_its_three_layers()
    {
        var map = SampleAssets.Map(Url);

        Assert.Equal(new Int2(60, 40), map.Size);
        Assert.Equal(Vector2.One, map.CellSize);
        Assert.Equal(["Ground", "Details", "Overhead"], map.Layers.Select(layer => layer.Name));
        var ground = map.Layers["Ground"];
        for (var y = 0; y < 40; y++)
        {
            for (var x = 0; x < 60; x++)
            {
                Assert.False(ground.GetCell(new Int2(x, y)).IsEmpty, $"Ground has no tile at ({x}, {y})");
            }
        }
        foreach (var layer in map.Layers)
        {
            var outside = Cells(layer).Select(pair => pair.Cell).Where(cell => cell.X < 0 || cell.Y < 0 || cell.X >= 60 || cell.Y >= 40);
            Assert.Empty(outside);
        }
    }

    [Fact]
    public void The_town_has_water_that_plays_twelve_brambles_and_one_door()
    {
        var map = SampleAssets.Map(Url);

        var playing = Cells(map.Layers["Ground"]).Count(pair => map.GetTile(pair.Value) is { Animation.Count: > 0 });

        Assert.True(playing >= 12, $"{playing} cells of Ground play");
        Assert.Equal(12, Brambles.Count(map));
        var door = Assert.Single(Doors.Of(map));
        Assert.Equal("House", door.To);
        Assert.Equal("Maps/House", door.Map);
    }

    [Fact]
    public void The_hero_is_not_walled_in_at_the_start()
    {
        var (place, start) = Town();

        var arrival = Doors.Arrival(place, "House");

        Assert.NotNull(arrival);
        Assert.Equal(start, arrival.Value.Cell);
        Assert.Equal(FlipbookDirection.Down, arrival.Value.Facing);
        var reached = Reach.From(place, start).Count;
        Assert.True(reached >= 100, $"the hero reaches {reached} cells");
    }

    [Fact]
    public void The_hero_can_walk_to_a_corner_of_the_town()
    {
        var (place, start) = Town();

        var reached = Reach.From(place, start);

        Int2[] corners = [new(0, 0), new(59, 0), new(0, 39), new(59, 39)];
        Assert.Contains(corners, reached.Contains);
    }

    [Fact]
    public void The_brambles_close_a_part_of_the_town()
    {
        var (place, start) = Town();
        var map = place.Component.Map!;
        var bushes = TileCells(map, tile => tile.Kind == TileKinds.Bush);

        var opened = Reach.From(place, start, throughBrambles: true);
        opened.ExceptWith(Reach.From(place, start));
        opened.ExceptWith(bushes);

        Assert.True(opened.Count >= 10, $"the brambles open {opened.Count} cells");
    }

    [Fact]
    public void The_hero_walks_under_the_overhead_layer()
    {
        var (place, start) = Town();
        var overhead = place.Component.Map!.Layers[Place.OverheadLayer];

        var under = Reach.From(place, start).Count(cell => !overhead.GetCell(cell).IsEmpty);

        Assert.True(under >= 6, $"the hero walks under {under} cells of Overhead");
    }

    [Fact]
    public void The_town_is_drawn_to_a_picture()
    {
        var path = MapPicture.Save(Url);

        Assert.True(File.Exists(path));
        var (pixels, width, height) = SampleAssets.Image(path);
        Assert.Equal((960, 640), (width, height));
        Assert.DoesNotContain(pixels, pixel => pixel.A != 255);
    }

    // The town's place, and where the hero starts: under the door of the house
    private static (Place Place, Int2 Start) Town()
    {
        var map = SampleAssets.Map(Url);
        var place = TestMaps.Place(map, "Town");
        var door = Doors.Of(map).Single(door => door.To == "House");
        return (place, door.Cell + GridWalker.Offset(FlipbookDirection.Down));
    }

    // The cells of a layer that are not empty, with their values
    private static IEnumerable<(Int2 Cell, TileCell Value)> Cells(TileLayer layer)
    {
        foreach (var chunk in layer.Chunks)
        {
            var values = chunk.Cells.ToArray();
            for (var index = 0; index < values.Length; index++)
            {
                if (!values[index].IsEmpty)
                {
                    yield return (chunk.Position * TileChunk.Size + new Int2(index % TileChunk.Size, index / TileChunk.Size), values[index]);
                }
            }
        }
    }

    // The cells of a map that hold, on some layer, a tile that matches
    private static HashSet<Int2> TileCells(TileMap map, Func<Tile, bool> match)
        => [.. map.Layers.SelectMany(Cells).Where(pair => map.GetTile(pair.Value) is { } tile && match(tile)).Select(pair => pair.Cell)];
}
