namespace ColorRise.Skein.Editor.Graph;

/// <summary>
/// Places the cards of a graph that have no place of their own: a node nothing jumps to starts a strand, where each
/// node is one column after the first node that jumps to it, and the strands follow each other in rows. The nodes of
/// a group stay together, one under the other.
/// </summary>
public static class YarnGraphLayout
{
    /// <summary>
    /// The distance between the left sides of two columns.
    /// </summary>
    public const int ColumnWidth = 340;

    /// <summary>
    /// The distance between the tops of two cards of a column.
    /// </summary>
    public const int RowHeight = 240;

    // The width of a laid out graph for a height of one
    private const double Shape = 1.5;

    /// <summary>
    /// Gives each of <paramref name="count"/> nodes, named by their index in the order of the text, its place.
    /// </summary>
    /// <param name="links">The links between the nodes, from a node to another.</param>
    /// <param name="groups">For each node, the number of the group it stays with, or a negative number for none; null when no node has a group.</param>
    public static YarnGraphPoint[] Compute(int count, IReadOnlyCollection<(int Source, int Target)> links, IReadOnlyList<int>? groups = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentNullException.ThrowIfNull(links);
        if (groups is not null && groups.Count != count)
            throw new ArgumentException("There is not one group for each node.", nameof(groups));

        var members = new Dictionary<int, List<int>>();
        for (var i = 0; i < count && groups is not null; i++)
        {
            if (groups[i] >= 0)
            {
                if (!members.TryGetValue(groups[i], out var list))
                    members[groups[i]] = list = [];
                list.Add(i);
            }
        }

        var targets = new List<int>[count];
        var hasSource = new bool[count];
        foreach (var (source, target) in links)
        {
            if (source == target || source < 0 || target < 0 || source >= count || target >= count)
                continue;

            (targets[source] ??= []).Add(target);
            hasSource[target] = true;
        }

        var column = new int[count];
        var row = new int[count];
        var strand = new int[count];
        Array.Fill(column, -1);
        var sizes = new List<(int Width, int Height)>();
        var queue = new Queue<int>();

        // A strand is what a node leads to that no earlier strand has: its own columns, each filled from the top
        void Visit(int start)
        {
            var filled = new List<int>();

            void Put(int first, int at)
            {
                if (at == filled.Count)
                    filled.Add(0);

                // The rest of the node's group comes under it
                IEnumerable<int> together = groups is not null && groups[first] >= 0 ? members[groups[first]].Where(m => m != first).Prepend(first) : [first];
                foreach (var node in together)
                {
                    if (column[node] < 0)
                    {
                        column[node] = at;
                        row[node] = filled[at]++;
                        strand[node] = sizes.Count;
                        queue.Enqueue(node);
                    }
                }
            }

            Put(start, 0);
            while (queue.TryDequeue(out var node))
            {
                foreach (var target in targets[node] ?? [])
                {
                    if (column[target] < 0)
                    {
                        Put(target, column[node] + 1);
                    }
                }
            }

            sizes.Add((filled.Count, filled.Max()));
        }

        for (var i = 0; i < count; i++)
        {
            if (!hasSource[i] && column[i] < 0)
            {
                Visit(i);
            }
        }

        // What is left is a loop no node outside of it leads to: it starts at its first node
        for (var i = 0; i < count; i++)
        {
            if (column[i] < 0)
            {
                Visit(i);
            }
        }

        // The strands fill rows: a script whose nodes are all entry points is a block, not one tall column. Of the
        // widths a row can have, the one that gives the whole the shape nearest to a screen's
        var origins = new (int X, int Y)[sizes.Count];
        var (width, distance) = (0, double.MaxValue);
        for (var limit = sizes.Select(s => s.Width).DefaultIfEmpty(0).Max(); limit <= sizes.Sum(s => s.Width); limit++)
        {
            var (columns, rows) = Pack(limit);
            var shape = columns * ColumnWidth / (double)(rows * RowHeight);
            if (Math.Abs(Math.Log(shape / Shape)) is var away && away < distance)
            {
                (width, distance) = (limit, away);
            }
        }

        Pack(width);

        (int Columns, int Rows) Pack(int limit)
        {
            var (x, y, height, columns) = (0, 0, 0, 0);
            for (var i = 0; i < sizes.Count; i++)
            {
                if (x > 0 && x + sizes[i].Width > limit)
                {
                    (x, y, height) = (0, y + height, 0);
                }

                origins[i] = (x, y);
                x += sizes[i].Width;
                height = Math.Max(height, sizes[i].Height);
                columns = Math.Max(columns, x);
            }

            return (columns, y + height);
        }

        var places = new YarnGraphPoint[count];
        for (var i = 0; i < count; i++)
        {
            var origin = origins[strand[i]];
            places[i] = new YarnGraphPoint((origin.X + column[i]) * ColumnWidth, (origin.Y + row[i]) * RowHeight);
        }

        return places;
    }
}
