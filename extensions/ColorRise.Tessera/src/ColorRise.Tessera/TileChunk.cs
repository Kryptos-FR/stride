using Stride.Core.Mathematics;

namespace ColorRise.Tessera;

/// <summary>
/// A square of 16 by 16 cells of a layer, the unit a layer stores and draws. Only a layer makes one.
/// </summary>
public sealed class TileChunk
{
    public const int Size = 16;

    internal TileChunk(Int2 position) => Position = position;

    /// <summary>
    /// The place of the chunk, in chunks: the chunk (1, 0) holds the cells (16, 0) to (31, 15).
    /// </summary>
    public Int2 Position { get; }

    /// <summary>
    /// The cells, row by row from the bottom: the index of a cell is <c>y * Size + x</c>, with x and y inside the chunk.
    /// </summary>
    /// <remarks>Read them here, and change them through <see cref="TileLayer.SetCell(Int2, TileCell)"/>.</remarks>
    public TileCell[] Cells { get; } = new TileCell[Size * Size];

    /// <summary>
    /// How many cells are not empty.
    /// </summary>
    public int Count { get; private set; }

    /// <summary>
    /// The index in <see cref="Cells"/> of a cell of the layer.
    /// </summary>
    internal static int IndexOf(Int2 cell) => (cell.Y & (Size - 1)) * Size + (cell.X & (Size - 1));

    // Whether the value of the cell changed
    internal bool Set(int index, TileCell value)
    {
        var old = Cells[index];
        if (old == value)
        {
            return false;
        }
        Count += (old.IsEmpty ? 0 : -1) + (value.IsEmpty ? 0 : 1);
        Cells[index] = value;
        return true;
    }

    internal TileChunk Clone()
    {
        var clone = new TileChunk(Position) { Count = Count };
        Cells.CopyTo(clone.Cells, 0);
        return clone;
    }
}
