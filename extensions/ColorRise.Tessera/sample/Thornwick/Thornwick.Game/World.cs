using ColorRise.Tessera;
using Stride.Core;
using Stride.Core.Serialization.Contents;
using Stride.Engine;
using Stride.Games;
using Stride.Input;

namespace Thornwick;

/// <summary>
/// The places of the scene, the doors between them and the count of the brambles. One in a scene.
/// </summary>
public sealed class World : SyncScript
{
    private readonly Dictionary<string, Place> places = [];

    // The count of the brambles the title shows; -1 before the first title
    private int shown = -1;

    /// <summary>
    /// The brambles left, in the places that have a map.
    /// </summary>
    [DataMemberIgnore]
    public int BramblesLeft { get; private set; }

    /// <summary>
    /// The place of a name: the entity of the scene, at any depth, that has that name and a tile map component; null when
    /// there is none. A place is made the first time it is asked for, and kept.
    /// </summary>
    public Place? Find(string name)
    {
        if (places.TryGetValue(name, out var place))
        {
            return place;
        }
        if (EntitiesOf(Entity.Scene).FirstOrDefault(entity => entity.Name == name && entity.Get<TileMapComponent>() is not null) is not { } found)
        {
            return null;
        }
        place = new Place(name, found.Get<TileMapComponent>());
        places[name] = place;
        return place;
    }

    /// <summary>
    /// Takes the door a walker stands on, with the places of the scene; the map of a place that has none is loaded from the
    /// door's URL. A wrong door is one error line of the log; a map that cannot be loaded is a wrong door, not an exception.
    /// </summary>
    public DoorResult TakeDoor(GridWalker walker)
    {
        var result = Doors.TryTake(walker, Find, LoadMap, out var problem);
        if (result == DoorResult.Wrong)
        {
            Log.Error(problem ?? "A door leads nowhere.");
        }
        else if (result == DoorResult.Taken)
        {
            // The place the walker arrived in may have just got its map
            Count();
        }
        return result;
    }

    /// <summary>
    /// Cuts the bush in front of a walker, and counts the brambles again when one was cut. A "cut" that names no tile is
    /// one error line of the log.
    /// </summary>
    public bool Cut(GridWalker walker)
    {
        if (walker.Place.Component.Map is not { } map || !Brambles.TryCut(map, walker.Ahead, out var problem))
        {
            return false;
        }
        if (problem is not null)
        {
            Log.Error(problem);
        }
        Count();
        return true;
    }

    /// <summary>
    /// The world of the scene an entity is in; null when the scene has none.
    /// </summary>
    public static World? Of(Entity entity) => EntitiesOf(entity.Scene).Select(other => other.Get<World>()).FirstOrDefault(world => world is not null);

    /// <summary>
    /// The title of the window for a count of brambles.
    /// </summary>
    public static string TitleFor(int brambles) => brambles switch
    {
        <= 0 => "Thornwick: all the brambles are cut",
        1 => "Thornwick: 1 bramble left",
        _ => $"Thornwick: {brambles} brambles left",
    };

    /// <inheritdoc />
    public override void Start()
    {
        Game.Window.AllowUserResizing = true;
        Count();
    }

    /// <inheritdoc />
    public override void Update()
    {
        if (Input.IsKeyPressed(Keys.Escape))
        {
            (Game as GameBase)?.Exit();
        }
        if (BramblesLeft != shown)
        {
            Game.Window.Title = TitleFor(BramblesLeft);
            shown = BramblesLeft;
        }
    }

    private void Count()
        => BramblesLeft = EntitiesOf(Entity.Scene).Select(entity => entity.Get<TileMapComponent>()?.Map).Sum(map => map is null ? 0 : Brambles.Count(map));

    // The map of a URL; null when the game has none there or it is no tile map. No line of its own: the door's problem is
    // the line, and asking first keeps the content manager from logging a missing asset as well
    private TileMap? LoadMap(string url)
    {
        if (!Content.Exists(url))
        {
            return null;
        }
        try
        {
            return Content.Load<TileMap>(url);
        }
        catch (ContentManagerException)
        {
            return null;
        }
    }

    // Every entity of a scene, at any depth
    private static IEnumerable<Entity> EntitiesOf(Scene? scene)
    {
        if (scene is null)
        {
            yield break;
        }
        var pending = new Stack<Entity>(scene.Entities);
        while (pending.TryPop(out var entity))
        {
            yield return entity;
            foreach (var child in entity.Transform.Children)
            {
                pending.Push(child.Entity);
            }
        }
    }
}
