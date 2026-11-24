using ColorRise.Tessera.Serialization;
using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Core.Serialization;
using Stride.Core.Serialization.Contents;
using Stride.Engine.Design;

namespace ColorRise.Tessera;

/// <summary>
/// A map of tiles: layers of cells, each cell a tile of one of the map's tile sets, found by the slot of the set.
/// </summary>
[DataContract]
[DataSerializer(typeof(TileMapSerializer))]
[ContentSerializer(typeof(DataContentSerializer<TileMap>))]
[ReferenceSerializer, DataSerializerGlobal(typeof(ReferenceSerializer<TileMap>), Profile = "Content")]
[DataSerializerGlobal(typeof(CloneSerializer<TileMap>), Profile = "Clone")]
public sealed class TileMap
{
    private readonly Dictionary<ushort, TileSet> tileSets = [];

    public TileMap() => Layers = new TileLayerCollection(this);

    /// <summary>
    /// The size of a cell, in world units.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">A side is not a finite number above 0.</exception>
    public Vector2 CellSize
    {
        get;
        set
        {
            if (!(float.IsFinite(value.X) && value.X > 0 && float.IsFinite(value.Y) && value.Y > 0))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Both sides of a cell must be above 0.");
            }
            field = value;
        }
    } = Vector2.One;

    /// <summary>
    /// The size of the map's frame, in cells, from the cell (0, 0); (0, 0) for a map without a frame.
    /// </summary>
    public Int2 Size
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value.X, nameof(value));
            ArgumentOutOfRangeException.ThrowIfNegative(value.Y, nameof(value));
            field = value;
        }
    }

    /// <summary>
    /// The tile sets of the map, by slot, from 1 to 65535.
    /// </summary>
    [DataMemberIgnore]
    public IReadOnlyDictionary<ushort, TileSet> TileSets => tileSets;

    [DataMemberIgnore]
    public TileLayerCollection Layers { get; }

    /// <summary>
    /// Gives the map a tile set, and returns its slot: the one it has, else the highest slot plus 1.
    /// </summary>
    /// <exception cref="InvalidOperationException">The slot 65535 is taken.</exception>
    public ushort AddTileSet(TileSet tileSet)
    {
        var slot = GetSlot(tileSet);
        if (slot != 0)
        {
            return slot;
        }
        var highest = tileSets.Count == 0 ? 0 : tileSets.Keys.Max();
        if (highest == ushort.MaxValue)
        {
            throw new InvalidOperationException("The map has a tile set in its last slot, 65535: set the tile set in a slot of your choice.");
        }
        slot = (ushort)(highest + 1);
        tileSets.Add(slot, tileSet);
        return slot;
    }

    /// <summary>
    /// Puts a tile set in a slot, in place of the one there.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The slot is 0, which empty cells have.</exception>
    public void SetTileSet(ushort slot, TileSet tileSet)
    {
        ArgumentOutOfRangeException.ThrowIfZero(slot);
        ArgumentNullException.ThrowIfNull(tileSet);
        tileSets[slot] = tileSet;
    }

    /// <summary>
    /// The lowest slot of a tile set, 0 when the map does not have it.
    /// </summary>
    public ushort GetSlot(TileSet tileSet)
    {
        ushort found = 0;
        foreach (var (slot, candidate) in tileSets)
        {
            if (ReferenceEquals(candidate, tileSet) && (found == 0 || slot < found))
            {
                found = slot;
            }
        }
        return found;
    }

    /// <summary>
    /// The tile of a cell, or null for an empty cell, or a slot or a tile that the map does not have.
    /// </summary>
    public Tile? GetTile(TileCell cell)
        => !cell.IsEmpty && tileSets.TryGetValue(cell.Slot, out var tileSet) ? tileSet.GetTile(cell.Tile) : null;

    /// <summary>
    /// Raised after the content manager read the map again in place (<see cref="ContentManager.Reload"/>): the same object
    /// has the layers, the cells and the tile sets of the content, in new layer objects. A tile set the map did not have
    /// before may still be loading when the event is raised.
    /// </summary>
    public event EventHandler? Reloaded;

    // Whether the content manager read this map, so that a second read is a reload
    internal bool WasRead { get; set; }

    // Empties the map before the content manager reads it
    internal void ClearForReading()
    {
        tileSets.Clear();
        while (Layers.Count > 0)
        {
            Layers.Remove(Layers[^1]);
        }
    }

    internal void OnReloaded() => Reloaded?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// A copy with its own layers and cells, and the same tile sets.
    /// </summary>
    public TileMap Clone()
    {
        var clone = new TileMap { CellSize = CellSize, Size = Size };
        foreach (var (slot, tileSet) in tileSets)
        {
            clone.tileSets.Add(slot, tileSet);
        }
        foreach (var layer in Layers)
        {
            clone.Layers.Insert(clone.Layers.Count, layer.Clone());
        }
        return clone;
    }
}
