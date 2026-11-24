using ColorRise.Tessera.Assets;
using ColorRise.Tessera.Tests.Assets;
using Stride.Core.Assets;
using Stride.Core.IO;
using Stride.Core.Mathematics;
using Stride.Core.Serialization;

namespace ColorRise.Tessera.Tests.Graphics;

/// <summary>
/// The pixels of a tile's neighbours in its texture never show at the edges of the tile, whatever the zoom, the sampler and
/// the multisampling.
/// </summary>
[Collection(GraphicsCollection.Name)]
public sealed class BleedingTests
{
    private const int Side = 64;
    private const int MapCells = 8;
    private static readonly Color Red = new(255, 0, 0, 255);
    private static readonly Color Blue = new(0, 0, 255, 255);
    private static readonly float[] Zooms = [2.9f, 7.77f, 13.4f];

    // A tile set compiled from an image of a red tile between two blue tiles, and a map of 8 by 8 red cells
    [Theory]
    [InlineData(TileSampler.Point, 1)]
    [InlineData(TileSampler.Linear, 1)]
    [InlineData(TileSampler.Point, 4)]
    [InlineData(TileSampler.Linear, 4)]
    public void No_neighbour_of_the_atlas_shows_at_any_zoom(TileSampler sampler, int msaa)
    {
        using var build = new AssetBuild();
        var set = new TileSetAsset { Id = AssetId.New(), TileSize = new Int2(16, 16), PixelsPerUnit = 16 };
        set.AddSource<GridTileSetSource>("Strip").Image = new UFile(build.WriteFile("strip.png", TestImages.Png(48, 16, (x, _) => x is >= 16 and < 32 ? Red : Blue)));
        var setItem = build.Add("Tiles/Strip", set);
        build.Add("Maps/Red", RedMap(setItem));
        var result = build.Build();
        Assert.True(result.Succeeded, string.Join("\n", result.Errors));

        // Each zoom, with the view moved by sixteenths of a pixel
        var views = Zooms.SelectMany(unitsHigh => Enumerable.Range(0, 16)
            .Select(step => (Center: new Vector2(MapCells / 2f + step / 16f * unitsHigh / Side), UnitsHigh: unitsHigh))).ToList();
        var frames = GraphicsHarness.Run(new HarnessOptions { Msaa = msaa }, scene =>
        {
            var map = build.ContentFor(scene.Game.Services).Load<TileMap>("Maps/Red");
            scene.Add("map", new TileMapComponent { Map = map, Sampler = sampler });
        }, [.. views.Select(view => (Action<HarnessScene>)(scene => scene.SetView(view.Center, view.UnitsHigh)))]);

        for (var i = 0; i < views.Count; i++)
        {
            var (center, unitsHigh) = views[i];
            var size = unitsHigh / Side;
            var checkedPixels = 0;
            for (var y = 0; y < Side; y++)
            {
                for (var x = 0; x < Side; x++)
                {
                    // The pixel's square in the world, y up; only those one pixel in from the map's edges
                    var left = center.X + (x - Side / 2f) * size;
                    var top = center.Y - (y - Side / 2f) * size;
                    if (left < size || left + 2 * size > MapCells || top - 2 * size < 0 || top + size > MapCells)
                    {
                        continue;
                    }
                    var pixel = frames[i][x, y];
                    Assert.True(pixel.R == 255 && pixel.B == 0, $"The pixel ({x}, {y}) is {pixel} with the view on {center}, {unitsHigh} units high.");
                    checkedPixels++;
                }
            }
            Assert.True(checkedPixels > 0);
        }
    }

    // A map of 8 by 8 cells of the red tile, 1:1,0, from (0, 0)
    private static TileMapAsset RedMap(AssetItem set)
    {
        var map = new TileMap();
        var placeholder = new TileSet();
        map.SetTileSet(1, placeholder);
        var ground = map.Layers.Add("Ground");
        for (var y = 0; y < MapCells; y++)
        {
            for (var x = 0; x < MapCells; x++)
            {
                ground.SetCell(new Int2(x, y), placeholder, TileId.FromGrid(1, 1, 0));
            }
        }
        var asset = TileMapAsset.FromTileMap(map);
        asset.Id = AssetId.New();
        asset.TileSets[0].TileSet = AttachedReferenceManager.CreateProxyObject<TileSet>(set.Id, set.Location);
        return asset;
    }
}
