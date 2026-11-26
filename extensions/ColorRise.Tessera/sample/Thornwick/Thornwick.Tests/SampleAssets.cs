using System.Collections.Concurrent;
using ColorRise.Tessera;
using ColorRise.Tessera.Assets;
using Stride.Core.Assets;
using Stride.Core.IO;
using Stride.Core.Mathematics;
using Stride.Core.Serialization;
using Stride.Graphics;

namespace Thornwick.Tests;

/// <summary>
/// The game's asset files, read with Tessera's asset classes. Nothing is built.
/// </summary>
internal static class SampleAssets
{
    /// <summary>
    /// The tile sets, as "The art" of the plan names them.
    /// </summary>
    public static readonly string[] TileSets = ["Tiles/Town", "Tiles/Indoors", "Tiles/People"];

    /// <summary>
    /// The flipbooks of the hero and the villagers.
    /// </summary>
    public static readonly string[] Flipbooks = ["Flipbooks/Hero", "Flipbooks/Villager1", "Flipbooks/Villager2", "Flipbooks/Villager3"];

    /// <summary>
    /// The maps of the two places.
    /// </summary>
    public static readonly string[] Maps = ["Maps/Town", "Maps/House"];

    private static readonly ConcurrentDictionary<string, TileSet> RuntimeTileSets = new();

    /// <summary>
    /// The tile set asset of a URL.
    /// </summary>
    public static TileSetAsset TileSetAsset(string url) => Load<TileSetAsset>(url, ColorRise.Tessera.Assets.TileSetAsset.FileExtension);

    /// <summary>
    /// The map asset of a URL.
    /// </summary>
    public static TileMapAsset MapAsset(string url) => Load<TileMapAsset>(url, ColorRise.Tessera.Assets.TileMapAsset.FileExtension);

    /// <summary>
    /// The flipbook asset of a URL.
    /// </summary>
    public static FlipbookAsset FlipbookAsset(string url) => Load<FlipbookAsset>(url, ColorRise.Tessera.Assets.FlipbookAsset.FileExtension);

    /// <summary>
    /// The pixels of an image file, row by row from the top, as red, green, blue and alpha.
    /// </summary>
    public static (Color[] Pixels, int Width, int Height) Image(string path)
    {
        using var stream = File.OpenRead(path);
        using var image = Stride.Graphics.Image.Load(stream);
        var buffer = image.PixelBuffer[0];
        var pixels = buffer.GetPixels<Color>();
        if (buffer.Format is PixelFormat.B8G8R8A8_UNorm or PixelFormat.B8G8R8A8_UNorm_SRgb)
        {
            for (var index = 0; index < pixels.Length; index++)
            {
                var pixel = pixels[index];
                pixels[index] = new Color(pixel.B, pixel.G, pixel.R, pixel.A);
            }
        }
        return (pixels, buffer.Width, buffer.Height);
    }

    /// <summary>
    /// The runtime tile set of a tile set asset, without textures: a tile for each position of each grid source whose pixels
    /// are not all empty, with the collision, the kind, the properties and the animation of its item in Tiles. Made once for
    /// each URL.
    /// </summary>
    /// <exception cref="NotSupportedException">A source is not a grid source.</exception>
    public static TileSet TileSet(string url) => RuntimeTileSets.GetOrAdd(url, MakeTileSet);

    /// <summary>
    /// The runtime map of a map asset, each slot given <see cref="TileSet"/> of the URL of its reference. The errors as
    /// <see cref="TileMapAsset.ToTileMap"/> gives them.
    /// </summary>
    public static TileMap Map(string url, ICollection<string>? errors = null)
        => MapAsset(url).ToTileMap(slot => slot.TileSet is { } reference ? TileSet(UrlOf(reference)) : null, errors);

    /// <summary>
    /// The runtime flipbook of a flipbook asset, its tile set given the same way.
    /// </summary>
    public static Flipbook Flipbook(string url)
    {
        var asset = FlipbookAsset(url);
        var flipbook = asset.ToFlipbook();
        flipbook.TileSet = asset.TileSet is { } reference ? TileSet(UrlOf(reference)) : null;
        return flipbook;
    }

    /// <summary>
    /// The URL of an asset that another asset references.
    /// </summary>
    public static string UrlOf(object reference)
        => AttachedReferenceManager.GetUrl(reference) ?? throw new InvalidDataException("A reference of an asset has no URL.");

    private static T Load<T>(string url, string extension) where T : Asset
    {
        var path = SampleFiles.Asset(url, extension);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"The asset {url}{extension} is not in the game.", path);
        }
        return (T)AssetFileSerializer.Load<Asset>(path).Asset;
    }

    private static TileSet MakeTileSet(string url)
    {
        var asset = TileSetAsset(url);
        var folder = new UFile(SampleFiles.Asset(url, ColorRise.Tessera.Assets.TileSetAsset.FileExtension)).GetParent();
        var colorKey = asset.ColorKeyEnabled ? asset.ColorKeyColor : (Color?)null;
        var set = new TileSet { PixelsPerUnit = asset.PixelsPerUnit, TileSize = asset.TileSize };

        // The first item of a tile is kept, as the build keeps it
        var definitions = new Dictionary<TileId, TileDefinition>();
        foreach (var definition in asset.Tiles)
        {
            definitions.TryAdd(definition.Tile, definition);
        }

        foreach (var source in asset.Sources)
        {
            if (source is not GridTileSetSource { Image: { } file } grid)
            {
                throw new NotSupportedException($"The source '{source.Name}' of {url} is not a grid source with an image.");
            }
            var (pixels, width, height) = Image(UPath.Combine(folder, file).ToOSPath());
            foreach (var slice in TileSlicer.Grid(grid, asset.TileSize, new Int2(width, height)))
            {
                if (TileSlicer.IsEmpty(pixels, width, slice.Region, colorKey))
                {
                    continue;
                }
                var tile = new Tile { Id = slice.Tile, Region = slice.Region };
                if (definitions.TryGetValue(slice.Tile, out var definition))
                {
                    tile.Collision = definition.Collision;
                    tile.Kind = string.IsNullOrEmpty(definition.Kind) ? null : definition.Kind;
                    foreach (var (name, property) in definition.Properties)
                    {
                        tile.Properties[name] = property.GetValue();
                    }
                    tile.Animation.AddRange(definition.Animation);
                }
                set.Tiles[slice.Tile] = tile;
            }
        }
        return set;
    }
}
