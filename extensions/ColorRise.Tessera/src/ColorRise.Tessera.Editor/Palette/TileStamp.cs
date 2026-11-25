using System.Diagnostics.CodeAnalysis;
using Stride.Core.Mathematics;

namespace ColorRise.Tessera.Editor.Palette;

/// <summary>
/// A block of cells that a tool paints. Its rows count from the bottom, as cells do.
/// </summary>
public sealed class TileStamp : IEquatable<TileStamp>
{
    private readonly TileCell[] cells;

    /// <summary>
    /// A block of <paramref name="size"/> cells, given row by row from the bottom: <c>cells[y * size.X + x]</c>.
    /// </summary>
    public TileStamp(Int2 size, IReadOnlyList<TileCell> cells)
    {
        ArgumentNullException.ThrowIfNull(cells);
        if (size.X <= 0 || size.Y <= 0 || cells.Count != size.X * size.Y)
        {
            throw new ArgumentException($"A stamp of {size.X} by {size.Y} has {Math.Max(0, size.X) * Math.Max(0, size.Y)} cells, not {cells.Count}.", nameof(cells));
        }
        Size = size;
        this.cells = [.. cells];
    }

    /// <summary>
    /// The number of columns and rows.
    /// </summary>
    public Int2 Size { get; }

    /// <summary>
    /// The cell at a column and a row, from the bottom left.
    /// </summary>
    public TileCell this[int x, int y] => cells[y * Size.X + x];

    /// <summary>
    /// Whether no cell has a tile.
    /// </summary>
    public bool IsEmpty => cells.All(cell => cell.IsEmpty);

    /// <summary>
    /// A stamp of one cell.
    /// </summary>
    public static TileStamp Single(TileCell cell) => new(new Int2(1, 1), [cell]);

    /// <summary>
    /// The stamp mirrored left to right.
    /// </summary>
    public TileStamp FlipX() => Map(Size, (x, y) => (Size.X - 1 - x, y), flip => flip ^ TileFlip.Horizontal);

    /// <summary>
    /// The stamp mirrored top to bottom.
    /// </summary>
    public TileStamp FlipY() => Map(Size, (x, y) => (x, Size.Y - 1 - y), flip => flip ^ TileFlip.Vertical);

    /// <summary>
    /// The stamp turned a quarter clockwise.
    /// </summary>
    public TileStamp RotateClockwise()
        => Map(new Int2(Size.Y, Size.X), (x, y) => (y, Size.X - 1 - x), flip => Turn(flip, !Has(flip, TileFlip.Vertical), Has(flip, TileFlip.Horizontal)));

    /// <summary>
    /// The stamp turned a quarter counter-clockwise.
    /// </summary>
    public TileStamp RotateCounterClockwise()
        => Map(new Int2(Size.Y, Size.X), (x, y) => (Size.Y - 1 - y, x), flip => Turn(flip, Has(flip, TileFlip.Vertical), !Has(flip, TileFlip.Horizontal)));

    /// <inheritdoc/>
    public bool Equals([NotNullWhen(true)] TileStamp? other)
        => other is not null && Size == other.Size && cells.AsSpan().SequenceEqual(other.cells);

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is TileStamp other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Size);
        foreach (var cell in cells)
        {
            hash.Add(cell);
        }
        return hash.ToHashCode();
    }

    private static bool Has(TileFlip flip, TileFlip bit) => (flip & bit) != 0;

    // A quarter turn swaps the diagonal bit and sets the other two
    private static TileFlip Turn(TileFlip flip, bool horizontal, bool vertical)
        => (flip & TileFlip.Diagonal) ^ TileFlip.Diagonal
           | (horizontal ? TileFlip.Horizontal : TileFlip.None)
           | (vertical ? TileFlip.Vertical : TileFlip.None);

    // Each cell (x, y) goes to place(x, y) in a stamp of the new size, its flips changed; an empty cell stays empty
    private TileStamp Map(Int2 size, Func<int, int, (int X, int Y)> place, Func<TileFlip, TileFlip> flip)
    {
        var moved = new TileCell[cells.Length];
        for (var y = 0; y < Size.Y; y++)
        {
            for (var x = 0; x < Size.X; x++)
            {
                var cell = this[x, y];
                var (toX, toY) = place(x, y);
                moved[toY * size.X + toX] = cell.IsEmpty ? cell : new TileCell(cell.Slot, cell.Tile, flip(cell.Flip));
            }
        }
        return new TileStamp(size, moved);
    }
}
