using Stride.Core.Serialization;

namespace ColorRise.Tessera.Serialization;

/// <summary>
/// Writes a tile id as its number.
/// </summary>
internal sealed class TileIdSerializer : DataSerializer<TileId>
{
    public override void Serialize(ref TileId obj, ArchiveMode mode, SerializationStream stream)
    {
        if (mode == ArchiveMode.Serialize)
        {
            stream.Write(obj.Value);
        }
        else
        {
            obj = TileId.FromValue(stream.ReadUInt32());
        }
    }
}
