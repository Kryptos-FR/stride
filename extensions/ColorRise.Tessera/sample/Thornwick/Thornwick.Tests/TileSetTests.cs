using ColorRise.Tessera;
using ColorRise.Tessera.Assets;
using Stride.Core.IO;

namespace Thornwick.Tests;

/// <summary>
/// The game's tile sets, made in Game Studio: what the game reads from their tile data is there.
/// </summary>
public sealed class TileSetTests
{
    public static TheoryData<string> TileSets => new(SampleAssets.TileSets);

    [Theory]
    [MemberData(nameof(TileSets))]
    public void A_tile_set_has_nothing_to_tell(string url)
    {
        var asset = SampleAssets.TileSetAsset(url);
        var set = SampleAssets.TileSet(url);

        Assert.Empty(TileAssetChecks.Of(asset));
        Assert.NotEmpty(asset.Sources);
        var folder = new UFile(SampleFiles.Asset(url, TileSetAsset.FileExtension)).GetParent();
        foreach (var source in asset.Sources)
        {
            var grid = Assert.IsType<GridTileSetSource>(source);
            Assert.True(File.Exists(UPath.Combine(folder, grid.Image!).ToOSPath()), $"the image of '{grid.Name}'");
        }
        foreach (var definition in asset.Tiles)
        {
            Assert.True(set.Tiles.ContainsKey(definition.Tile), $"the set has no tile {definition.Tile}, which has data");
            foreach (var frame in definition.Animation)
            {
                Assert.True(set.Tiles.ContainsKey(frame.Tile), $"the set has no tile {frame.Tile}, a frame of {definition.Tile}");
                Assert.True(frame.Duration > 0, $"a frame of {definition.Tile} lasts {frame.Duration} s");
            }
        }
    }

    [Fact]
    public void The_town_has_a_bush_and_what_is_left_of_it()
    {
        var set = SampleAssets.TileSet("Tiles/Town");

        var bushes = set.Tiles.Values.Where(tile => tile.Kind == TileKinds.Bush).ToList();

        Assert.NotEmpty(bushes);
        foreach (var bush in bushes)
        {
            Assert.True(bush.IsSolid, $"the bush {bush.Id} is not solid");
            var cut = Assert.IsType<string>(bush.Properties[TileProperties.Cut]);
            Assert.True(TileId.TryParse(cut, out var id), $"'{cut}' is not a tile id");
            var left = Assert.Contains(id, set.Tiles);
            Assert.False(left.IsSolid, $"{id}, left after a cut, is solid");
            Assert.NotEqual(TileKinds.Bush, left.Kind);
        }
    }

    [Fact]
    public void One_door_leads_to_the_house_and_one_leads_back()
    {
        var doors = SampleAssets.TileSets.SelectMany(url => SampleAssets.TileSet(url).Tiles.Values).Where(tile => tile.Kind == TileKinds.Door).ToList();

        Assert.Equal(2, doors.Count);
        var toHouse = Assert.Single(doors, door => door.Properties.GetValueOrDefault(TileProperties.To) as string == "House");
        Assert.Equal("Maps/House", toHouse.Properties.GetValueOrDefault(TileProperties.Map));
        Assert.Single(doors, door => door.Properties.GetValueOrDefault(TileProperties.To) as string == "Town");
        Assert.All(doors, door => Assert.False(door.IsSolid, $"the door {door.Id} is solid"));
    }

    [Fact]
    public void The_water_plays_and_stops_the_hero()
    {
        var set = SampleAssets.TileSet("Tiles/Town");

        Assert.Contains(set.Tiles.Values, tile => tile.IsSolid && tile.Animation.Count >= 2);
    }
}
