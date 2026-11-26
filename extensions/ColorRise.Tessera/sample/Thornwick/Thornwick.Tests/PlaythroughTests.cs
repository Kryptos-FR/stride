using ColorRise.Tessera;
using Stride.Core.Mathematics;

namespace Thornwick.Tests;

/// <summary>
/// The game played on its own maps by the rules, a step at a time, with nothing drawn.
/// </summary>
public sealed class PlaythroughTests
{
    [Fact]
    public void The_door_leads_to_the_house_and_back()
    {
        var town = TestMaps.Place(SampleAssets.Map("Maps/Town"), "Town");
        var house = TestMaps.Place(null, "House");
        var start = Doors.Arrival(town, "House")!.Value;
        var walker = new GridWalker(town, start.Cell, start.Facing);
        var loaded = new List<string>();
        Place? Find(string name) => name == "Town" ? town : name == "House" ? house : null;
        TileMap? Load(string url)
        {
            loaded.Add(url);
            return SampleAssets.Map(url);
        }

        var townDoor = Assert.Single(Doors.Of(town.Component.Map!)).Cell;
        Walk(walker, Reach.Path(town, walker.Cell, townDoor));

        Assert.Equal(DoorResult.Taken, Doors.TryTake(walker, Find, Load, out var problem));
        Assert.Null(problem);
        Assert.Equal(["Maps/House"], loaded);
        Assert.Same(house, walker.Place);
        var mat = Assert.Single(Doors.Of(house.Component.Map!)).Cell;
        Assert.Equal(1, Math.Abs(walker.Cell.X - mat.X) + Math.Abs(walker.Cell.Y - mat.Y));

        Walk(walker, Reach.Path(house, walker.Cell, mat));

        Assert.Equal(DoorResult.Taken, Doors.TryTake(walker, Find, Load, out problem));
        Assert.Null(problem);
        Assert.Same(town, walker.Place);
        Assert.Equal(start.Cell, walker.Cell);
        Assert.Equal(FlipbookDirection.Down, walker.Facing);
    }

    [Fact]
    public void The_whole_game_can_be_played()
    {
        var map = SampleAssets.Map("Maps/Town");
        var town = TestMaps.Place(map, "Town");
        var start = Doors.Arrival(town, "House")!.Value;
        var walker = new GridWalker(town, start.Cell, start.Facing);

        while (Nearest(town, walker.Cell) is (var stand, var side, var bush))
        {
            Walk(walker, Reach.Path(town, walker.Cell, stand));

            Assert.False(walker.TryStep(side));
            Assert.Equal(bush, walker.Ahead);
            Assert.True(Brambles.TryCut(map, walker.Ahead), $"the bramble at {bush} is not cut");
        }

        Assert.Equal(0, Brambles.Count(map));
    }

    // Walks a way a step at a time, as the game does; every cell stood on is inside the frame
    private static void Walk(GridWalker walker, IReadOnlyList<FlipbookDirection>? path)
    {
        Assert.NotNull(path);
        foreach (var side in path)
        {
            Assert.True(walker.TryStep(side), $"no step {side} from {walker.Cell}");
            Assert.True(walker.Advance(walker.StepSeconds));
            Assert.True(walker.Place.InFrame(walker.Cell), $"{walker.Cell} is outside the frame");
        }
    }

    // The bramble nearest to a cell by the way there: where to stand, the side to face, and the bramble's cell
    private static (Int2 Stand, FlipbookDirection Side, Int2 Bush)? Nearest(Place place, Int2 from)
    {
        var map = place.Component.Map!;
        (Int2, FlipbookDirection, Int2)? nearest = null;
        var shortest = int.MaxValue;
        var bushes = MapCells.Where(map, tile => tile.Kind == TileKinds.Bush).OrderBy(cell => cell.Y).ThenBy(cell => cell.X);
        foreach (var bush in bushes)
        {
            foreach (var side in new[] { FlipbookDirection.Down, FlipbookDirection.Up, FlipbookDirection.Left, FlipbookDirection.Right })
            {
                var stand = bush - GridWalker.Offset(side);
                if (Reach.Path(place, from, stand) is { } path && path.Count < shortest)
                {
                    (nearest, shortest) = ((stand, side, bush), path.Count);
                }
            }
        }
        return nearest;
    }
}
