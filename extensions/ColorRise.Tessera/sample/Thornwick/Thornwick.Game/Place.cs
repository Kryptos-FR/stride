using ColorRise.Tessera;
using Stride.Core.Mathematics;

namespace Thornwick;

/// <summary>
/// A map shown in the scene, and who stands on which of its cells.
/// </summary>
public sealed class Place
{
    /// <summary>
    /// The name of the layer that hides what walks under it, such as the roofs.
    /// </summary>
    public const string OverheadLayer = "Overhead";

    private readonly Dictionary<Int2, GridWalker> holders = [];

    /// <summary>
    /// A place of a name, over the component that shows its map.
    /// </summary>
    public Place(string name, TileMapComponent component)
    {
        Name = name;
        Component = component;
    }

    /// <summary>
    /// The name of the place, which the doors give in their property <c>to</c>.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The component that shows the place's map. Its map can be given after the place is made.
    /// </summary>
    public TileMapComponent Component { get; }

    /// <summary>
    /// The frame in world units, X and Y its bottom-left corner; null when the map has no frame, or there is no map.
    /// </summary>
    public RectangleF? Frame
    {
        get
        {
            if (Component.Map is not { } map || !HasFrame(map))
            {
                return null;
            }
            var corner = Component.CellToWorld(Int2.Zero);
            var far = Component.CellToWorld(map.Size);
            return new RectangleF(corner.X, corner.Y, far.X - corner.X, far.Y - corner.Y);
        }
    }

    /// <summary>
    /// The z, in the map's space, of what walks here: half a layer spacing under the layer "Overhead" when the map has one,
    /// else half a spacing under where one more layer would be.
    /// </summary>
    public float WalkerZ
    {
        get
        {
            if (Component.Map is not { } map)
            {
                return 0;
            }
            var spacing = Component.LayerSpacing;
            var index = map.Layers.TryGet(OverheadLayer, out var overhead) ? map.Layers.IndexOf(overhead) : map.Layers.Count;
            return index * spacing - spacing / 2;
        }
    }

    /// <summary>
    /// Whether a cell is inside the map's frame. Every cell is, while the component has no map or the map has no frame.
    /// </summary>
    public bool InFrame(Int2 cell)
    {
        if (Component.Map is not { } map || !HasFrame(map))
        {
            return true;
        }
        return cell.X >= 0 && cell.Y >= 0 && cell.X < map.Size.X && cell.Y < map.Size.Y;
    }

    /// <summary>
    /// Whether a walker can step on a cell: inside the frame, not solid, and no walker stands on it or walks to it.
    /// </summary>
    public bool IsFree(Int2 cell) => InFrame(cell) && !Component.IsSolid(cell) && !holders.ContainsKey(cell);

    /// <summary>
    /// The point of the world of a position in cells, at <see cref="WalkerZ"/>. The bottom centre of the cell (x, y) is
    /// (x + 0.5, y).
    /// </summary>
    public Vector3 ToWorld(Vector2 cells)
    {
        var size = Component.Map?.CellSize ?? Vector2.One;
        var local = new Vector3(cells.X * size.X, cells.Y * size.Y, WalkerZ);
        var world = Component.Entity?.Transform.WorldMatrix ?? Matrix.Identity;
        return Vector3.TransformCoordinate(local, world);
    }

    /// <summary>
    /// Gives a cell to a walker; false when another walker holds it.
    /// </summary>
    internal bool TryHold(Int2 cell, GridWalker walker)
    {
        if (holders.TryGetValue(cell, out var holder))
        {
            return holder == walker;
        }
        holders[cell] = walker;
        return true;
    }

    /// <summary>
    /// Takes a cell back from the walker that holds it.
    /// </summary>
    internal void Release(Int2 cell, GridWalker walker)
    {
        if (holders.TryGetValue(cell, out var holder) && holder == walker)
        {
            holders.Remove(cell);
        }
    }

    private static bool HasFrame(TileMap map) => map.Size.X > 0 && map.Size.Y > 0;
}
