using Stride.Core;

namespace ColorRise.Tessera;

/// <summary>
/// One frame of an animated tile: the tile it shows, and for how long.
/// </summary>
[DataContract]
public struct TileAnimationFrame
{
    public TileId Tile;

    /// <summary>
    /// In seconds.
    /// </summary>
    public float Duration;
}
