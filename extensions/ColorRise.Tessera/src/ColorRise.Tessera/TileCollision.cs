namespace ColorRise.Tessera;

/// <summary>
/// How a tile stops what moves on the map.
/// </summary>
[Flags]
public enum TileCollision
{
    /// <summary>
    /// Nothing stops on the tile.
    /// </summary>
    None = 0,
    /// <summary>
    /// The tile stops what moves on a layer that has collision.
    /// </summary>
    Solid = 1,
}
