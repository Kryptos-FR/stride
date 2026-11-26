using ColorRise.Tessera;
using Stride.Core.Mathematics;
using static Thornwick.Tests.TestMaps;

namespace Thornwick.Tests;

/// <summary>
/// Where a villager steps next, and how long it stands: near home, on free cells, the same way for the same seed.
/// </summary>
public sealed class WanderPlanTests
{
    [Fact]
    public void It_steps_to_a_free_neighbour_within_its_radius()
    {
        var plan = new WanderPlan(new Int2(4, 4), 1, new Random(3));

        var sides = Enumerable.Range(0, 200).Select(_ => plan.Next(new Int2(5, 4), _ => true)).ToHashSet();

        // Right would be two cells from home
        Assert.Equal([FlipbookDirection.Down, FlipbookDirection.Left, FlipbookDirection.Up], sides.Select(side => side!.Value).Order());
    }

    [Fact]
    public void It_stays_when_no_neighbour_is_free()
    {
        var plan = new WanderPlan(new Int2(4, 4), 4, new Random(3));

        Assert.Null(plan.Next(new Int2(4, 4), _ => false));
    }

    [Fact]
    public void The_same_seed_gives_the_same_walk()
    {
        var first = new WanderPlan(new Int2(4, 4), 4, new Random(7));
        var second = new WanderPlan(new Int2(4, 4), 4, new Random(7));

        for (var i = 0; i < 50; i++)
        {
            var cell = new Int2(4 + i % 3, 4);
            Assert.Equal(first.Next(cell, _ => true), second.Next(cell, _ => true));
            Assert.Equal(first.NextPause(), second.NextPause());
        }
    }

    [Fact]
    public void It_stands_between_its_two_pauses()
    {
        var plan = new WanderPlan(new Int2(4, 4), 4, new Random(5));

        Assert.All(Enumerable.Range(0, 200).Select(_ => plan.NextPause()), pause => Assert.InRange(pause, 0.5f, 2.5f));

        plan.MinPause = 3;
        plan.MaxPause = 1;
        Assert.All(Enumerable.Range(0, 20).Select(_ => plan.NextPause()), pause => Assert.Equal(3f, pause));
    }

    [Fact]
    public void A_thousand_steps_stay_within_the_radius()
    {
        var place = Place(Room(21, 21));
        var home = new Int2(10, 10);
        var walker = new GridWalker(place, home) { StepSeconds = 0.4f };
        var plan = new WanderPlan(home, 4, new Random(11));
        var moves = 0;

        for (var i = 0; i < 1000; i++)
        {
            if (plan.Next(walker.Cell, place.IsFree) is { } side && walker.TryStep(side))
            {
                moves++;
            }
            walker.Advance(0.4f);
            Assert.True(Math.Max(Math.Abs(walker.Cell.X - home.X), Math.Abs(walker.Cell.Y - home.Y)) <= 4, $"{walker.Cell} after {i} steps");
        }

        Assert.True(moves >= 500, $"{moves} moves");
    }
}
