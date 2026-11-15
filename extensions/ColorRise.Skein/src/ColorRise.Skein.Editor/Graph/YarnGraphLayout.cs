namespace ColorRise.Skein.Editor.Graph;

/// <summary>
/// Places the cards of a graph that have no place of their own: in columns from the nodes nothing jumps to, each node
/// one column after the first node that jumps to it.
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
    public const int RowHeight = 200;

    /// <summary>
    /// Gives each of <paramref name="count"/> nodes, named by their index in the order of the text, its place.
    /// </summary>
    /// <param name="links">The links between the nodes, from a node to another.</param>
    public static YarnGraphPoint[] Compute(int count, IReadOnlyCollection<(int Source, int Target)> links)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentNullException.ThrowIfNull(links);

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
        Array.Fill(column, -1);
        var queue = new Queue<int>();

        void Visit(int start)
        {
            column[start] = 0;
            queue.Enqueue(start);
            while (queue.TryDequeue(out var node))
            {
                foreach (var target in targets[node] ?? [])
                {
                    if (column[target] < 0)
                    {
                        column[target] = column[node] + 1;
                        queue.Enqueue(target);
                    }
                }
            }
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

        var rows = new Dictionary<int, int>();
        var places = new YarnGraphPoint[count];
        for (var i = 0; i < count; i++)
        {
            var row = rows.GetValueOrDefault(column[i]);
            rows[column[i]] = row + 1;
            places[i] = new YarnGraphPoint(column[i] * ColumnWidth, row * RowHeight);
        }

        return places;
    }
}
