using ColorRise.Tessera;
using Stride.Core.Mathematics;
using static Thornwick.Tests.TestMaps;

namespace Thornwick.Tests;

/// <summary>
/// A walker on the grid: it stands on a cell or walks to a neighbour, holds the cells it uses, and never passes a cell.
/// </summary>
public sealed class GridWalkerTests
{
    [Fact]
    public void A_new_walker_holds_its_cell()
    {
        var place = Place(Room(20, 3));

        var walker = new GridWalker(place, new Int2(0, 1));

        Assert.False(place.IsFree(new Int2(0, 1)));
        Assert.Equal((new Int2(0, 1), new Int2(0, 1), FlipbookDirection.Down, false), (walker.Cell, walker.Target, walker.Facing, walker.IsMoving));
        Assert.Throws<InvalidOperationException>(() => new GridWalker(place, new Int2(0, 1)));
    }

    [Fact]
    public void A_step_to_a_free_cell_starts_and_holds_both_cells()
    {
        var place = Place(Room(20, 3));
        var walker = new GridWalker(place, new Int2(0, 1));

        Assert.True(walker.TryStep(FlipbookDirection.Right));

        Assert.True(walker.IsMoving);
        Assert.Equal((new Int2(0, 1), new Int2(1, 1), FlipbookDirection.Right), (walker.Cell, walker.Target, walker.Facing));
        Assert.False(place.IsFree(new Int2(0, 1)));
        Assert.False(place.IsFree(new Int2(1, 1)));
        Assert.Equal(new Int2(1, 1), walker.Ahead);
    }

    [Fact]
    public void A_step_against_a_wall_only_turns()
    {
        var map = Room(20, 3);
        Put(map, Wall, new Int2(1, 1));
        var walker = new GridWalker(Place(map), new Int2(0, 1));

        Assert.False(walker.TryStep(FlipbookDirection.Right));

        Assert.Equal(FlipbookDirection.Right, walker.Facing);
        Assert.False(walker.IsMoving);
        Assert.Equal(new Int2(1, 1), walker.Ahead);
    }

    [Fact]
    public void A_step_ends_on_the_next_cell_after_its_time()
    {
        var place = Place(Room(20, 3));
        var walker = new GridWalker(place, new Int2(0, 1)) { StepSeconds = 0.2f };
        walker.TryStep(FlipbookDirection.Right);

        Assert.False(walker.Advance(0.1f));
        Assert.Equal(new Vector2(1, 1), walker.Position);

        Assert.True(walker.Advance(0.1f));
        Assert.Equal((new Int2(1, 1), false), (walker.Cell, walker.IsMoving));
        Assert.Equal(new Vector2(1.5f, 1), walker.Position);
        Assert.True(place.IsFree(new Int2(0, 1)));
    }

    // Review Focus 2: what a walker does at 60, 30 and 14 frames a second
    [Theory]
    [InlineData(1 / 60f)]
    [InlineData(1 / 30f)]
    [InlineData(0.07f)]
    public void A_walker_that_keeps_walking_loses_no_time_between_cells(float frame)
    {
        var walker = new GridWalker(Place(Room(20, 3)), new Int2(0, 1)) { StepSeconds = 0.2f };
        walker.TryStep(FlipbookDirection.Right);

        for (var i = 0; i < (int)MathF.Round(2.05f / frame); i++)
        {
            walker.Advance(frame);
            if (!walker.IsMoving)
            {
                walker.TryStep(FlipbookDirection.Right);
            }
        }

        // Ten cells in two seconds, at 0.2 s a cell
        Assert.Equal(10, walker.Cell.X);
    }

    // Review Focus 2
    [Fact]
    public void A_long_frame_arrives_on_one_cell()
    {
        var walker = new GridWalker(Place(Room(20, 3)), new Int2(0, 1)) { StepSeconds = 0.2f };
        walker.TryStep(FlipbookDirection.Right);

        Assert.True(walker.Advance(5));
        Assert.Equal(1, walker.Cell.X);

        // One step's time was kept
        walker.TryStep(FlipbookDirection.Right);
        Assert.True(walker.Advance(0));
        Assert.Equal(2, walker.Cell.X);

        walker.TryStep(FlipbookDirection.Right);
        Assert.False(walker.Advance(0));
    }

