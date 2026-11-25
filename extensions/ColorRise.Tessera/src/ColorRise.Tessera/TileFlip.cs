namespace ColorRise.Tessera;

/// <summary>
/// How a cell turns its tile. The diagonal flip swaps the tile's x and y; it is applied first, then the horizontal and the
/// vertical flips, as in Tiled.
/// </summary>
[Flags]
public enum TileFlip : byte
{
    /// <summary>
    /// The tile as it is.
    /// </summary>
    None = 0,
    /// <summary>
    /// Mirrored left to right.
    /// </summary>
    Horizontal = 1,
    /// <summary>
    /// Mirrored top to bottom.
    /// </summary>
    Vertical = 2,
    /// <summary>
    /// Its x and y swapped, before the other flips.
    /// </summary>
    Diagonal = 4,
}
