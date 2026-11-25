using System.Diagnostics.CodeAnalysis;
using Stride.Core.Mathematics;

namespace ColorRise.Tessera.Rendering;

/// <summary>
/// Where the cells of a flipbook's frame are drawn, in the entity's plane z = 0 with y up: the pivot of the first cell on
/// the entity, each cell placed by its offset in pixels and as large as its tile, all mirrored around the pivot by FlipX.
/// </summary>
internal static class FlipbookQuads
{
    /// <summary>
    /// The quad of one cell of a frame in the world, and its tile; false when the set lacks the tile, the tile has no
    /// texture, or the set's pixels per unit is not above 0.
    /// </summary>
    public static bool TryCell(Flipbook flipbook, FlipbookFrame frame, int cell, in Matrix world, bool flipX, out TileQuad quad,
        [MaybeNullWhen(false)] out Tile tile)
    {
        quad = default;
        if (!TryPlace(flipbook, frame, cell, flipX, out var bottomLeft, out var topRight, out tile, out var flip))
        {
            return false;
        }

        quad.BottomLeft = Vector3.TransformCoordinate(new Vector3(bottomLeft, 0), world);
        quad.BottomRight = Vector3.TransformCoordinate(new Vector3(topRight.X, bottomLeft.Y, 0), world);
        quad.TopRight = Vector3.TransformCoordinate(new Vector3(topRight, 0), world);
        quad.TopLeft = Vector3.TransformCoordinate(new Vector3(bottomLeft.X, topRight.Y, 0), world);

        // The region is in the pixels of the texture at full quality, which a streamed texture may not have yet. Mirrored, the
        // corners on the left take the texture coordinates of the right, so that the quad keeps its corners in their places
        Span<Vector2> uvs = stackalloc Vector2[4];
        var fullSize = tile.Texture!.FullQualitySize;
        TileQuads.Uvs(tile.Region, fullSize.Width, fullSize.Height, flip, uvs);
        (quad.UvBottomLeft, quad.UvBottomRight, quad.UvTopRight, quad.UvTopLeft) = flipX
            ? (uvs[1], uvs[0], uvs[3], uvs[2])
            : (uvs[0], uvs[1], uvs[2], uvs[3]);
        return true;
    }

    /// <summary>
    /// The box of the cells of a frame that can be drawn, in the world; null when none can.
    /// </summary>
    public static BoundingBoxExt? Bounds(Flipbook flipbook, FlipbookFrame frame, in Matrix world, bool flipX)
    {
        var minimum = new Vector3(float.MaxValue);
        var maximum = new Vector3(float.MinValue);
        var any = false;
        for (var cell = 0; cell < frame.Cells.Count; cell++)
        {
            if (!TryPlace(flipbook, frame, cell, flipX, out var bottomLeft, out var topRight, out _, out _))
            {
                continue;
            }
            any = true;
            foreach (var corner in (ReadOnlySpan<Vector2>)[bottomLeft, new Vector2(topRight.X, bottomLeft.Y), topRight, new Vector2(bottomLeft.X, topRight.Y)])
            {
                var point = Vector3.TransformCoordinate(new Vector3(corner, 0), world);
                minimum = Vector3.Min(minimum, point);
                maximum = Vector3.Max(maximum, point);
            }
        }
        return any ? new BoundingBoxExt(minimum, maximum) : null;
    }

    // The corners of a cell that can be drawn, in the entity's plane: bottom-left and top-right as they are on screen
    private static bool TryPlace(Flipbook flipbook, FlipbookFrame frame, int cell, bool flipX, out Vector2 bottomLeft, out Vector2 topRight,
        [MaybeNullWhen(false)] out Tile tile, out TileFlip flip)
    {
        (bottomLeft, topRight, tile, flip) = (default, default, null, TileFlip.None);
        if (flipbook.TileSet is not { } set || !(set.PixelsPerUnit > 0) || (uint)cell >= (uint)frame.Cells.Count
            || frame.Cells[cell] is not { } item || set.GetTile(item.Tile) is not { Texture: not null } found)
        {
            return false;
        }

        var unitsPerPixel = 1 / set.PixelsPerUnit;
        // The first cell's tile, or the set's tile size when the set lacks it, places the pivot
        var first = frame.Cells[0] is { } firstCell && set.GetTile(firstCell.Tile) is { } firstTile
            ? new Vector2(firstTile.Region.Width, firstTile.Region.Height)
            : new Vector2(set.TileSize.X, set.TileSize.Y);
        var origin = -flipbook.Pivot * first * unitsPerPixel;

        var left = origin.X + item.Offset.X * unitsPerPixel;
        var bottom = origin.Y + item.Offset.Y * unitsPerPixel;
        var right = left + found.Region.Width * unitsPerPixel;
        var top = bottom + found.Region.Height * unitsPerPixel;
        if (flipX)
        {
            (left, right) = (-right, -left);
        }

        (bottomLeft, topRight, tile, flip) = (new Vector2(left, bottom), new Vector2(right, top), found, item.Flip);
        return true;
    }
}
