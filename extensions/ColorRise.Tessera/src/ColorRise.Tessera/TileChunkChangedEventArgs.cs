namespace ColorRise.Tessera;

/// <summary>
/// Tells which chunk of a layer changed, and whether the layer removed it because it has no tile left.
/// </summary>
public sealed class TileChunkChangedEventArgs(TileChunk chunk, bool removed) : EventArgs
{
    /// <summary>
    /// The chunk of the cell that changed.
    /// </summary>
    public TileChunk Chunk { get; } = chunk;

    /// <summary>
    /// Whether the chunk was removed from its layer, as it has no tile left.
    /// </summary>
    public bool Removed { get; } = removed;
}
