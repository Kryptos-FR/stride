using ColorRise.Tessera;
using Stride.Core.Mathematics;
using Stride.Engine;

namespace Thornwick.Tests;

/// <summary>
/// Maps made in code, over one tile set without textures, for the rules of the game.
/// </summary>
internal static class TestMaps
{
    /// <summary>Not solid.</summary>
    public static readonly TileId Floor = TileId.Parse("1:0,0");

    /// <summary>Solid.</summary>
    public static readonly TileId Wall = TileId.Parse("1:1,0");

    /// <summary>Solid, of the kind "bush", and cut to the stump.</summary>
    public static readonly TileId Bush = TileId.Parse("1:2,0");

    /// <summary>Not solid: what is left of a bush.</summary>
    public static readonly TileId Stump = TileId.Parse("1:3,0");

    /// <summary>Of the kind "door", to the house, whose map it names.</summary>
    public static readonly TileId Door = TileId.Parse("1:4,0");

    /// <summary>Of the kind "door", back to the town.</summary>
    public static readonly TileId Mat = TileId.Parse("1:5,0");

    /// <summary>Solid, of the kind "bush", without a property.</summary>
    public static readonly TileId Hedge = TileId.Parse("1:6,0");

    /// <summary>
    /// A map with a frame of that size, the layers Ground (the floor on every cell of the frame) and Details (empty), and the
    /// tile set of these tiles in slot 1.
    /// </summary>
    public static TileMap Room(int width, int height)
    {
        var map = new TileMap { Size = new Int2(width, height) };
        var tiles = TileSet();
        map.AddTileSet(tiles);
        var ground = map.Layers.Add("Ground");
        map.Layers.Add("Details");
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                ground.SetCell(new Int2(x, y), tiles, Floor);
            }
        }
        return map;
    }

    /// <summary>
    /// The tile set of a map made by <see cref="Room"/>.
    /// </summary>
    public static TileSet Tiles(TileMap map) => map.TileSets[1];

    /// <summary>
    /// A place over a component with that map, on an entity at a position when one is given; a component without a map when
    /// the map is null.
    /// </summary>
    public static Place Place(TileMap? map, string name = "Town", Vector3? at = null)
    {
        var component = new TileMapComponent { Map = map };
        if (at is { } position)
        {
            var entity = new Entity(name) { component };
            entity.Transform.Position = position;
            entity.Transform.UpdateWorldMatrix();
        }
        return new Place(name, component);
    }

    /// <summary>
    /// Puts a tile on cells of the layer Details.
    /// </summary>
    public static void Put(TileMap map, TileId tile, params Int2[] cells)
    {
        foreach (var cell in cells)
        {
            map.Layers["Details"].SetCell(cell, Tiles(map), tile);
        }
    }

    private static TileSet TileSet()
    {
        var set = new TileSet();
        Add(set, Floor);
        Add(set, Wall, solid: true);
        Add(set, Bush, solid: true, kind: "bush").Properties["cut"] = Stump.ToString();
        Add(set, Stump);
        var door = Add(set, Door, kind: "door");
        door.Properties["to"] = "House";
        door.Properties["map"] = "Maps/House";
        Add(set, Mat, kind: "door").Properties["to"] = "Town";
        Add(set, Hedge, solid: true, kind: "bush");
        return set;
    }

    private static Tile Add(TileSet set, TileId id, bool solid = false, string? kind = null)
    {
        var tile = new Tile { Id = id, Collision = solid ? TileCollision.Solid : TileCollision.None, Kind = kind };
        set.Tiles.Add(id, tile);
        return tile;
    }
}
