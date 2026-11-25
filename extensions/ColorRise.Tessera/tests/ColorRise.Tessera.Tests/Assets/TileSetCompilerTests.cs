using ColorRise.Tessera.Assets;
using Stride.Assets.Entities;
using Stride.Assets.Sprite;
using Stride.Core.Assets;
using Stride.Core.BuildEngine;
using Stride.Core.IO;
using Stride.Core.Mathematics;
using Stride.Core.Serialization;
using Stride.Engine;
using Stride.Graphics;
using Stride.Rendering.Sprites;

namespace ColorRise.Tessera.Tests.Assets;

/// <summary>
/// The build of a game compiles a tile set asset to a <see cref="TileSet"/> at the asset's URL, and its tiles to textures.
/// </summary>
[Collection(BuildCollection.Name)]
public sealed class TileSetCompilerTests
{
    private static readonly Color Red = new(255, 0, 0, 255);
    private static readonly Color Blue = new(0, 0, 255, 255);
    private static readonly Color Green = new(0, 255, 0, 255);

    [Fact]
    public void A_tile_set_compiles_to_its_tiles_with_their_data()
    {
        using var build = new AssetBuild();
        var asset = RedAndBlue(build);
        var wall = asset.GetOrAddDefinition(TileId.FromGrid(1, 1, 0));
        (wall.Collision, wall.Kind) = (TileCollision.Solid, "wall");
        wall.Properties["hp"] = TileProperty.From(3);
        build.Add("Tiles/Town", asset);

        var result = build.Build();

        Assert.True(result.Succeeded, string.Join("\n", result.Errors));
        var set = build.LoadAlone<TileSet>("Tiles/Town");
        Assert.Equal((8f, new Int2(16, 16)), (set.PixelsPerUnit, set.TileSize));
        Assert.Equal(2, set.Tiles.Count);
        var red = set.GetTile(TileId.FromGrid(1, 0, 0))!;
        Assert.NotNull(red.Texture);
        Assert.Equal((16, 16), (red.Region.Width, red.Region.Height));
        var blue = set.GetTile(TileId.FromGrid(1, 1, 0))!;
        Assert.True(blue.IsSolid);
        Assert.Equal("wall", blue.Kind);
        Assert.Equal(3, Assert.IsType<int>(blue.Properties["hp"]));
        Assert.Equal(Red, Middle(build, red));
        Assert.Equal(Blue, Middle(build, blue));
    }

    [Fact]
    public void A_sprite_sheet_gives_tiles_like_a_grid()
    {
        using var build = new AssetBuild();
        var (asset, _) = Props(build, ["grass", "water"], ["grass", "water"]);
        build.Add("Tiles/Props", asset);

        var result = build.Build();

        Assert.True(result.Succeeded, string.Join("\n", result.Errors));
        var set = build.LoadAlone<TileSet>("Tiles/Props");
        Assert.Equal([TileId.FromNumber(2, 1), TileId.FromNumber(2, 2)], set.Tiles.Keys.Order());
        // The sizes of the sprites' regions; the sheet's packing and pixels per unit play no part
        var (grass, water) = (set.GetTile(TileId.FromNumber(2, 1))!, set.GetTile(TileId.FromNumber(2, 2))!);
        Assert.Equal((16, 16), (grass.Region.Width, grass.Region.Height));
        Assert.Equal((16, 8), (water.Region.Width, water.Region.Height));
        Assert.Equal(16, set.PixelsPerUnit);
        Assert.Equal(Red, Middle(build, grass));
        Assert.Equal(Blue, Middle(build, water));
    }

    [Fact]
    public void A_second_build_runs_no_command()
    {
        using var build = new AssetBuild();
        build.Add("Tiles/Town", RedAndBlue(build));
        Assert.Contains(build.Build().Commands, command => command.Url == "Tiles/Town" && command.Status == ResultStatus.Successful);

        var second = build.Build();

        Assert.True(second.Succeeded, string.Join("\n", second.Errors));
        Assert.NotEmpty(second.Commands);
        Assert.All(second.Commands, command => Assert.Equal(ResultStatus.NotTriggeredWasSuccessful, command.Status));
    }

    [Fact]
    public void A_change_of_the_image_builds_the_tile_set_again()
    {
        using var build = new AssetBuild();
        build.Add("Tiles/Town", RedAndBlue(build));
        Assert.True(build.Build().Succeeded);

        build.WriteFile("tiles.png", TestImages.Png(32, 16, (x, _) => x < 16 ? Green : Blue));
        var changed = build.Build();

        Assert.Contains(changed.Commands, command => command.Url == "Tiles/Town" && command.Status == ResultStatus.Successful);
        var set = build.LoadAlone<TileSet>("Tiles/Town");
        Assert.Equal(Green, Middle(build, set.GetTile(TileId.FromGrid(1, 0, 0))!));
    }

