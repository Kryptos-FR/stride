using ColorRise.Tessera.Assets;

namespace ColorRise.Tessera.Tests.Assets;

/// <summary>
/// What a merge or a hand edit can leave in an asset that still loads: the build warns about it, and the editors show it.
/// </summary>
public sealed class TileAssetChecksTests
{
    [Fact]
    public void Two_sources_with_one_id_are_named()
    {
        var asset = new TileSetAsset();
        asset.Sources.Add(new GridTileSetSource { Id = 1, Name = "Town" });
        asset.Sources.Add(new GridTileSetSource { Id = 2, Name = "Cave" });
        asset.Sources.Add(new GridTileSetSource { Id = 2, Name = "Cave copy" });

        var line = Assert.Single(TileAssetChecks.Of(asset));

        Assert.Contains("'Cave'", line, StringComparison.Ordinal);
        Assert.Contains("'Cave copy'", line, StringComparison.Ordinal);
        Assert.Contains("2", line, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-16f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void A_pixels_per_unit_that_is_not_above_zero_is_named(float value)
    {
        var line = Assert.Single(TileAssetChecks.Of(new TileSetAsset { PixelsPerUnit = value }));

        Assert.Contains("PixelsPerUnit", line, StringComparison.Ordinal);
        Assert.Contains("16", line, StringComparison.Ordinal);
    }

    [Fact]
    public void A_tile_set_as_the_editor_makes_it_has_no_line()
    {
        var asset = new TileSetAsset { PixelsPerUnit = 16 };
        asset.AddSource<GridTileSetSource>("Town");
        asset.AddSource<SpriteSheetTileSetSource>("Objects");

        Assert.Empty(TileAssetChecks.Of(asset));
    }

    [Fact]
    public void A_slot_given_twice_is_named()
    {
        var asset = new TileMapAsset();
        asset.TileSets.Add(new TileSetSlot { Slot = 1 });
        asset.TileSets.Add(new TileSetSlot { Slot = 2 });
        asset.TileSets.Add(new TileSetSlot { Slot = 2 });

        var line = Assert.Single(TileAssetChecks.Of(asset));

        Assert.Contains("2", line, StringComparison.Ordinal);
        Assert.Contains("twice", line, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public void A_slot_out_of_range_is_named(int slot)
    {
        var asset = new TileMapAsset();
        asset.TileSets.Add(new TileSetSlot { Slot = slot });

        var line = Assert.Single(TileAssetChecks.Of(asset));

        Assert.Contains(slot.ToString(System.Globalization.CultureInfo.InvariantCulture), line, StringComparison.Ordinal);
        Assert.Contains("65535", line, StringComparison.Ordinal);
    }

    [Fact]
    public void A_map_as_the_editor_makes_it_has_no_line()
    {
        var asset = new TileMapAsset();
        asset.AddTileSet(new TileSet());
        asset.AddTileSet(new TileSet());
        asset.Layers.Add(new TileLayerAsset { Name = "Ground" });

        Assert.Empty(TileAssetChecks.Of(asset));
    }
}
