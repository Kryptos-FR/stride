using Stride.Core.Serialization;

namespace ColorRise.Tessera.Serialization;

/// <summary>
/// Writes a cell as its slot, its flips and the number of its tile.
/// </summary>
internal sealed class TileCellSerializer : DataSerializer<TileCell>
{
    public override void Serialize(ref TileCell obj, ArchiveMode mode, SerializationStream stream)
    {
        if (mode == ArchiveMode.Serialize)
        {
            Write(stream, obj);
        }
        else
        {
            obj = Read(stream);
        }
    }

    internal static void Write(SerializationStream stream, TileCell cell)
    {
        stream.Write(cell.Slot);
        stream.Write((byte)cell.Flip);
        stream.Write(cell.Tile.Value);
    }

    internal static TileCell Read(SerializationStream stream)
    {
        var slot = stream.ReadUInt16();
        var flip = (TileFlip)stream.ReadByte();
        return new TileCell(slot, TileId.FromValue(stream.ReadUInt32()), flip);
    }
}
