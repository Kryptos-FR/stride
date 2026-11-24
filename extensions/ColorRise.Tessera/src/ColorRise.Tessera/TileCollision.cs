namespace ColorRise.Tessera;

/// <summary>
/// How a tile stops what moves on the map.
/// </summary>
[Flags]
public enum TileCollision
{
    None = 0,
    Solid = 1,
}
