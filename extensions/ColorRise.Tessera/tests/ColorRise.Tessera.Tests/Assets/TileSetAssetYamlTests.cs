using System.Runtime.CompilerServices;
using System.Text;
using ColorRise.Tessera.Assets;
using Stride.Core.Assets;
using Stride.Core.Assets.Serializers;
using Stride.Core.IO;
using Stride.Core.Mathematics;
using Stride.Core.Serialization;
using Stride.Graphics;

namespace ColorRise.Tessera.Tests.Assets;

public sealed class TileSetAssetYamlTests
{
    private static readonly Guid SmallSetId = new("2b7f6c1e-9d3a-4c58-8e21-5a4f0c7d9b31");
    private static readonly Guid PropsSheetId = new("8c0e4d2a-71b5-4f93-a6d8-0e3b5c9f1a72");

    [Fact]
    public void A_tile_set_saved_loaded_and_saved_again_has_the_same_bytes()
    {
        var yaml = Write(SmallSet());
        Assert.Equal(yaml, Write(Read(yaml)));
    }

    [Fact]
    public void A_small_tile_set_is_the_golden_file()
    {
        var golden = Path.Combine(GoldenFolder(), "small.sdtileset");
        if (Environment.GetEnvironmentVariable("TESSERA_WRITE_GOLDEN") == "1")
        {
            File.WriteAllText(golden, Write(SmallSet()));
        }

        // Ids aside: the golden file reads as SmallSet, and writes back to itself
        var text = File.ReadAllText(golden).ReplaceLineEndings("\n");
        var read = Read(text);
        Assert.Equal(Describe(SmallSet()), Describe(read));
        Assert.Equal(text, Write(read));

        var lines = text.Split('\n').Select(line => line.Trim()).ToList();
        Assert.Contains("Tile: 1:3,4", lines);
        Assert.Contains("Tile: 2:#1", lines);
        // The tile 1:0,0 has a definition that says nothing, which is not written
        Assert.DoesNotContain("1:0,0", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Properties_of_the_five_types_come_back_from_the_file()
    {
        var asset = new TileSetAsset();
        var properties = asset.GetOrAddDefinition(TileId.FromGrid(1, 0, 0)).Properties;
        properties["depth"] = TileProperty.From(3);
        properties["speed"] = TileProperty.From(0.5f);
        properties["deep"] = TileProperty.From(true);
        properties["name"] = TileProperty.From("pond");
        properties["tint"] = TileProperty.From(new Color(1, 2, 3, 4));
        // Strings that read as other types when untyped
        properties["level"] = TileProperty.From("3");
        properties["answer"] = TileProperty.From("true");

        var read = Read(Write(asset)).GetDefinition(TileId.FromGrid(1, 0, 0))!.Properties.ToDictionary(pair => pair.Key, pair => pair.Value.GetValue());

        Assert.Equal(3, Assert.IsType<int>(read["depth"]));
        Assert.Equal(0.5f, Assert.IsType<float>(read["speed"]));
        Assert.True(Assert.IsType<bool>(read["deep"]));
        Assert.Equal("pond", Assert.IsType<string>(read["name"]));
        Assert.Equal(new Color(1, 2, 3, 4), Assert.IsType<Color>(read["tint"]));
        Assert.Equal("3", Assert.IsType<string>(read["level"]));
        Assert.Equal("true", Assert.IsType<string>(read["answer"]));
    }

    [Fact]
    public void A_property_of_another_type_is_refused()
    {
        Assert.Throws<ArgumentException>(() => TileProperty.From(0.5));
        Assert.Throws<ArgumentException>(() => TileProperty.From(3L));
    }

    [Fact]
    public void Sources_get_ids_that_are_never_reused()
    {
        var asset = new TileSetAsset();
        var a = asset.AddSource<GridTileSetSource>("a");
        var b = asset.AddSource<SpriteSheetTileSetSource>("b");
        asset.Sources.Remove(b);
        var c = asset.AddSource<GridTileSetSource>("c");
        Assert.Equal([1, 3], asset.Sources.Select(source => source.Id));
        Assert.Equal(("a", "c"), (a.Name, c.Name));
    }

    [Fact]
    public void A_source_above_255_is_refused()
    {
        var asset = new TileSetAsset { LastSourceId = 255 };
        Assert.Throws<InvalidOperationException>(() => asset.AddSource<GridTileSetSource>("one too many"));
        Assert.Empty(asset.Sources);
        Assert.Equal(255, asset.LastSourceId);
    }

    [Fact]
    public void Definitions_are_kept_sorted_by_id_and_found_by_it()
    {
        var asset = new TileSetAsset();
        foreach (var tile in new[] { TileId.FromGrid(2, 0, 0), TileId.FromGrid(1, 5, 0), TileId.FromNumber(1, 3), TileId.FromGrid(1, 0, 1) })
        {
            asset.GetOrAddDefinition(tile).Kind = tile.ToString();
        }
        Assert.Equal(asset.Tiles.Select(definition => definition.Tile).Order(), asset.Tiles.Select(definition => definition.Tile));
        Assert.Same(asset.GetDefinition(TileId.FromGrid(1, 5, 0)), asset.GetOrAddDefinition(TileId.FromGrid(1, 5, 0)));
        Assert.Equal(4, asset.Tiles.Count);
        Assert.Null(asset.GetDefinition(TileId.FromGrid(1, 4, 0)));
    }

    [Fact]
    public void A_definition_that_says_nothing_is_removed()
    {
        var asset = new TileSetAsset();
        asset.GetOrAddDefinition(TileId.FromGrid(1, 0, 0));
        asset.GetOrAddDefinition(TileId.FromGrid(1, 1, 0)).Collision = TileCollision.Solid;
        asset.GetOrAddDefinition(TileId.FromGrid(1, 2, 0)).Kind = "";
        Assert.True(asset.GetDefinition(TileId.FromGrid(1, 0, 0))!.IsDefault);

        asset.RemoveDefaultDefinitions();

        Assert.Equal([TileId.FromGrid(1, 1, 0)], asset.Tiles.Select(definition => definition.Tile));
    }

    // Two sources, a grid with a margin and a sheet with two bindings; a wall with Solid and a kind, a torch of two frames
    private static TileSetAsset SmallSet()
    {
        var asset = new TileSetAsset { Id = (AssetId)SmallSetId };
        var grid = asset.AddSource<GridTileSetSource>("Town");
        (grid.Image, grid.Margin, grid.Spacing) = (new UFile("town.png"), new Int2(1, 1), new Int2(2, 2));
        var sheet = asset.AddSource<SpriteSheetTileSetSource>("Props");
        sheet.Sheet = AttachedReferenceManager.CreateProxyObject<SpriteSheet>((AssetId)PropsSheetId, "Sheets/Props");
        TileSlicer.Bind(sheet, ["torch", "torch lit"]);

        var wall = asset.GetOrAddDefinition(TileId.FromGrid(1, 3, 4));
        (wall.Collision, wall.Kind) = (TileCollision.Solid, "wall");
        wall.Properties["height"] = TileProperty.From(2);
        var torch = asset.GetOrAddDefinition(TileId.FromNumber(2, 1));
        torch.Animation.Add(new TileAnimationFrame { Tile = TileId.FromNumber(2, 1), Duration = 0.25f });
        torch.Animation.Add(new TileAnimationFrame { Tile = TileId.FromNumber(2, 2), Duration = 0.25f });
        asset.GetOrAddDefinition(TileId.FromGrid(1, 0, 0));
        asset.RemoveDefaultDefinitions();
        return asset;
    }

    // The values of a tile set asset, ids aside
    private static string Describe(TileSetAsset asset)
    {
        var text = new StringBuilder();
        text.Append($"{asset.TileSize} {asset.PixelsPerUnit} last {asset.LastSourceId}\n");
        text.Append($"{asset.ColorKeyColor} {asset.ColorKeyEnabled} {asset.Alpha} {asset.PremultiplyAlpha} {asset.UseSRgbSampling} {asset.IsCompressed}\n");
        foreach (var source in asset.Sources)
        {
            text.Append($"source {source.Id} {source.Name} {source.GetType().Name}\n");
            switch (source)
            {
                case GridTileSetSource grid:
                    text.Append($"  {grid.Image} {grid.TileSize} {grid.Margin} {grid.Spacing}\n");
                    break;
                case SpriteSheetTileSetSource sheet:
                    text.Append($"  {(sheet.Sheet is null ? "none" : AttachedReferenceManager.GetUrl(sheet.Sheet))} last {sheet.LastNumber}\n");
                    foreach (var binding in sheet.Bindings)
                    {
                        text.Append($"  {binding.Number} {binding.SpriteName}\n");
                    }
                    break;
            }
        }
        foreach (var definition in asset.Tiles)
        {
            text.Append($"tile {definition.Tile} {definition.Collision} {definition.Kind}\n");
            foreach (var (name, property) in definition.Properties)
            {
                text.Append($"  {name} = {property.GetValue()} ({property.GetValue().GetType().Name})\n");
            }
            foreach (var frame in definition.Animation)
            {
                text.Append($"  {frame.Tile} {frame.Duration}\n");
            }
        }
        return text.ToString();
    }

    private static string Write(TileSetAsset asset)
    {
        using var stream = new MemoryStream();
        AssetFileSerializer.Save(stream, asset, null);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static TileSetAsset Read(string yaml)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(yaml));
        return (TileSetAsset)AssetFileSerializer.Load<Asset>(stream, new UFile("Town.sdtileset")).Asset;
    }

    private static string GoldenFolder([CallerFilePath] string path = "") => Path.Combine(Path.GetDirectoryName(path)!, "Golden");
}
