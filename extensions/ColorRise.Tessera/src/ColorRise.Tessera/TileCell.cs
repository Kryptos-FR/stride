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

    public TileFlip Flip { get; }

    public TileId Tile { get; }

    public bool IsEmpty => Slot == 0;

    public static TileCell Empty => default;

    public bool Equals(TileCell other) => Slot == other.Slot && Flip == other.Flip && Tile == other.Tile;

    public override bool Equals([NotNullWhen(true)] object? obj) => obj is TileCell other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Slot, Flip, Tile);

    public static bool operator ==(TileCell left, TileCell right) => left.Equals(right);

    public static bool operator !=(TileCell left, TileCell right) => !left.Equals(right);
}
