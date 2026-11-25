using ColorRise.Tessera.Editor.Palette;
using Stride.Core.Mathematics;

namespace ColorRise.Tessera.Editor.TileMapEditor;

/// <summary>
/// The geometry of the tools of the map editor, without a map. Cells count from the bottom left, as in a layer.
/// </summary>
internal static class TilePainting
{
    /// <summary>
    /// The most cells one fill changes, 512 by 512: a larger area would hold the editor for many seconds and make one huge
    /// undo step.
    /// </summary>
    public const int FillLimit = 512 * 512;

    /// <summary>
    /// Every cell of the line between two cells, both ends included, each one step from the one before.
    /// </summary>
    public static IEnumerable<Int2> Line(Int2 from, Int2 to)
    {
        var (dx, dy) = (to.X - from.X, to.Y - from.Y);
        var steps = Math.Max(Math.Abs(dx), Math.Abs(dy));
        if (steps == 0)
        {
            yield return from;
            yield break;
        }
        for (var step = 0; step <= steps; step++)
        {
            yield return new Int2(from.X + Along(dx, step, steps), from.Y + Along(dy, step, steps));
        }
    }

    /// <summary>
    /// The bottom-left cell of a stamp whose middle cell is under the cursor: <c>cursor - (Size - 1) / 2</c>.
    /// </summary>
    public static Int2 StampOrigin(TileStamp stamp, Int2 cursor) => new(cursor.X - (stamp.Size.X - 1) / 2, cursor.Y - (stamp.Size.Y - 1) / 2);

    /// <summary>
    /// Puts in <paramref name="cells"/> the cells of a stamp that have a tile, with its middle cell under the cursor.
    /// </summary>
    public static void Stamp(TileStamp stamp, Int2 cursor, IDictionary<Int2, TileCell> cells)
    {
        var origin = StampOrigin(stamp, cursor);
        for (var y = 0; y < stamp.Size.Y; y++)
        {
            for (var x = 0; x < stamp.Size.X; x++)
            {
                if (!stamp[x, y].IsEmpty)
                {
                    cells[new Int2(origin.X + x, origin.Y + y)] = stamp[x, y];
                }
            }
        }
    }

    /// <summary>
    /// The rectangle between two corner cells, filled with the stamp repeated from its top-left cell; the holes of the stamp
    /// are left out.
    /// </summary>
    public static Dictionary<Int2, TileCell> Rectangle(TileStamp stamp, Int2 from, Int2 to)
    {
        var bounds = Between(from, to);
        var top = bounds.Y + bounds.Height - 1;
        var cells = new Dictionary<Int2, TileCell>();
        for (var y = bounds.Y; y <= top; y++)
        {
            for (var x = bounds.X; x < bounds.X + bounds.Width; x++)
            {
                var cell = stamp[(x - bounds.X) % stamp.Size.X, stamp.Size.Y - 1 - (top - y) % stamp.Size.Y];
                if (!cell.IsEmpty)
                {
                    cells[new Int2(x, y)] = cell;
                }
            }
        }
        return cells;
    }

    /// <summary>
    /// The cells that a fill from <paramref name="start"/> changes: those inside the bounds that touch it through a side, one
    /// after the other, and have its cell, each given the cell of the stamp at <c>(x mod w, y mod h)</c>.
    /// </summary>
    /// <remarks>
    /// The stamp is anchored at the cell zero, so two fills next to each other line up. A hole of the stamp empties its
    /// cells. A start outside the bounds gives nothing, and so do the cells that already have their new value.
    /// </remarks>
    /// <param name="isWall">The cells the fill never enters, whatever they read as, such as those of a chunk that cannot be read.</param>
    /// <returns>The new value of each changed cell; null when the area has more than <paramref name="limit"/> cells.</returns>
    public static Dictionary<Int2, TileCell>? Fill(Func<Int2, TileCell> read, Int2 start, Rectangle bounds, TileStamp stamp, int limit = FillLimit, Func<Int2, bool>? isWall = null)
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(stamp);
        var cells = new Dictionary<Int2, TileCell>();
        if (!Contains(bounds, start) || isWall?.Invoke(start) == true)
        {
            return cells;
        }
        var target = read(start);
        var seen = new HashSet<Int2> { start };
        var next = new Queue<Int2>();
        next.Enqueue(start);
        while (next.TryDequeue(out var cell))
        {
            if (seen.Count > limit)
            {
                return null;
            }
            var value = stamp[Modulo(cell.X, stamp.Size.X), Modulo(cell.Y, stamp.Size.Y)];
            if (value != target)
            {
                cells[cell] = value;
            }
            foreach (var side in (ReadOnlySpan<Int2>)[new(1, 0), new(-1, 0), new(0, 1), new(0, -1)])
            {
                var neighbour = new Int2(cell.X + side.X, cell.Y + side.Y);
                if (Contains(bounds, neighbour) && seen.Add(neighbour) && isWall?.Invoke(neighbour) != true && read(neighbour) == target)
                {
                    next.Enqueue(neighbour);
                }
            }
        }
        return cells;
    }

    /// <summary>
    /// The cells of the rectangle between two corner cells as a stamp; an empty cell is a hole of the stamp.
    /// </summary>
    public static TileStamp Capture(Func<Int2, TileCell> read, Int2 from, Int2 to)
    {
        ArgumentNullException.ThrowIfNull(read);
        var bounds = Between(from, to);
        var cells = new TileCell[bounds.Width * bounds.Height];
        for (var y = 0; y < bounds.Height; y++)
        {
            for (var x = 0; x < bounds.Width; x++)
            {
                cells[y * bounds.Width + x] = read(new Int2(bounds.X + x, bounds.Y + y));
            }
        }
        return new TileStamp(new Int2(bounds.Width, bounds.Height), cells);
    }

    /// <summary>
    /// Where a fill may go: the map's frame when it has one; else the cells of the layer that have a tile, when the start is
    /// among them; else the chunk of the start.
    /// </summary>
    public static Rectangle FillBounds(Int2 mapSize, Rectangle? usedBounds, Int2 start)
    {
        if (mapSize.X > 0 && mapSize.Y > 0)
        {
            return new Rectangle(0, 0, mapSize.X, mapSize.Y);
        }
        if (usedBounds is { } used && Contains(used, start))
        {
            return used;
        }
        var chunk = TileLayer.ChunkOf(start);
        return new Rectangle(chunk.X * TileChunk.Size, chunk.Y * TileChunk.Size, TileChunk.Size, TileChunk.Size);
    }

    /// <summary>
    /// The cells between two corner cells, both included, with Y up: <c>(minX, minY, width, height)</c>.
    /// </summary>
    public static Rectangle Between(Int2 from, Int2 to)
    {
        var (x, y) = (Math.Min(from.X, to.X), Math.Min(from.Y, to.Y));
        return new Rectangle(x, y, Math.Max(from.X, to.X) - x + 1, Math.Max(from.Y, to.Y) - y + 1);
    }

    /// <summary>
    /// Whether a rectangle of cells, with Y up, has a cell.
    /// </summary>
    public static bool Contains(Rectangle bounds, Int2 cell)
        => cell.X >= bounds.X && cell.Y >= bounds.Y && cell.X - bounds.X < bounds.Width && cell.Y - bounds.Y < bounds.Height;

    // How far a line has gone along one axis at a step, to the nearest cell
    private static int Along(int delta, int step, int steps) => (int)Math.Round((double)delta * step / steps, MidpointRounding.AwayFromZero);

    private static int Modulo(int value, int size) => ((value % size) + size) % size;
}