    [Fact]
    public void A_change_of_the_sprite_sheet_builds_the_tile_set_again()
    {
        using var build = new AssetBuild();
        var (asset, sheet) = Props(build, ["grass", "water"], ["grass", "water"]);
        build.Add("Tiles/Props", asset);
        Assert.True(build.Build().Succeeded);

        sheet.Sprites.Single(sprite => sprite.Name == "water").TextureRegion = new Rectangle(16, 0, 16, 16);
        var changed = build.Build();

        Assert.Contains(changed.Commands, command => command.Url == "Tiles/Props" && command.Status == ResultStatus.Successful);
        var water = build.LoadAlone<TileSet>("Tiles/Props").GetTile(TileId.FromNumber(2, 2))!;
        Assert.Equal((16, 16), (water.Region.Width, water.Region.Height));
    }

    [Fact]
    public void An_image_that_is_missing_fails_the_build_and_is_named()
    {
        using var build = new AssetBuild();
        var asset = RedAndBlue(build);
        File.Delete(((GridTileSetSource)asset.Sources[0]).Image!);
        build.Add("Tiles/Town", asset);

        var missing = build.Build();

        Assert.False(missing.Succeeded);
        Assert.Contains(missing.Errors, error => error.Contains("tiles.png", StringComparison.Ordinal) && error.Contains("Town", StringComparison.Ordinal));

        // The same for a file that is not an image
        build.WriteFile("tiles.png", "not an image"u8.ToArray());
        var unreadable = build.Build();

        Assert.False(unreadable.Succeeded);
        Assert.Contains(unreadable.Errors, error => error.Contains("tiles.png", StringComparison.Ordinal) && error.Contains("Town", StringComparison.Ordinal));
    }

    [Fact]
    public void A_missing_sprite_and_an_unbound_sprite_are_warnings()
    {
        using var build = new AssetBuild();
        // water is bound and gone, pond is new
        var (asset, _) = Props(build, ["grass", "pond"], ["grass", "water"]);
        build.Add("Tiles/Props", asset);

        var result = build.Build();

        Assert.True(result.Succeeded, string.Join("\n", result.Errors));
        Assert.Contains(result.Warnings, warning => warning.Contains("water", StringComparison.Ordinal));
        Assert.Contains(result.Warnings, warning => warning.Contains("1 sprite", StringComparison.Ordinal) && warning.Contains("not bound", StringComparison.Ordinal)
            && warning.Contains("pond", StringComparison.Ordinal));
    }

    [Fact]
    public void A_sprite_name_used_twice_is_a_warning()
    {
        using var build = new AssetBuild();
        var (asset, _) = Props(build, ["grass", "grass"], ["grass"]);
        build.Add("Tiles/Props", asset);

        var result = build.Build();

        Assert.True(result.Succeeded, string.Join("\n", result.Errors));
        Assert.Contains(result.Warnings, warning => warning.Contains("twice", StringComparison.Ordinal) && warning.Contains("grass", StringComparison.Ordinal));
    }

    [Fact]
    public void A_source_without_a_tile_is_a_warning()
    {
        using var build = new AssetBuild();
        var asset = RedAndBlue(build);
        ((GridTileSetSource)asset.Sources[0]).TileSize = new Int2(64, 64);
        build.Add("Tiles/Town", asset);

        var result = build.Build();

        Assert.True(result.Succeeded, string.Join("\n", result.Errors));
        Assert.Contains(result.Warnings, warning => warning.Contains("Town", StringComparison.Ordinal) && warning.Contains("no tile", StringComparison.Ordinal));
    }

    [Fact]
    public void An_animation_frame_of_a_tile_that_is_gone_is_a_warning()
    {
        using var build = new AssetBuild();
        var asset = RedAndBlue(build);
        var torch = asset.GetOrAddDefinition(TileId.FromGrid(1, 0, 0));
        torch.Animation.Add(new TileAnimationFrame { Tile = TileId.FromGrid(1, 0, 0), Duration = 0.25f });
        torch.Animation.Add(new TileAnimationFrame { Tile = TileId.FromGrid(1, 5, 0), Duration = 0.25f });
        build.Add("Tiles/Town", asset);

        var result = build.Build();

        Assert.True(result.Succeeded, string.Join("\n", result.Errors));
        Assert.Contains(result.Warnings, warning => warning.Contains("1:5,0", StringComparison.Ordinal) && warning.Contains("1 animation frame", StringComparison.Ordinal));
    }

