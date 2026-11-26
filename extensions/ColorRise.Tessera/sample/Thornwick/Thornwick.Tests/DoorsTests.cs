using ColorRise.Tessera;
using Stride.Core.Mathematics;
using static Thornwick.Tests.TestMaps;

namespace Thornwick.Tests;

/// <summary>
/// The doors, read from tile data: the kind "door", the property "to" that names a place, and "map" that names its map.
/// </summary>
public sealed class DoorsTests
{
    private static readonly Int2 TownDoor = new(3, 5);
    private static readonly Int2 HouseMat = new(2, 0);

    [Fact]
    public void A_door_is_a_tile_of_the_kind_door_that_says_where_it_leads()
    {
        var town = TownMap();

        Assert.Equal<Door?>(new Door(TownDoor, "House", "Maps/House"), Doors.At(town, TownDoor));
        Assert.Equal<Door?>(new Door(HouseMat, "Town", null), Doors.At(HouseMap(), HouseMat));
        Assert.Null(Doors.At(town, new Int2(1, 1)));

        // A tile of the kind "door" without "to", with "to" empty, or with "to" that is not a string
        foreach (var to in new object?[] { null, "", 5 })
        {
            var map = TownMap();
            var properties = Tiles(map).Tiles[TestMaps.Door].Properties;
            properties.Remove("to");
            if (to is not null)
            {
                properties["to"] = to;
            }
            Assert.Null(Doors.At(map, TownDoor));
        }
    }

    [Fact]
    public void The_doors_of_a_map_come_in_the_order_of_their_cells()
    {
        var map = Room(8, 4);
        Put(map, TestMaps.Door, new Int2(5, 2), new Int2(1, 2), new Int2(3, 0));

        Assert.Equal([new Int2(3, 0), new Int2(1, 2), new Int2(5, 2)], Doors.Of(map).Select(door => door.Cell));
        Assert.Empty(Doors.Of(new TileMap()));
    }

    [Fact]
    public void A_walker_arrives_beside_the_door_that_leads_back()
    {
        // The cell under the mat is outside the frame
        Assert.Equal<(Int2, FlipbookDirection)?>((new Int2(2, 1), FlipbookDirection.Up), Doors.Arrival(Place(HouseMap(), "House"), "Town"));
        Assert.Equal<(Int2, FlipbookDirection)?>((new Int2(3, 4), FlipbookDirection.Down), Doors.Arrival(Place(TownMap()), "House"));
    }

    [Fact]
    public void The_arrival_goes_to_the_next_free_side_and_waits_when_there_is_none()
    {
        var town = Place(TownMap());
        _ = new GridWalker(town, new Int2(3, 4));

        Assert.Equal<(Int2, FlipbookDirection)?>((new Int2(3, 6), FlipbookDirection.Up), Doors.Arrival(town, "House"));

        // Walls on the left and the right of the door, walkers under and over it
        _ = new GridWalker(town, new Int2(3, 6));
        Assert.Null(Doors.Arrival(town, "House"));
    }

    [Fact]
    public void A_place_without_a_door_back_has_no_arrival()
    {
        Assert.Null(Doors.Arrival(Place(TownMap()), "Mill"));
        Assert.Null(Doors.Arrival(Place(null), "House"));
    }

    // Review Focus 3
    [Fact]
    public void A_door_is_not_open_ground()
    {
        var town = Place(TownMap());
        _ = new GridWalker(town, new Int2(6, 1));

        Assert.True(Doors.IsOpenGround(town, new Int2(1, 1)));
        Assert.True(town.IsFree(TownDoor));
        Assert.False(Doors.IsOpenGround(town, TownDoor));
        Assert.False(Doors.IsOpenGround(town, new Int2(0, 5)));
        Assert.False(Doors.IsOpenGround(town, new Int2(6, 1)));
        Assert.False(Doors.IsOpenGround(town, new Int2(-1, 1)));

        // A tile of the kind "door" that says nowhere is not open ground either
        var broken = TownMap();
        Tiles(broken).Tiles[TestMaps.Door].Properties.Remove("to");
        Assert.False(Doors.IsOpenGround(Place(broken), TownDoor));
    }

    [Fact]
    public void A_walker_on_a_door_is_taken_beside_the_door_back()
    {
        var town = Place(TownMap());
        var house = Place(null, "House");
        var houseMap = HouseMap();
        var loads = new List<string>();
        var walker = new GridWalker(town, new Int2(3, 4));
        Walk(walker, FlipbookDirection.Up);

        Assert.Equal(DoorResult.Taken, Doors.TryTake(walker, Find(town, house), url => { loads.Add(url); return houseMap; }, out var problem));

        Assert.Null(problem);
        Assert.Equal(["Maps/House"], loads);
        Assert.Same(houseMap, house.Component.Map);
        Assert.Same(house, walker.Place);
        Assert.Equal((new Int2(2, 1), FlipbookDirection.Up), (walker.Cell, walker.Facing));

        Walk(walker, FlipbookDirection.Down);

        Assert.Equal(DoorResult.Taken, Doors.TryTake(walker, Find(town, house), url => { loads.Add(url); return houseMap; }, out problem));

        Assert.Single(loads);
        Assert.Same(town, walker.Place);
        Assert.Equal((new Int2(3, 4), FlipbookDirection.Down), (walker.Cell, walker.Facing));
        Assert.True(house.IsFree(HouseMat));
    }

