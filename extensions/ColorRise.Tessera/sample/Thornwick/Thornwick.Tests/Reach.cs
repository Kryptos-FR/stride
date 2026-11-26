using ColorRise.Tessera;
using Stride.Core.Mathematics;

namespace Thornwick.Tests;

/// <summary>
/// Where a walker can go on a place, one step at a time to a free neighbour.
/// </summary>
internal static class Reach
{
    private static readonly FlipbookDirection[] Sides = [FlipbookDirection.Down, FlipbookDirection.Up, FlipbookDirection.Left, FlipbookDirection.Right];

    /// <summary>
    /// The cells a walker reaches from a cell by steps to free neighbours, the start included. With
    /// <paramref name="throughBrambles"/>, a cell that holds a bush counts as free.
    /// </summary>
    public static HashSet<Int2> From(Place place, Int2 start, bool throughBrambles = false)
        => [.. Walk(place, start, throughBrambles).Keys];

    /// <summary>
    /// A shortest way from a cell to another, as the sides to step to, in order; null when there is none.
    /// </summary>
    public static IReadOnlyList<FlipbookDirection>? Path(Place place, Int2 from, Int2 to, bool throughBrambles = false)
    {
        var steps = Walk(place, from, throughBrambles);
        if (!steps.ContainsKey(to))
        {
            return null;
        }
        var path = new List<FlipbookDirection>();
        for (var cell = to; cell != from;)
        {
            var side = steps[cell]!.Value;
            path.Add(side);
            cell -= GridWalker.Offset(side);
        }
        path.Reverse();
        return path;
    }

    // Each cell reached, with the side of the step that reached it first (none for the start)
    private static Dictionary<Int2, FlipbookDirection?> Walk(Place place, Int2 start, bool throughBrambles)
    {
        var steps = new Dictionary<Int2, FlipbookDirection?> { [start] = null };
        var queue = new Queue<Int2>([start]);
        while (queue.TryDequeue(out var cell))
        {
            foreach (var side in Sides)
            {
                var next = cell + GridWalker.Offset(side);
                if (!steps.ContainsKey(next) && (place.IsFree(next) || (throughBrambles && HasBush(place, next))))
                {
                    steps[next] = side;
                    queue.Enqueue(next);
                }
            }
        }
        return steps;
    }

    private static bool HasBush(Place place, Int2 cell)
        => place.InFrame(cell) && place.Component.Map is { } map
            && map.Layers.Any(layer => map.GetTile(layer.GetCell(cell))?.Kind == TileKinds.Bush);
}
