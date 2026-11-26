using ColorRise.Tessera;
using Stride.Core.Mathematics;

namespace Thornwick;

/// <summary>
/// Where a villager steps next, and how long it stands between two steps.
/// </summary>
public sealed class WanderPlan
{
    // The sides in the order they are tried, so that a seed always gives the same walk
    private static readonly FlipbookDirection[] Sides = [FlipbookDirection.Down, FlipbookDirection.Left, FlipbookDirection.Right, FlipbookDirection.Up];

    private readonly Random random;

    /// <summary>
    /// A plan that keeps a villager within a radius of its home, its choices drawn from a random source.
    /// </summary>
    public WanderPlan(Int2 home, int radius, Random random)
    {
        Home = home;
        Radius = radius;
        this.random = random;
    }

    /// <summary>
    /// The cell the villager stays near.
    /// </summary>
    public Int2 Home { get; }

    /// <summary>
    /// How far from home the villager goes, in cells, along X and along Y.
    /// </summary>
    public int Radius { get; }

    /// <summary>
    /// The shortest time the villager stands between two steps, in seconds.
    /// </summary>
    public float MinPause { get; set; } = 0.5f;

    /// <summary>
    /// The longest time the villager stands between two steps, in seconds.
    /// </summary>
    public float MaxPause { get; set; } = 2.5f;

    /// <summary>
    /// A side to step to from a cell: one of the free neighbours no further from <see cref="Home"/> than
    /// <see cref="Radius"/> (the larger of the distances along X and Y), each as likely; null when there is none.
    /// </summary>
    public FlipbookDirection? Next(Int2 cell, Func<Int2, bool> isFree)
    {
        var choices = Sides.Where(side => IsNearHome(cell + GridWalker.Offset(side)) && isFree(cell + GridWalker.Offset(side))).ToList();
        return choices.Count == 0 ? null : choices[random.Next(choices.Count)];
    }

    /// <summary>
    /// How long to stand before the next step: between <see cref="MinPause"/> and <see cref="MaxPause"/>;
    /// <see cref="MinPause"/> when <see cref="MaxPause"/> is under it.
    /// </summary>
    public float NextPause()
        => MaxPause > MinPause ? MinPause + random.NextSingle() * (MaxPause - MinPause) : MinPause;

    private bool IsNearHome(Int2 cell) => Math.Max(Math.Abs(cell.X - Home.X), Math.Abs(cell.Y - Home.Y)) <= Radius;
}