    // Review Focus 3
    [Fact]
    public void Two_walkers_never_share_a_cell()
    {
        var place = Place(Room(20, 3));
        var a = new GridWalker(place, new Int2(0, 1));
        var b = new GridWalker(place, new Int2(2, 1));

        Assert.True(a.TryStep(FlipbookDirection.Right));
        Assert.False(b.TryStep(FlipbookDirection.Left));
        a.Advance(a.StepSeconds);
        Assert.False(b.TryStep(FlipbookDirection.Left));

        // The cell a left is free once it arrived
        var c = new GridWalker(place, new Int2(0, 0));
        Assert.True(c.TryStep(FlipbookDirection.Up));
    }

    [Fact]
    public void A_walker_travels_to_a_free_cell_of_another_place()
    {
        var town = Place(Room(20, 3));
        var houseMap = Room(5, 4);
        Put(houseMap, Wall, new Int2(3, 1));
        var house = Place(houseMap, "House");
        var walker = new GridWalker(town, new Int2(0, 1));

        Assert.True(walker.TryTravel(house, new Int2(2, 1), FlipbookDirection.Up));

        Assert.Same(house, walker.Place);
        Assert.Equal((new Int2(2, 1), new Int2(2, 1), FlipbookDirection.Up), (walker.Cell, walker.Target, walker.Facing));
        Assert.True(town.IsFree(new Int2(0, 1)));
        Assert.False(house.IsFree(new Int2(2, 1)));

        // A held cell, a solid cell, a cell outside the frame: nothing changes
        _ = new GridWalker(town, new Int2(5, 1));
        Assert.False(walker.TryTravel(town, new Int2(5, 1), FlipbookDirection.Down));
        Assert.False(walker.TryTravel(house, new Int2(3, 1), FlipbookDirection.Down));
        Assert.False(walker.TryTravel(town, new Int2(25, 1), FlipbookDirection.Down));
        Assert.Same(house, walker.Place);
        Assert.Equal((new Int2(2, 1), FlipbookDirection.Up), (walker.Cell, walker.Facing));

        // Not while it walks
        walker.TryStep(FlipbookDirection.Left);
        Assert.False(walker.TryTravel(town, new Int2(0, 1), FlipbookDirection.Down));
        Assert.Same(house, walker.Place);
    }

    [Fact]
    public void A_step_time_that_is_zero_or_not_a_number_arrives_at_once()
    {
        foreach (var time in new[] { 0f, float.NaN })
        {
            var walker = new GridWalker(Place(Room(20, 3)), new Int2(0, 1)) { StepSeconds = time };
            walker.TryStep(FlipbookDirection.Right);

            Assert.True(walker.Advance(0.01f), $"a step of {time} s");
            Assert.Equal(new Int2(1, 1), walker.Cell);
            Assert.False(float.IsNaN(walker.Position.X) || float.IsNaN(walker.Position.Y));
        }
    }

    [Fact]
    public void Time_that_is_negative_or_not_a_number_moves_nothing()
    {
        var walker = new GridWalker(Place(Room(20, 3)), new Int2(0, 1)) { StepSeconds = 0.2f };
        walker.TryStep(FlipbookDirection.Right);
        walker.Advance(0.1f);

        Assert.False(walker.Advance(-1));
        Assert.False(walker.Advance(float.NaN));

        Assert.Equal(new Vector2(1, 1), walker.Position);
        Assert.True(walker.Advance(0.1f));
    }

    [Fact]
    public void Only_the_four_sides_are_steps()
    {
        var walker = new GridWalker(Place(Room(20, 3)), new Int2(5, 1), FlipbookDirection.Left);

        Assert.False(walker.TryStep(FlipbookDirection.None));
        Assert.False(walker.TryStep(FlipbookDirection.UpLeft));

        Assert.Equal(FlipbookDirection.Left, walker.Facing);
        Assert.False(walker.IsMoving);
        Assert.Equal(Int2.Zero, GridWalker.Offset(FlipbookDirection.UpLeft));
        Assert.Equal(new Int2(0, -1), GridWalker.Offset(FlipbookDirection.Down));
    }
}
