using Stride.Core;
using Stride.Core.Mathematics;

namespace ColorRise.Tessera.Assets;

/// <summary>
/// A chunk of a layer in a tile map asset: its place, in chunks, and its 256 cells as the text of <see cref="TileChunkText"/>.
/// </summary>
[DataContract]
public sealed class TileChunkAsset
{
    /// <summary>
    /// Where the chunk is, in chunks: the chunk (1, 0) holds the cells (16, 0) to (31, 15).
    /// </summary>
    [DataMember(10)]
    public Int2 Position { get; set; }

    /// <summary>
    /// The 256 cells as text, 16 lines of 16 cells, the top row first.
    /// </summary>
    [DataMember(20)]
    [DataStyle(ScalarStyle.Literal)]
    public string Cells { get; set; } = "";
}
