using Stride.Core.Mathematics;
using Stride.Core.Serialization;

namespace ColorRise.Tessera.Serialization;

/// <summary>
/// Writes a map as content: its sizes, its tile sets by slot as references to their own content, then its layers, each
/// with its looks and the cells of its chunks that have a tile.
/// </summary>
/// <remarks>
/// Reading into a map that was read before is a reload: the map is emptied, filled again, then raises
/// <see cref="TileMap.Reloaded"/>.
/// </remarks>
internal sealed class TileMapSerializer : ClassDataSerializer<TileMap>
{
    // The serializer's own number for the layout below. A map of another number fails to load with an InvalidDataException that
    // says to build the game again. Nothing ties it to the version of the tile map's build command: a change of the layout
    // raises both, this number and TileMapCommand.Version, so that the build writes the maps again
    private const int Format = 1;

    public override void Serialize(ref TileMap obj, ArchiveMode mode, SerializationStream stream)
    {
        if (mode == ArchiveMode.Serialize)
        {
            Write(obj, stream);
        }
        else
        {
            Read(obj, stream);
        }
    }

    private static void Write(TileMap map, SerializationStream stream)
    {
        stream.Write(Format);
        stream.Write(map.CellSize.X);
        stream.Write(map.CellSize.Y);
        stream.Write(map.Size.X);
        stream.Write(map.Size.Y);

        stream.Write(map.TileSets.Count);
        foreach (var (slot, tileSet) in map.TileSets.OrderBy(pair => pair.Key))
        {
            stream.Write(slot);
            var reference = tileSet;
            stream.SerializeExtended(ref reference, ArchiveMode.Serialize);
        }

        stream.Write(map.Layers.Count);
        foreach (var layer in map.Layers)
        {
            stream.Write(layer.Name);
            stream.Write(layer.Visible);
            stream.Write(layer.Opacity);
            stream.Write(layer.Tint.R);
            stream.Write(layer.Tint.G);
            stream.Write(layer.Tint.B);
            stream.Write(layer.Tint.A);
            stream.Write(layer.Offset.X);
            stream.Write(layer.Offset.Y);
            stream.Write(layer.Collision);

            stream.Write(layer.Chunks.Count);
            foreach (var chunk in layer.Chunks)
            {
                stream.Write(chunk.Position.X);
                stream.Write(chunk.Position.Y);
                stream.Write(chunk.Count);
                for (var i = 0; i < chunk.Cells.Length; i++)
                {
                    if (!chunk.Cells[i].IsEmpty)
                    {
                        stream.Write((byte)i);
                        TileCellSerializer.Write(stream, chunk.Cells[i]);
                    }
                }
            }
        }
    }

    private static void Read(TileMap map, SerializationStream stream)
    {
        var format = stream.ReadInt32();
        if (format != Format)
        {
            throw new InvalidDataException($"The tile map is in format {format}, which this version of Tessera does not read (it reads {Format}): build the game again.");
        }

        map.ClearForReading();
        map.CellSize = new Vector2(stream.ReadSingle(), stream.ReadSingle());
        map.Size = new Int2(stream.ReadInt32(), stream.ReadInt32());

        var tileSets = stream.ReadInt32();
        for (var i = 0; i < tileSets; i++)
        {
            var slot = stream.ReadUInt16();
            TileSet? tileSet = null;
            stream.SerializeExtended(ref tileSet, ArchiveMode.Deserialize);
            // A tile set whose content is not in the database fails the load: the content manager throws a
            // ContentManagerException with its URL. A reference read as null leaves its slot empty, and its cells draw nothing
            if (tileSet is not null)
            {
                map.SetTileSet(slot, tileSet);
            }
        }

        var layers = stream.ReadInt32();
        for (var i = 0; i < layers; i++)
        {
            var layer = new TileLayer
            {
                Name = stream.ReadString(),
                Visible = stream.ReadBoolean(),
                Opacity = stream.ReadSingle(),
                Tint = new Color(stream.ReadByte(), stream.ReadByte(), stream.ReadByte(), stream.ReadByte()),
                Offset = new Vector2(stream.ReadSingle(), stream.ReadSingle()),
                Collision = stream.ReadBoolean(),
            };
            map.Layers.Insert(map.Layers.Count, layer);

            var chunks = stream.ReadInt32();
            for (var c = 0; c < chunks; c++)
            {
                var position = new Int2(stream.ReadInt32(), stream.ReadInt32());
                var cells = stream.ReadInt32();
                for (var n = 0; n < cells; n++)
                {
                    var index = stream.ReadByte();
                    var cell = TileCellSerializer.Read(stream);
                    layer.SetCell(new Int2(position.X * TileChunk.Size + index % TileChunk.Size, position.Y * TileChunk.Size + index / TileChunk.Size), cell);
                }
            }
        }

        if (map.WasRead)
        {
            map.OnReloaded();
        }
        map.WasRead = true;
    }
}
