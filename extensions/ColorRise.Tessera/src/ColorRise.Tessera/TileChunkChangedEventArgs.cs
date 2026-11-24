namespace ColorRise.Tessera;

/// <summary>
/// Tells which chunk of a layer changed, and whether the layer removed it because it has no tile left.
/// </summary>
public sealed class TileChunkChangedEventArgs(TileChunk chunk, bool removed) : EventArgs
{
    public TileChunk Chunk { get; } = chunk;

    public bool Removed { get; } = removed;
}