    // As definitions written at the end of the file by hand, or moved by a merge
    [Fact]
    public void Tile_data_in_any_order_is_kept()
    {
        using var build = new AssetBuild();
        var asset = RedAndBlue(build);
        asset.Tiles.Add(new TileDefinition { Tile = TileId.FromGrid(1, 1, 0), Kind = "water", Collision = TileCollision.Solid });
        asset.Tiles.Add(new TileDefinition { Tile = TileId.FromGrid(1, 0, 0), Kind = "lava", Collision = TileCollision.Solid });
        build.Add("Tiles/Town", asset);

        var result = build.Build();

        Assert.True(result.Succeeded, string.Join("\n", result.Errors));
        var set = build.LoadAlone<TileSet>("Tiles/Town");
        Assert.Equal(("lava", true), (set.GetTile(TileId.FromGrid(1, 0, 0))!.Kind, set.GetTile(TileId.FromGrid(1, 0, 0))!.IsSolid));
        Assert.Equal(("water", true), (set.GetTile(TileId.FromGrid(1, 1, 0))!.Kind, set.GetTile(TileId.FromGrid(1, 1, 0))!.IsSolid));
    }

    [Fact]
    public void A_tile_given_data_twice_keeps_the_first_and_is_a_warning()
    {
        using var build = new AssetBuild();
        var asset = RedAndBlue(build);
        asset.Tiles.Add(new TileDefinition { Tile = TileId.FromGrid(1, 1, 0), Kind = "water" });
        asset.Tiles.Add(new TileDefinition { Tile = TileId.FromGrid(1, 1, 0), Kind = "lava" });
        build.Add("Tiles/Town", asset);

        var result = build.Build();

        Assert.True(result.Succeeded, string.Join("\n", result.Errors));
        Assert.Equal("water", build.LoadAlone<TileSet>("Tiles/Town").GetTile(TileId.FromGrid(1, 1, 0))!.Kind);
        Assert.Contains(result.Warnings, warning => warning.Contains("1:1,0", StringComparison.Ordinal) && warning.Contains("twice", StringComparison.Ordinal));
    }

    [Fact]
    public void Data_of_a_tile_the_set_does_not_have_is_a_warning()
    {
        using var build = new AssetBuild();
        var asset = RedAndBlue(build);
        asset.GetOrAddDefinition(TileId.FromGrid(1, 5, 0)).Collision = TileCollision.Solid;
        asset.GetOrAddDefinition(TileId.FromGrid(1, 6, 0));
        build.Add("Tiles/Town", asset);

        var result = build.Build();

        Assert.True(result.Succeeded, string.Join("\n", result.Errors));
        var warning = Assert.Single(result.Warnings, warning => warning.Contains("does not have", StringComparison.Ordinal) && warning.Contains("data", StringComparison.Ordinal));
        Assert.Contains("1:5,0", warning, StringComparison.Ordinal);
        Assert.DoesNotContain("1:6,0", warning, StringComparison.Ordinal);
    }

    [Fact]
    public void A_tile_set_whose_alpha_is_not_premultiplied_says_so()
    {
        using var build = new AssetBuild();
        var asset = RedAndBlue(build);
        asset.PremultiplyAlpha = false;
        build.Add("Tiles/Town", asset);

        var result = build.Build();

        Assert.True(result.Succeeded, string.Join("\n", result.Errors));
        Assert.False(build.LoadAlone<TileSet>("Tiles/Town").PremultipliedAlpha);
    }

    // "tiles.png", 32 by 16, a red tile and a blue tile, in a grid source "Town" of a tile set of 8 pixels per unit
    [Fact]
    public void Two_sources_with_one_id_build_the_first_and_warn()
    {
        using var build = new AssetBuild();
        var red = build.WriteFile("red.png", TestImages.Png(16, 16, (_, _) => Red));
        var blue = build.WriteFile("blue.png", TestImages.Png(16, 16, (_, _) => Blue));
        var asset = new TileSetAsset { Id = AssetId.New(), LastSourceId = 1 };
        asset.Sources.Add(new GridTileSetSource { Id = 1, Name = "Town", Image = new UFile(red) });
        asset.Sources.Add(new GridTileSetSource { Id = 1, Name = "Town copy", Image = new UFile(blue) });
        build.Add("Tiles/Town", asset);

        var result = build.Build();

        Assert.True(result.Succeeded, string.Join("\n", result.Errors));
        Assert.Contains(result.Warnings, warning => warning.Contains("Tiles/Town", StringComparison.Ordinal)
            && warning.Contains("'Town'", StringComparison.Ordinal) && warning.Contains("'Town copy'", StringComparison.Ordinal));
        var set = build.LoadAlone<TileSet>("Tiles/Town");
        Assert.Equal(Red, Middle(build, set.GetTile(TileId.FromGrid(1, 0, 0))!));
    }

