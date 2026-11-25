using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using ColorRise.Tessera.Serialization;
using Stride.Core;
using Stride.Core.Serialization;

namespace ColorRise.Tessera;

/// <summary>
/// One cell of a tile map layer: which of the map's tile sets, which tile of it, and how the tile is turned. Eight bytes.
/// </summary>
[DataContract]
[DataSerializer(typeof(TileCellSerializer))]
[StructLayout(LayoutKind.Sequential, Pack = 2)]
public readonly struct TileCell : IEquatable<TileCell>
{
    /// <summary>
    /// A cell of a tile; a slot of 0 makes the empty cell, whatever the tile and the flips.
    /// </summary>
    public TileCell(ushort slot, TileId tile, TileFlip flip = TileFlip.None)
    {
        if (slot == 0)
        {
            return;
        }
        Slot = slot;
        Flip = flip;
        Tile = tile;
    }

    /// <summary>
    /// The slot of the tile set in the map, 0 for an empty cell.
    /// </summary>
    public ushort Slot { get; }

    /// <summary>
    /// How the tile is turned.
    /// </summary>
    public TileFlip Flip { get; }

    /// <summary>
    /// The id of the tile in its tile set.
    /// </summary>
    public TileId Tile { get; }

    /// <summary>
    /// Whether the cell has no tile.
    /// </summary>
    public bool IsEmpty => Slot == 0;

    /// <summary>
    /// The cell without a tile.
    /// </summary>
    public static TileCell Empty => default;

    /// <summary>
    /// Whether both cells have the same slot, tile and flips.
    /// </summary>
    public bool Equals(TileCell other) => Slot == other.Slot && Flip == other.Flip && Tile == other.Tile;

    /// <summary>
    /// Whether the object is a cell with the same slot, tile and flips.
    /// </summary>
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is TileCell other && Equals(other);

    /// <summary>
    /// A hash of the slot, the tile and the flips.
    /// </summary>
    public override int GetHashCode() => HashCode.Combine(Slot, Flip, Tile);

    /// <summary>
    /// Whether both cells have the same slot, tile and flips.
    /// </summary>
    public static bool operator ==(TileCell left, TileCell right) => left.Equals(right);

    /// <summary>
    /// Whether the cells differ in their slot, tile or flips.
    /// </summary>
    public static bool operator !=(TileCell left, TileCell right) => !left.Equals(right);
}