    [Fact]
    public void A_walker_that_stands_on_no_door_or_that_walks_is_not_taken()
    {
        var town = Place(TownMap());
        var house = Place(HouseMap(), "House");
        var loads = 0;
        TileMap? Load(string url)
        {
            loads++;
            return null;
        }

        var away = new GridWalker(town, new Int2(1, 1));
        Assert.Equal(DoorResult.None, Doors.TryTake(away, Find(town, house), Load, out var problem));
        Assert.Null(problem);

        // On the door, and walking away from it
        var leaving = new GridWalker(town, TownDoor);
        leaving.TryStep(FlipbookDirection.Down);
        Assert.Equal(DoorResult.None, Doors.TryTake(leaving, Find(town, house), Load, out problem));
        Assert.Null(problem);

        Assert.Equal(0, loads);
        Assert.Same(town, leaving.Place);
    }

    // Review Focus 3
    [Fact]
    public void A_walker_waits_while_its_arrival_is_not_free()
    {
        var houseMap = HouseMap();
        Put(houseMap, Wall, new Int2(1, 0), new Int2(3, 0));
        var town = Place(TownMap());
        var house = Place(houseMap, "House");
        var other = new GridWalker(house, new Int2(2, 1));
        var walker = new GridWalker(town, TownDoor);

        Assert.Equal(DoorResult.Waits, Doors.TryTake(walker, Find(town, house), _ => null, out var problem));

        Assert.Null(problem);
        Assert.Same(town, walker.Place);
        Assert.Equal(TownDoor, walker.Cell);

        Walk(other, FlipbookDirection.Right);

        Assert.Equal(DoorResult.Taken, Doors.TryTake(walker, Find(town, house), _ => null, out _));
        Assert.Equal(new Int2(2, 1), walker.Cell);
    }

    // Review Focus 5
    [Fact]
    public void A_door_that_leads_nowhere_is_wrong_and_says_why()
    {
        AssertWrong(
            "The door at (3, 5) of 'Town' leads to 'Mill', and the scene has no such place.",
            change: (town, _) => Tiles(town).Tiles[TestMaps.Door].Properties["to"] = "Mill");
        AssertWrong(
            "The door at (3, 5) of 'Town' leads to 'House', which has no map, and the door names none.",
            change: (town, _) => Tiles(town).Tiles[TestMaps.Door].Properties.Remove("map"));
        AssertWrong(
            "The map 'Maps/Hous' of the door at (3, 5) of 'Town' could not be loaded.",
            change: (town, _) => Tiles(town).Tiles[TestMaps.Door].Properties["map"] = "Maps/Hous");
        AssertWrong(
            "'House' has no door that leads back to 'Town'.",
            change: (_, house) => Tiles(house).Tiles[Mat].Properties["to"] = "Mill");
        foreach (var to in new object?[] { null, "", 5 })
        {
            AssertWrong(
                "The door at (3, 5) of 'Town' has no string 'to' that names a place.",
                change: (town, _) =>
                {
                    var properties = Tiles(town).Tiles[TestMaps.Door].Properties;
                    properties.Remove("to");
                    if (to is not null)
                    {
                        properties["to"] = to;
                    }
                });
        }
    }

    // A town of 8 by 8 cells, with a row of walls at y 5 and its door at (3, 5), to the house
    private static TileMap TownMap()
    {
        var map = Room(8, 8);
        for (var x = 0; x < 8; x++)
        {
            Put(map, x == TownDoor.X ? TestMaps.Door : Wall, new Int2(x, TownDoor.Y));
        }
        return map;
    }

    // A house of 5 by 4 cells, its mat at (2, 0), back to the town
    private static TileMap HouseMap()
    {
        var map = Room(5, 4);
        Put(map, Mat, HouseMat);
        return map;
    }

    private static Func<string, Place?> Find(params Place[] places) => name => places.FirstOrDefault(place => place.Name == name);

    // One whole step to a side
    private static void Walk(GridWalker walker, FlipbookDirection side)
    {
        Assert.True(walker.TryStep(side), $"a step {side} from {walker.Cell}");
        Assert.True(walker.Advance(walker.StepSeconds));
    }

    // A walker on the town's door, with the house's map loaded by the door: the door is wrong, says why, and moves nobody
    private static void AssertWrong(string expected, Action<TileMap, TileMap> change)
    {
        var townMap = TownMap();
        var houseMap = HouseMap();
        change(townMap, houseMap);
        var town = Place(townMap);
        var house = Place(null, "House");
        var walker = new GridWalker(town, TownDoor);

        var result = Doors.TryTake(walker, Find(town, house), url => url == "Maps/House" ? houseMap : null, out var problem);

        Assert.Equal(DoorResult.Wrong, result);
        Assert.Equal(expected, problem);
        Assert.Same(town, walker.Place);
        Assert.Equal(TownDoor, walker.Cell);
    }
}
