using ColorRise.Tessera.Assets;
using Stride.Core.Mathematics;

namespace ColorRise.Tessera.Editor.Pictures;

/// <summary>
/// One source of a tile set as the editors show it. A grid source is its image, each tile in its place. A sheet source is
/// a flow of boxes, one per bound sprite, by number, in rows at most 256 units wide (or as wide as the widest sprite).
/// </summary>
public sealed class TileSourceLayout
{
    private readonly Dictionary<TileId, TileSlot> byTile = [];

    internal TileSourceLayout(TileSetSource source, bool isGrid, Int2 size, TilePicture? image, string? problem, IReadOnlyList<TileSlot> slots, IReadOnlyList<string> unboundSprites)
    {
        Source = source;
        IsGrid = isGrid;
        Size = size;
        Image = image;
        Problem = problem;
        Slots = slots;
        UnboundSprites = unboundSprites;
        foreach (var slot in slots)
        {
            byTile.TryAdd(slot.Tile, slot);
        }
    }

    /// <summary>
    /// The source of the asset.
    /// </summary>
    public TileSetSource Source { get; }

    /// <summary>
    /// Whether the source is an image cut on a grid, rather than the sprites of a sheet.
    /// </summary>
    public bool IsGrid { get; }

    /// <summary>
    /// The size of the layout, in canvas units (one unit is one pixel of an image).
    /// </summary>
    public Int2 Size { get; }

    /// <summary>
    /// The image of a grid source; null for a sheet source, or when the image cannot be read.
    /// </summary>
    public TilePicture? Image { get; }

    /// <summary>
    /// Why the source, or some of its tiles, have no picture, with the name of the file; null when nothing is wrong.
    /// </summary>
    public string? Problem { get; }

    /// <summary>
    /// The tiles, by id; the missing ones of a sheet source included.
    /// </summary>
    public IReadOnlyList<TileSlot> Slots { get; }

    /// <summary>
    /// The sprites of the sheet that no binding names, in the order of the sheet.
    /// </summary>
    public IReadOnlyList<string> UnboundSprites { get; }

    /// <summary>
    /// The tile drawn at a point of the layout, or null.
    /// </summary>
    public TileSlot? SlotAt(Vector2 position)
    {
        foreach (var slot in Slots)
        {
            var bounds = slot.Bounds;
            if (position.X >= bounds.Left && position.X < bounds.Right && position.Y >= bounds.Top && position.Y < bounds.Bottom)
            {
                return slot;
            }
        }
        return null;
    }

    /// <summary>
    /// The slot of a tile, or null when the source has no such tile.
    /// </summary>
    public TileSlot? SlotOf(TileId tile) => byTile.GetValueOrDefault(tile);
}