    [Fact]
    public void A_pixels_per_unit_of_zero_builds_with_16_and_warns()
    {
        using var build = new AssetBuild();
        var asset = RedAndBlue(build);
        asset.PixelsPerUnit = 0;
        build.Add("Tiles/Town", asset);

        var result = build.Build();

        Assert.True(result.Succeeded, string.Join("\n", result.Errors));
        Assert.Contains(result.Warnings, warning => warning.Contains("PixelsPerUnit", StringComparison.Ordinal));
        Assert.Equal(16, build.LoadAlone<TileSet>("Tiles/Town").PixelsPerUnit);
    }

    private static TileSetAsset RedAndBlue(AssetBuild build)
    {
        var path = build.WriteFile("tiles.png", TestImages.Png(32, 16, (x, _) => x < 16 ? Red : Blue));
        var asset = new TileSetAsset { Id = AssetId.New(), PixelsPerUnit = 8 };
        asset.AddSource<GridTileSetSource>("Town").Image = new UFile(path);
        return asset;
    }

    // A sprite sheet "Sheets/Props" over "props.png", red then blue: the first sprite 16 by 16, the second 16 by 8; a tile
    // set whose source 2, "Props", binds the given names. The sheet packs its sprites and has its own pixels per unit.
    private static (TileSetAsset Asset, SpriteSheetAsset Sheet) Props(AssetBuild build, string[] sprites, string[] bound)
    {
        var path = new UFile(build.WriteFile("props.png", TestImages.Png(32, 16, (x, _) => x < 16 ? Red : Blue)));
        var sheet = new SpriteSheetAsset { Id = AssetId.New() };
        sheet.Packing.Enabled = true;
        sheet.Sprites.Add(new SpriteInfo { Name = sprites[0], Source = path, TextureRegion = new Rectangle(0, 0, 16, 16), PixelsPerUnit = 100 });
        sheet.Sprites.Add(new SpriteInfo { Name = sprites[1], Source = path, TextureRegion = new Rectangle(16, 0, 16, 8), PixelsPerUnit = 100 });
        var item = build.Add("Sheets/Props", sheet, root: false);

        var asset = new TileSetAsset { Id = AssetId.New(), LastSourceId = 1 };
        var source = asset.AddSource<SpriteSheetTileSetSource>("Props");
        source.Sheet = AttachedReferenceManager.CreateProxyObject<SpriteSheet>(item.Id, item.Location);
        TileSlicer.Bind(source, bound);
        return (asset, sheet);
    }

    // A scene whose first entity shows a map of the tile set, and whose second shows a sprite of the tile set's sheet: the
    // build reaches the sheet through the tile set first, which reads only its asset, then through the sprite, which needs
    // its content
    [Fact]
    public void A_sheet_of_a_tile_set_that_a_sprite_of_the_scene_shows_is_compiled()
    {
        using var build = new AssetBuild();
        var (tileSet, sheet) = Props(build, ["grass", "water"], ["grass", "water"]);
        var tileSetItem = build.Add("Tiles/Props", tileSet, root: false);
        var map = TileMapAssetYamlTests.SmallMap();
        map.Id = AssetId.New();
        map.TileSets[0].TileSet = AttachedReferenceManager.CreateProxyObject<TileSet>(tileSetItem.Id, tileSetItem.Location);
        var mapItem = build.Add("Maps/Town", map, root: false);

        var scene = new SceneAsset { Id = AssetId.New() };
        var mapEntity = new Entity("Map") { new TileMapComponent { Map = AttachedReferenceManager.CreateProxyObject<TileMap>(mapItem.Id, mapItem.Location) } };
        var sprite = new SpriteFromSheet { Sheet = AttachedReferenceManager.CreateProxyObject<SpriteSheet>(sheet.Id, "Sheets/Props") };
        var hero = new Entity("Hero") { new SpriteComponent { SpriteProvider = sprite } };
        foreach (var entity in new[] { mapEntity, hero })
        {
            scene.Hierarchy.Parts.Add(new EntityDesign(entity));
            scene.Hierarchy.RootParts.Add(entity);
        }
        build.Add("Scenes/Main", scene);

        var result = build.Build();

        Assert.True(result.Succeeded, string.Join("\n", result.Errors));
        Assert.Contains(result.Commands, command => command.Url == "Sheets/Props");
    }

    // The pixel at the middle of a tile's region, in its texture as the build wrote it
    private static Color Middle(AssetBuild build, Tile tile)
    {
        using var image = build.LoadImage(build.UrlOf(tile.Texture!));
        return image.PixelBuffer[0].GetPixel<Color>(tile.Region.Center.X, tile.Region.Center.Y);
    }
}
