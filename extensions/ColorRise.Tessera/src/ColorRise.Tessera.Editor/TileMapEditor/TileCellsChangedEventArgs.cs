using Stride.Core.Mathematics;

namespace ColorRise.Tessera.Editor.TileMapEditor;

/// <summary>
/// The chunks of a layer whose cells changed.
/// </summary>
public sealed class TileCellsChangedEventArgs(int layer, IReadOnlyCollection<Int2> chunks) : EventArgs
{
    /// <summary>
    /// The index of the layer, as in the asset.
    /// </summary>
    public int Layer { get; } = layer;

    /// <summary>
    /// The places of the chunks, in chunks.
    /// </summary>
    public IReadOnlyCollection<Int2> Chunks { get; } = chunks;
}
