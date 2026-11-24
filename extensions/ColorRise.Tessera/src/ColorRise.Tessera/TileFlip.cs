namespace ColorRise.Tessera;

/// <summary>
/// How a cell turns its tile. The diagonal flip swaps the tile's x and y; it is applied first, then the horizontal and the
/// vertical flips, as in Tiled.
/// </summary>
[Flags]
public enum TileFlip : byte
{
    None = 0,
    Horizontal = 1,
    Vertical = 2,
    Diagonal = 4,
}
