using ColorRise.Tessera;
using Stride.Core.Mathematics;

namespace Thornwick;

/// <summary>
/// A door of a map: its cell, the name of the place it leads to, and the URL of that place's map if it says one.
/// </summary>
public readonly record struct Door(Int2 Cell, string To, string? Map);

/// <summary>
/// What became of a walker that asked for the door of its cell.
/// </summary>
public enum DoorResult
{
    /// <summary>
    /// Its cell has no door, or it walks.
    /// </summary>
    None,

    /// <summary>
    /// It is now in the other place, beside the door that leads back.
    /// </summary>
    Taken,

    /// <summary>
    /// The door is right, and where it would arrive is not free now.
    /// </summary>
    Waits,

    /// <summary>
    /// The door leads nowhere: the place is not found, it has no map and none could be loaded, or it has no door back.
    /// </summary>
    Wrong,
}

/// <summary>
/// The doors of the maps, read from their tiles: the kind "door", the property "to" and the property "map".
/// </summary>
public static class Doors
{
    // The sides tried for an arrival beside a door, in order
    private static readonly FlipbookDirection[] ArrivalSides = [FlipbookDirection.Down, FlipbookDirection.Up, FlipbookDirection.Left, FlipbookDirection.Right];

    /// <summary>
    /// The door of a cell: its topmost tile of the kind "door" whose property "to" is a string that is not empty; null when
    /// there is none.
    /// </summary>
    public static Door? At(TileMap map, Int2 cell)
    {
        for (var index = map.Layers.Count - 1; index >= 0; index--)
        {
            if (map.GetTile(map.Layers[index].GetCell(cell)) is { Kind: TileKinds.Door } tile
                && tile.Properties.TryGetValue(TileProperties.To, out var to) && to is string { Length: > 0 } place)
            {
                var url = tile.Properties.TryGetValue(TileProperties.Map, out var value) && value is string { Length: > 0 } text ? text : null;
                return new Door(cell, place, url);
            }
        }
        return null;
    }

    /// <summary>
    /// Every door of a map, the rows from the bottom, each row from the left.
    /// </summary>
    public static IReadOnlyList<Door> Of(TileMap map)
        => TileKinds.CellsOf(map, TileKinds.Door)
            .Select(cell => At(map, cell))
            .OfType<Door>()
            .OrderBy(door => door.Cell.Y)
            .ThenBy(door => door.Cell.X)
            .ToList();

    /// <summary>
    /// Where a walker that comes from another place arrives: beside the first door, in the order of <see cref="Of"/>, that
    /// leads back there, on its first free neighbour among down, up, left and right, facing away from the door. Null when
    /// the place has no such door, or none of them has a free neighbour.
    /// </summary>
    public static (Int2 Cell, FlipbookDirection Facing)? Arrival(Place place, string from)
    {
        if (place.Component.Map is not { } map)
        {
            return null;
        }
        foreach (var door in Of(map).Where(door => door.To == from))
        {
            foreach (var side in ArrivalSides)
            {
                var cell = door.Cell + GridWalker.Offset(side);
                if (place.IsFree(cell))
                {
                    return (cell, side);
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Whether a villager may step on a cell: the cell is free, and has no door.
    /// </summary>
    public static bool IsOpenGround(Place place, Int2 cell)
        => place.IsFree(cell) && (place.Component.Map is not { } map || At(map, cell) is null);

    /// <summary>
    /// Takes the door a walker stands on. <paramref name="find"/> gives the place of a name, or null.
    /// <paramref name="load"/> gives the map of a URL, or null; it is asked only when the place the door leads to has no
    /// map yet, and that map is then given to the place's component. The walker arrives where <see cref="Arrival"/> says.
    /// <paramref name="problem"/> is given with <see cref="DoorResult.Wrong"/> only.
    /// </summary>
    public static DoorResult TryTake(GridWalker walker, Func<string, Place?> find, Func<string, TileMap?> load, out string? problem)
    {
        problem = null;
        var here = walker.Place;
        if (walker.IsMoving || here.Component.Map is not { } map || At(map, walker.Cell) is not { } door)
        {
            return DoorResult.None;
        }

        var where = $"The door at ({door.Cell.X}, {door.Cell.Y}) of '{here.Name}'";
        if (find(door.To) is not { } there)
        {
            problem = $"{where} leads to '{door.To}', and the scene has no such place.";
            return DoorResult.Wrong;
        }
        if (there.Component.Map is null)
        {
            if (door.Map is null)
            {
                problem = $"{where} leads to '{door.To}', which has no map, and the door names none.";
                return DoorResult.Wrong;
            }
            if (load(door.Map) is not { } loaded)
            {
                problem = $"The map '{door.Map}' of the door at ({door.Cell.X}, {door.Cell.Y}) of '{here.Name}' could not be loaded.";
                return DoorResult.Wrong;
            }
            there.Component.Map = loaded;
        }
        if (!Of(there.Component.Map).Any(back => back.To == here.Name))
        {
            problem = $"'{there.Name}' has no door that leads back to '{here.Name}'.";
            return DoorResult.Wrong;
        }

        return Arrival(there, here.Name) is { } arrival && walker.TryTravel(there, arrival.Cell, arrival.Facing)
            ? DoorResult.Taken
            : DoorResult.Waits;
    }
}
