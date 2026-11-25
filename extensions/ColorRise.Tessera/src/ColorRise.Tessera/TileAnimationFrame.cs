using Stride.Core;

namespace ColorRise.Tessera;

/// <summary>
/// One frame of an animated tile: the tile it shows, and for how long.
/// </summary>
[DataContract]
public struct TileAnimationFrame
{
    /// <summary>
    /// The tile the frame shows, of the same tile set.
    /// </summary>
    public TileId Tile;

    /// <summary>
    /// In seconds.
    /// </summary>
    public float Duration;
}
