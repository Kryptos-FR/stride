using Stride.Core;
using Stride.Core.Assets;
using Stride.Core.Mathematics;

namespace ColorRise.Tessera.Assets;

/// <summary>
/// A tile map as Game Studio keeps it: its tile sets by slot, and its layers, whose cells are text, one block of 16 lines per
/// chunk, so that a change of a cell is a change of one line.
/// </summary>
[DataContract("TileMapAsset")]
[AssetDescription(FileExtension)]
[AssetContentType(typeof(TileMap))]
[AssetFormatVersion(FormatName, CurrentVersion)]
[Display(110, "Tile map")]
public sealed class TileMapAsset : Asset
{
    /// <summary>
    /// The extension of the files of tile map assets.
    /// </summary>
    public const string FileExtension = ".sdtilemap";

    private const string FormatName = "ColorRise.Tessera";
    private const string CurrentVersion = "1.0.0.0";

    /// <summary>
    /// The size of a cell, in world units.
    /// </summary>
    /// <userdoc>The size of a cell, in world units.</userdoc>
    [DataMember(10)]
    [Display("Cell size")]
    public Vector2 CellSize { get; set; } = Vector2.One;

    /// <summary>
    /// The size of the map's frame, in cells; (0, 0) for a map without a frame.
    /// </summary>
    /// <userdoc>The size of the map's frame, in cells, from the cell (0, 0). Leave it at (0, 0) for a map without a frame.</userdoc>
    [DataMember(20)]
    public Int2 Size { get; set; }

    /// <summary>
    /// The tile sets the cells name by slot.
    /// </summary>
    /// <userdoc>The tile sets of the map. A cell names its tile set by its slot.</userdoc>
    [DataMember(30)]
    [Display("Tile sets")]
    public List<TileSetSlot> TileSets { get; } = [];

    /// <summary>
    /// The layers, the bottom one first.
    /// </summary>
    /// <userdoc>The layers of the map, the bottom one first.</userdoc>
    [DataMember(40)]
    public List<TileLayerAsset> Layers { get; } = [];

    /// <summary>
    /// The last slot given to a tile set: a slot is never given twice, so that a cell never takes the tile set of another.
    /// </summary>
    [DataMember(50)]
    [Display(Browsable = false)]
    public int LastSlot { get; set; }

    /// <summary>
    /// Gives the map a tile set in a slot never given before.
    /// </summary>
    public TileSetSlot AddTileSet(TileSet tileSet)
    {
        var slot = new TileSetSlot { Slot = ++LastSlot, TileSet = tileSet };
        TileSets.Add(slot);
        return slot;
    }

    /// <summary>
    /// Makes the runtime map: decodes the chunks, and takes the tile set of each slot from <paramref name="resolve"/>. A slot
    /// given twice keeps its first tile set.
    /// </summary>
    /// <param name="resolve">The tile set of a slot, or null to leave the slot empty.</param>
    /// <param name="errors">Where each error goes, with the layer and the chunk; the rest of the map still loads. When null,
    /// the first error is thrown.</param>
    /// <exception cref="FormatException">An error, when <paramref name="errors"/> is null.</exception>
    public TileMap ToTileMap(Func<TileSetSlot, TileSet?> resolve, ICollection<string>? errors = null)
    {
        ArgumentNullException.ThrowIfNull(resolve);
        void Report(string error)
        {
            if (errors is null)
            {
                throw new FormatException(error);
            }
            errors.Add(error);
        }

        var map = new TileMap();
        try
        {
            map.CellSize = CellSize;
            map.Size = Size;
        }
        catch (ArgumentOutOfRangeException)
        {
            Report($"The cell size {CellSize} or the size {Size} of the map is out of range: a cell size is above 0, a size is 0 or above.");
        }

        // A slot given twice keeps its first tile set (TileAssetChecks names the others)
        var slots = new HashSet<int>();
        foreach (var slot in TileSets)
        {
            if (slot.Slot is < 1 or > ushort.MaxValue)
            {
                Report($"The slot {slot.Slot} is out of range: a slot is from 1 to {ushort.MaxValue}.");
                continue;
            }
            if (slots.Add(slot.Slot) && resolve(slot) is { } tileSet)
            {
                map.SetTileSet((ushort)slot.Slot, tileSet);
            }
        }

        var cells = new TileCell[TileChunk.Size * TileChunk.Size];
        foreach (var layerAsset in Layers)
        {
            var layer = layerAsset.ToTileLayer();
            map.Layers.Insert(map.Layers.Count, layer);
            var seen = new HashSet<Int2>();
            foreach (var chunk in layerAsset.Chunks)
            {
                var where = $"Layer '{layerAsset.Name}', chunk ({chunk.Position.X}, {chunk.Position.Y})";
                if (chunk.Position.X is < MinChunk or > MaxChunk || chunk.Position.Y is < MinChunk or > MaxChunk)
                {
                    Report($"{where}: the chunk is out of the range of cells.");
                    continue;
                }
                if (!seen.Add(chunk.Position))
                {
                    Report($"{where}: the layer has this chunk twice; the second is left out.");
                    continue;
                }
                if (!TileChunkText.TryDecode(chunk.Cells, cells, out var error))
                {
                    Report($"{where}: {error}");
                    continue;
                }
                for (var i = 0; i < cells.Length; i++)
                {
                    if (!cells[i].IsEmpty)
                    {
                        var cell = new Int2(chunk.Position.X * TileChunk.Size + i % TileChunk.Size, chunk.Position.Y * TileChunk.Size + i / TileChunk.Size);
                        layer.SetCell(cell, cells[i]);
                    }
                }
            }
        }
        return map;
    }

    /// <summary>
    /// The asset of a runtime map, its chunks top to bottom, then left to right.
    /// </summary>
    public static TileMapAsset FromTileMap(TileMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        var asset = new TileMapAsset { CellSize = map.CellSize, Size = map.Size };
        foreach (var (slot, tileSet) in map.TileSets.OrderBy(pair => pair.Key))
        {
            asset.TileSets.Add(new TileSetSlot { Slot = slot, TileSet = tileSet });
            asset.LastSlot = Math.Max(asset.LastSlot, slot);
        }
        foreach (var layer in map.Layers)
        {
            asset.Layers.Add(TileLayerAsset.FromTileLayer(layer));
        }
        return asset;
    }

    // The chunks whose cells all have coordinates that fit in an int
    private const int MinChunk = int.MinValue / TileChunk.Size;
    private const int MaxChunk = int.MaxValue / TileChunk.Size;
}
