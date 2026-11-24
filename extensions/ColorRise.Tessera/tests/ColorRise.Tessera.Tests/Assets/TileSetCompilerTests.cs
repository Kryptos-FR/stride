using ColorRise.Tessera.Assets;
using Stride.Assets.Sprite;
using Stride.Core.Assets;
using Stride.Core.BuildEngine;
using Stride.Core.IO;
using Stride.Core.Mathematics;
using Stride.Core.Serialization;
using Stride.Graphics;

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

        build.WriteFile("tiles.png", Png(32, 16, (x, _) => x < 16 ? Green : Blue));
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

    // "tiles.png", 32 by 16, a red tile and a blue tile, in a grid source "Town" of a tile set of 8 pixels per unit
    private static TileSetAsset RedAndBlue(AssetBuild build)
    {
        var path = build.WriteFile("tiles.png", Png(32, 16, (x, _) => x < 16 ? Red : Blue));
        var asset = new TileSetAsset { Id = AssetId.New(), PixelsPerUnit = 8 };
        asset.AddSource<GridTileSetSource>("Town").Image = new UFile(path);
        return asset;
    }

    // A sprite sheet "Sheets/Props" over "props.png", red then blue: the first sprite 16 by 16, the second 16 by 8; a tile
    // set whose source 2, "Props", binds the given names. The sheet packs its sprites and has its own pixels per unit.
    private static (TileSetAsset Asset, SpriteSheetAsset Sheet) Props(AssetBuild build, string[] sprites, string[] bound)
    {
        var path = new UFile(build.WriteFile("props.png", Png(32, 16, (x, _) => x < 16 ? Red : Blue)));
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

    // The pixel at the middle of a tile's region, in its texture as the build wrote it
    private static Color Middle(AssetBuild build, Tile tile)
    {
        using var image = build.LoadImage(build.UrlOf(tile.Texture!));
        return image.PixelBuffer[0].GetPixel<Color>(tile.Region.Center.X, tile.Region.Center.Y);
    }

    private static byte[] Png(int width, int height, Func<int, int, Color> pixel)
    {
        using var image = Image.New2D(width, height, 1, PixelFormat.R8G8B8A8_UNorm);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                image.PixelBuffer[0].SetPixel(x, y, pixel(x, y));
            }
        }
        using var stream = new MemoryStream();
        image.Save(stream, ImageFileType.Png);
        return stream.ToArray();
    }
}
