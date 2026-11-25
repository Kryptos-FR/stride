using Stride.Core.Mathematics;

namespace ColorRise.Tessera.Editor.Pictures;

/// <summary>
/// A tile of a source: where its pixels are, and where a palette draws it. A tile whose sprite is gone, or whose image
/// cannot be read, has no picture.
/// </summary>
/// <param name="Tile">The id of the tile.</param>
/// <param name="Bounds">Where the palette of its source draws it, in canvas units.</param>
/// <param name="Picture">The picture its pixels are in.</param>
/// <param name="Region">Its pixels in <paramref name="Picture"/>.</param>
/// <param name="SpriteName">The sprite of a sheet source the tile is, or null for a grid source.</param>
public sealed record TileSlot(TileId Tile, Rectangle Bounds, TilePicture? Picture, Rectangle Region, string? SpriteName)
{
    /// <summary>
    /// Whether the tile has no picture to show.
    /// </summary>
    public bool IsMissing => Picture is null;
}
