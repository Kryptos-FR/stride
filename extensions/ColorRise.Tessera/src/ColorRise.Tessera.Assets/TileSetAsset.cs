using System.ComponentModel;
using Stride.Assets.Textures;
using Stride.Core;
using Stride.Core.Assets;
using Stride.Core.Mathematics;

namespace ColorRise.Tessera.Assets;

/// <summary>
/// A tile set as Game Studio keeps it: the images and sprite sheets its tiles come from, and the data of the tiles that have
/// some. A tile that has no data has no definition: the sources alone give the tiles.
/// </summary>
[DataContract("TileSetAsset")]
[AssetDescription(FileExtension)]
[AssetContentType(typeof(TileSet))]
[AssetFormatVersion(FormatName, CurrentVersion)]
[Display(100, "Tile set")]
public sealed class TileSetAsset : Asset
{
    public const string FileExtension = ".sdtileset";

    private const string FormatName = "ColorRise.Tessera";
    private const string CurrentVersion = "1.0.0.0";

    /// <summary>
    /// The size of a tile of the grid sources that do not give their own, in pixels.
    /// </summary>
    /// <userdoc>The size of a tile, in pixels, for the images that do not give their own.</userdoc>
    [DataMember(10)]
    [Display("Tile size")]
    public Int2 TileSize { get; set; } = new(16, 16);

    /// <summary>
    /// How many pixels of a tile make one unit of the world.
    /// </summary>
    /// <userdoc>How many pixels of a tile make one unit of the world. With 16, a tile of 16 pixels is one unit wide.</userdoc>
    [DataMember(20)]
    [Display("Pixels per unit")]
    public float PixelsPerUnit { get; set; } = 16;

    /// <userdoc>The colour that is made transparent when the color key is on.</userdoc>
    [DataMember(30)]
    [Display("Color key color", category: "Texture")]
    public Color ColorKeyColor { get; set; } = new(255, 0, 255);

    /// <userdoc>Makes the pixels of the color key transparent.</userdoc>
    [DataMember(40)]
    [DefaultValue(false)]
    [Display(category: "Texture")]
    public bool ColorKeyEnabled { get; set; }

    /// <userdoc>Compresses the atlas of the tiles. Compression blurs pixel art, so it is off by default.</userdoc>
    [DataMember(50)]
    [DefaultValue(false)]
    [Display("Compress", "Texture")]
    public bool IsCompressed { get; set; }

    /// <userdoc>Reads the images as sRGB when the game is in linear colour space.</userdoc>
    [DataMember(60)]
    [DefaultValue(true)]
    [Display("sRGB sampling", "Texture")]
    public bool UseSRgbSampling { get; set; } = true;

    /// <userdoc>The alpha format of the atlas.</userdoc>
    [DataMember(70)]
    [DefaultValue(AlphaFormat.Auto)]
    [Display(category: "Texture")]
    public AlphaFormat Alpha { get; set; } = AlphaFormat.Auto;

    /// <userdoc>Premultiplies the colours of the atlas by their alpha. The maps blend the tiles either way, by the alpha they have.</userdoc>
    [DataMember(80)]
    [DefaultValue(true)]
    [Display(category: "Texture")]
    public bool PremultiplyAlpha { get; set; } = true;

    /// <summary>
    /// Where the tiles come from. A source's id is the first part of the ids of its tiles.
    /// </summary>
    /// <userdoc>The images and sprite sheets the tiles come from.</userdoc>
    [DataMember(90)]
    public List<TileSetSource> Sources { get; } = [];

    /// <summary>
    /// The data of the tiles that have some, sorted by id.
    /// </summary>
    /// <userdoc>The tiles that have data: collision, a kind, properties or an animation.</userdoc>
    [DataMember(100)]
    public List<TileDefinition> Tiles { get; } = [];

    /// <summary>
    /// The last id given to a source: an id is never given twice, so that a map never takes the tiles of another source.
    /// </summary>
    [DataMember(110)]
    [Display(Browsable = false)]
    public int LastSourceId { get; set; }

    /// <summary>
    /// Adds a source with an id never given before.
    /// </summary>
    /// <exception cref="InvalidOperationException">The ids 1 to 255 are all given.</exception>
    public T AddSource<T>(string name) where T : TileSetSource, new()
    {
        if (LastSourceId >= TileId.MaxSource)
        {
            throw new InvalidOperationException($"The tile set has given all {TileId.MaxSource} ids of sources: make another tile set for more images.");
        }
        var source = new T { Id = ++LastSourceId, Name = name };
        Sources.Add(source);
        return source;
    }

    /// <summary>
    /// The data of a tile, or null when it has none.
    /// </summary>
    public TileDefinition? GetDefinition(TileId tile)
    {
        var index = IndexOf(tile);
        return index >= 0 ? Tiles[index] : null;
    }

    /// <summary>
    /// The data of a tile, made empty in its place in the list when the tile has none.
    /// </summary>
    public TileDefinition GetOrAddDefinition(TileId tile)
    {
        var index = IndexOf(tile);
        if (index >= 0)
        {
            return Tiles[index];
        }
        var definition = new TileDefinition { Tile = tile };
        Tiles.Insert(~index, definition);
        return definition;
    }

    /// <summary>
    /// Removes the definitions that say nothing, so that the file keeps only the tiles that have data.
    /// </summary>
    public void RemoveDefaultDefinitions() => Tiles.RemoveAll(definition => definition.IsDefault);

    // The index of a tile's first definition, or the complement of where it goes. The list is read in full: a file written by
    // hand or merged may not be sorted, and a search by halves would miss a definition out of order
    private int IndexOf(TileId tile)
    {
        var found = Tiles.FindIndex(definition => definition.Tile == tile);
        if (found >= 0)
        {
            return found;
        }
        var (low, high) = (0, Tiles.Count - 1);
        while (low <= high)
        {
            var middle = low + (high - low) / 2;
            var order = Tiles[middle].Tile.CompareTo(tile);
            if (order == 0)
            {
                return middle;
            }
            (low, high) = order < 0 ? (middle + 1, high) : (low, middle - 1);
        }
        return ~low;
    }
}
