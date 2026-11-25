using ColorRise.Tessera.Editor.Pictures;
using ColorRise.Tessera.Editor.TileMapEditor;
using Stride.Core.Mathematics;

namespace ColorRise.Tessera.Editor.FlipbookEditor;

/// <summary>
/// A frame as the game draws it, in pixels.
/// </summary>
/// <param name="Picture">The pixels of all the cells of the frame.</param>
/// <param name="Bounds">Where the picture is around the pivot, x right and y up: its X and Y are the left and the bottom.</param>
/// <param name="HasMissing">Whether a cell names a tile the set does not have, drawn as a marked box.</param>
public sealed record FlipbookFramePicture(TilePicture Picture, Rectangle Bounds, bool HasMissing);

/// <summary>
/// Draws the frames of a flipbook into pictures, as the game places them around the pivot.
/// </summary>
internal static class FlipbookFrameComposer
{
    /// <summary>
    /// The picture of a frame; null for a frame without a cell. The first cell's bottom-left corner is at minus the pivot
    /// times the size of its tile, rounded to the nearest pixel; a cell is at its offset from there, as large as its tile, and
    /// drawn with its flips. A cell whose tile the set lacks is a marked box of the set's tile size.
    /// </summary>
    /// <param name="frame">The frame.</param>
    /// <param name="pivot">The flipbook's pivot, a part of the size of the first cell's tile.</param>
    /// <param name="find">The slot of a tile, or null when the set does not have it.</param>
    /// <param name="tileSize">The size of the tiles of the set, for the boxes of missing tiles.</param>
    public static FlipbookFramePicture? Compose(FlipbookFrame frame, Vector2 pivot, Func<TileId, TileSlot?> find, Int2 tileSize)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(find);
        var missingSize = new Int2(Math.Max(tileSize.X, 1), Math.Max(tileSize.Y, 1));
        var cells = new List<(FlipbookCell Cell, TileSlot? Slot, Rectangle Place)>();
        Int2? origin = null;
        foreach (var cell in frame.Cells)
        {
            if (cell is null)
            {
                continue;
            }
            var slot = find(cell.Tile) is { IsMissing: false } found ? found : null;
            var size = slot is null ? missingSize : new Int2(Math.Max(slot.Region.Width, 1), Math.Max(slot.Region.Height, 1));
            origin ??= new Int2(-(int)MathF.Round(pivot.X * size.X, MidpointRounding.AwayFromZero), -(int)MathF.Round(pivot.Y * size.Y, MidpointRounding.AwayFromZero));
            cells.Add((cell, slot, new Rectangle(origin.Value.X + cell.Offset.X, origin.Value.Y + cell.Offset.Y, size.X, size.Y)));
        }
        if (cells.Count == 0)
        {
            return null;
        }

        var bounds = cells[0].Place;
        foreach (var (_, _, place) in cells)
        {
            bounds = Rectangle.Union(bounds, place);
        }
        var picture = new TilePicture(bounds.Width, bounds.Height);
        var hasMissing = false;
        foreach (var (cell, slot, place) in cells)
        {
            // The picture has y down: its top row is the top of the bounds
            var destination = new Rectangle(place.X - bounds.X, bounds.Bottom - place.Bottom, place.Width, place.Height);
            if (slot is { Picture: { } source })
            {
                TileComposer.Draw(picture, destination, source, slot.Region, cell.Flip, Color.White);
            }
            else
            {
                TileComposer.DrawMissing(picture, destination);
                hasMissing = true;
            }
        }
        picture.Touch();
        return new FlipbookFramePicture(picture, bounds, hasMissing);
    }
}
