using Stride.Core;
using Stride.Core.Mathematics;

namespace ColorRise.Tessera;

/// <summary>
/// One layer of a tile map: its cells, kept in chunks of 16 by 16 that exist only where there are tiles, and how it looks.
/// </summary>
[DataContract]
public sealed class TileLayer
{
    private readonly Dictionary<Int2, TileChunk> chunks = [];

    /// <summary>
    /// The name of the layer, which game code finds it by.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Whether the layer is drawn.
    /// </summary>
    public bool Visible { get; set; } = true;

    /// <summary>
    /// From 0, invisible, to 1, opaque.
    /// </summary>
    public float Opacity { get; set; } = 1;

    /// <summary>
    /// The colour every tile of the layer is multiplied by.
    /// </summary>
    public Color Tint { get; set; } = Color.White;

    /// <summary>
    /// Moves the layer when it is drawn, in world units. Queries of cells ignore it.
    /// </summary>
    public Vector2 Offset { get; set; }

    /// <summary>
    /// Whether the solid tiles of the layer stop what moves on the map.
    /// </summary>
    public bool Collision { get; set; } = true;

    /// <summary>
    /// The map the layer is in, if any.
    /// </summary>
    [DataMemberIgnore]
    public TileMap? Map { get; internal set; }

    /// <summary>
    /// The chunks that have at least one tile.
    /// </summary>
    [DataMemberIgnore]
    public IReadOnlyCollection<TileChunk> Chunks => chunks.Values;

    /// <summary>
    /// Raised when the value of a cell really changes, with the chunk of the cell.
    /// </summary>
    public event EventHandler<TileChunkChangedEventArgs>? Changed;

    /// <summary>
    /// The chunk that holds a cell: each coordinate divided by 16, rounded down.
    /// </summary>
    public static Int2 ChunkOf(Int2 cell) => new(cell.X >> 4, cell.Y >> 4);

    /// <summary>
    /// The chunk at a place, in chunks, or null when it has no tile.
    /// </summary>
    public TileChunk? GetChunk(Int2 chunkPosition) => chunks.GetValueOrDefault(chunkPosition);

    /// <summary>
    /// The value of a cell, empty where the layer has no tile.
    /// </summary>
    public TileCell GetCell(Int2 cell)
        => chunks.TryGetValue(ChunkOf(cell), out var chunk) ? chunk.Cells[TileChunk.IndexOf(cell)] : TileCell.Empty;

    /// <summary>
    /// Sets the value of a cell. An empty value clears it, and a chunk left without a tile is removed.
    /// </summary>
    public void SetCell(Int2 cell, TileCell value)
    {
        var position = ChunkOf(cell);
        if (!chunks.TryGetValue(position, out var chunk))
        {
            if (value.IsEmpty)
            {
                return;
            }
            chunk = new TileChunk(position);
            chunks.Add(position, chunk);
        }

        if (!chunk.Set(TileChunk.IndexOf(cell), value))
        {
            return;
        }
        var removed = chunk.Count == 0;
        if (removed)
        {
            chunks.Remove(position);
        }
        Changed?.Invoke(this, new TileChunkChangedEventArgs(chunk, removed));
    }

    /// <summary>
    /// Puts a tile of one of the map's tile sets in a cell.
    /// </summary>
    /// <exception cref="InvalidOperationException">The layer is in no map.</exception>
    /// <exception cref="ArgumentException">The tile set is not one of the map's.</exception>
    public void SetCell(Int2 cell, TileSet tileSet, TileId tile, TileFlip flip = TileFlip.None)
    {
        ArgumentNullException.ThrowIfNull(tileSet);
        var map = Map ?? throw new InvalidOperationException($"The layer '{Name}' is in no map: add it to a map's layers first.");
        var slot = map.GetSlot(tileSet);
        if (slot == 0)
        {
            throw new ArgumentException("The tile set is not one of the map's: add it with TileMap.AddTileSet first.", nameof(tileSet));
        }
        SetCell(cell, new TileCell(slot, tile, flip));
    }

    /// <summary>
    /// Removes the tile of a cell.
    /// </summary>
    public void ClearCell(Int2 cell) => SetCell(cell, TileCell.Empty);

    // The same looks and cells, in no map
    internal TileLayer Clone()
    {
        var clone = new TileLayer
        {
            Name = Name,
            Visible = Visible,
            Opacity = Opacity,
            Tint = Tint,
            Offset = Offset,
            Collision = Collision,
        };
        foreach (var (position, chunk) in chunks)
        {
            clone.chunks.Add(position, chunk.Clone());
        }
        return clone;
    }
}
