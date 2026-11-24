using ColorRise.Tessera.Assets;
using ColorRise.Tessera.Tests.Assets;
using Stride.Assets.Sprite;
using Stride.Core.Assets;
using Stride.Core.IO;
using Stride.Core.Mathematics;
using Stride.Core.Serialization;
using Stride.Graphics;

namespace ColorRise.Tessera.Tests.Graphics;

/// <summary>
/// A tile set asset and a map asset, compiled as the build of a game does, then read and drawn by a game.
/// </summary>
[Collection(GraphicsCollection.Name)]
public sealed class EndToEndTests
{
    private static readonly Color R = new(255, 0, 0, 255);
    private static readonly Color G = new(0, 255, 0, 255);
    private static readonly Color B = new(0, 0, 255, 255);
    private static readonly Color Y = new(255, 255, 0, 255);

    [Fact]
    public void A_map_built_from_assets_is_drawn_by_a_game()
    {
        using var build = new AssetBuild();
        var set = FourTiles();
        set.AddSource<GridTileSetSource>("Town").Image = new UFile(build.WriteFile("tiles.png", RgbyImage()));
        var setItem = build.Add("Tiles/Town", set);
        build.Add("Maps/Town", MapOf(setItem, TileId.FromGrid(1, 0, 0), TileId.FromGrid(1, 1, 0), TileId.FromGrid(1, 2, 0), TileId.FromGrid(1, 3, 0)));

        var frame = BuildAndDraw(build);

        frame.Save("end-to-end");
        Assert.Equal([R, G, B, Y], new[] { frame[16, 16], frame[48, 16], frame[16, 48], frame[48, 48] });
    }

    [Fact]
    public void A_tile_set_of_a_sprite_sheet_is_drawn_the_same()
    {
        using var build = new AssetBuild();
        var image = new UFile(build.WriteFile("tiles.png", RgbyImage()));
        var sheet = new SpriteSheetAsset { Id = AssetId.New() };
        string[] names = ["red", "green", "blue", "yellow"];
        for (var i = 0; i < names.Length; i++)
        {
            sheet.Sprites.Add(new SpriteInfo { Name = names[i], Source = image, TextureRegion = new Rectangle(4 * i, 0, 4, 4) });
        }
        var sheetItem = build.Add("Sheets/Town", sheet, root: false);
        var set = FourTiles();
        var source = set.AddSource<SpriteSheetTileSetSource>("Town");
        source.Sheet = AttachedReferenceManager.CreateProxyObject<SpriteSheet>(sheetItem.Id, sheetItem.Location);
        TileSlicer.Bind(source, names);
        var setItem = build.Add("Tiles/Town", set);
        build.Add("Maps/Town", MapOf(setItem, TileId.FromNumber(1, 1), TileId.FromNumber(1, 2), TileId.FromNumber(1, 3), TileId.FromNumber(1, 4)));

        var frame = BuildAndDraw(build);

        Assert.Equal([R, G, B, Y], new[] { frame[16, 16], frame[48, 16], frame[16, 48], frame[48, 48] });
    }

    // Builds the package, then a game reads "Maps/Town" from its database and draws its four cells, each 32 pixels wide
    private static Frame BuildAndDraw(AssetBuild build)
    {
        var result = build.Build();
        Assert.True(result.Succeeded, string.Join("\n", result.Errors));
        Assert.Empty(result.Warnings);

        return GraphicsHarness.Run(new HarnessOptions(), scene =>
        {
            var map = build.ContentFor(scene.Game.Services).Load<TileMap>("Maps/Town");
            scene.Add("map", new TileMapComponent { Map = map });
            scene.SetView(new Vector2(1, 1), 2);
        }, _ => { })[0];
    }

    // A tile set asset of 4-pixel tiles, one unit wide
    private static TileSetAsset FourTiles() => new() { Id = AssetId.New(), TileSize = new Int2(4, 4), PixelsPerUnit = 4 };

    // An image of four tiles of 4 by 4 in a row: red, green, blue, yellow
    private static byte[] RgbyImage() => TestImages.Png(16, 4, (x, _) => new[] { R, G, B, Y }[x / 4]);

    // A map asset of one layer whose top row is the first two tiles and bottom row the last two, with the tile set at slot 1
    private static TileMapAsset MapOf(AssetItem set, params TileId[] tiles)
    {
        var map = new TileMap();
        var placeholder = new TileSet();
        map.SetTileSet(1, placeholder);
        var ground = map.Layers.Add("Ground");
        ground.SetCell(new Int2(0, 1), placeholder, tiles[0]);
        ground.SetCell(new Int2(1, 1), placeholder, tiles[1]);
        ground.SetCell(new Int2(0, 0), placeholder, tiles[2]);
        ground.SetCell(new Int2(1, 0), placeholder, tiles[3]);

        var asset = TileMapAsset.FromTileMap(map);
        asset.Id = AssetId.New();
        asset.TileSets[0].TileSet = AttachedReferenceManager.CreateProxyObject<TileSet>(set.Id, set.Location);
        return asset;
    }
}
