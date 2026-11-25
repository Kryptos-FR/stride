using ColorRise.Tessera.Assets;
using Stride.Core.Assets;
using Stride.Core.BuildEngine;
using Stride.Core.Mathematics;
using Stride.Core.Serialization;

namespace ColorRise.Tessera.Tests.Assets;

/// <summary>
/// The build of a game compiles a tile map asset to a <see cref="TileMap"/> at the asset's URL.
/// </summary>
[Collection(BuildCollection.Name)]
public sealed class TileMapCompilerTests
{
    [Fact]
    public void A_map_asset_compiles_to_its_cells()
    {
        using var build = new AssetBuild();
        var asset = TownWithTileSet();
        build.Add("Maps/Town", asset);

        var result = build.Build();
        Assert.True(result.Succeeded, string.Join("\n", result.Errors));

        // The tile set's own asset comes in a later step: its content is written by the test
        build.Save("Tiles/Town", new TileSet { PixelsPerUnit = 8 });
        var map = build.Load<TileMap>("Maps/Town");
        Assert.Equal(ContentTests.Describe(asset.ToTileMap(_ => new TileSet { PixelsPerUnit = 8 })), ContentTests.Describe(map));
        Assert.Equal(8, map.TileSets[1].PixelsPerUnit);
    }

    [Fact]
    public void A_bad_chunk_fails_the_build_and_is_named()
    {
        using var build = new AssetBuild();
        var asset = TownWithTileSet();
        var chunk = asset.Layers.Single(layer => layer.Name == "Ground").Chunks.Single(c => c.Position == new Int2(-1, 0));
        chunk.Cells = chunk.Cells.Replace("1/1:2,0", "1/1:two,0");
        build.Add("Maps/Town", asset);

        var result = build.Build();

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("Maps/Town", StringComparison.Ordinal) && error.Contains("Ground", StringComparison.Ordinal)
            && error.Contains("(-1, 0)", StringComparison.Ordinal) && error.Contains("line", StringComparison.Ordinal));
    }

    [Fact]
    public void A_cell_of_a_slot_the_map_does_not_have_is_kept()
    {
        using var build = new AssetBuild();
        var asset = TownWithTileSet();
        var chunk = asset.Layers.Single(layer => layer.Name == "Ground").Chunks.Single(c => c.Position == new Int2(0, 0));
        var cells = new TileCell[TileChunk.Size * TileChunk.Size];
        TileChunkText.Decode(chunk.Cells, cells);
        cells[3 * TileChunk.Size + 3] = new TileCell(9, TileId.FromGrid(1, 1, 1));
        chunk.Cells = TileChunkText.Encode(cells);
        build.Add("Maps/Town", asset);

        var result = build.Build();

        Assert.True(result.Succeeded, string.Join("\n", result.Errors));
        Assert.Empty(result.Errors);
        build.Save("Tiles/Town", new TileSet());
        Assert.Equal(new TileCell(9, TileId.FromGrid(1, 1, 1)), build.Load<TileMap>("Maps/Town").Layers["Ground"].GetCell(new Int2(3, 3)));
    }

    [Fact]
    public void A_second_build_runs_no_command()
    {
        using var build = new AssetBuild();
        build.Add("Maps/Town", TownWithTileSet());
        Assert.Contains(build.Build().Commands, command => command.Url == "Maps/Town" && command.Status == ResultStatus.Successful);

        var second = build.Build();

        Assert.True(second.Succeeded, string.Join("\n", second.Errors));
        Assert.NotEmpty(second.Commands);
        Assert.All(second.Commands, command => Assert.Equal(ResultStatus.NotTriggeredWasSuccessful, command.Status));
    }

    [Fact]
    public void A_change_of_a_cell_builds_the_map_again()
    {
        using var build = new AssetBuild();
        var asset = TownWithTileSet();
        build.Add("Maps/Town", asset);
        Assert.True(build.Build().Succeeded);

        var chunk = asset.Layers.Single(layer => layer.Name == "Details").Chunks.Single();
        var cells = new TileCell[TileChunk.Size * TileChunk.Size];
        TileChunkText.Decode(chunk.Cells, cells);
        cells[0] = new TileCell(1, TileId.FromGrid(1, 7, 7));
        chunk.Cells = TileChunkText.Encode(cells);
        var changed = build.Build();

        Assert.Contains(changed.Commands, command => command.Url == "Maps/Town" && command.Status == ResultStatus.Successful);
        build.Save("Tiles/Town", new TileSet());
        Assert.Equal(TileId.FromGrid(1, 7, 7), build.Load<TileMap>("Maps/Town").Layers["Details"].GetCell(Int2.Zero).Tile);
    }

    [Fact]
    public void A_slot_given_twice_builds_with_the_first_and_warns()
    {
        using var build = new AssetBuild();
        var asset = new TileMapAsset { Id = AssetId.New(), LastSlot = 1 };
        asset.TileSets.Add(new TileSetSlot { Slot = 1, TileSet = AttachedReferenceManager.CreateProxyObject<TileSet>(AssetId.New(), "Tiles/A") });
        asset.TileSets.Add(new TileSetSlot { Slot = 1, TileSet = AttachedReferenceManager.CreateProxyObject<TileSet>(AssetId.New(), "Tiles/B") });
        asset.Layers.Add(new TileLayerAsset { Name = "Ground" });
        build.Add("Maps/Town", asset);

        var result = build.Build();

        Assert.True(result.Succeeded, string.Join("\n", result.Errors));
        Assert.Contains(result.Warnings, warning => warning.Contains("Maps/Town", StringComparison.Ordinal)
            && warning.Contains("slot 1", StringComparison.Ordinal) && warning.Contains("twice", StringComparison.Ordinal));
        build.Save("Tiles/A", new TileSet { PixelsPerUnit = 4 });
        build.Save("Tiles/B", new TileSet { PixelsPerUnit = 8 });
        Assert.Equal(4, build.Load<TileMap>("Maps/Town").TileSets[1].PixelsPerUnit);
    }

    // The small map of the YAML tests, whose slot 1 names the tile set at Tiles/Town
    private static TileMapAsset TownWithTileSet()
    {
        var asset = TileMapAssetYamlTests.SmallMap();
        asset.Id = AssetId.New();
        asset.TileSets[0].TileSet = AttachedReferenceManager.CreateProxyObject<TileSet>(AssetId.New(), "Tiles/Town");
        return asset;
    }
}
