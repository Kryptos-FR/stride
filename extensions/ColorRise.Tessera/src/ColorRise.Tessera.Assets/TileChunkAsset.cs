using Stride.Core;
using Stride.Core.Mathematics;

namespace ColorRise.Tessera.Assets;

/// <summary>
/// A chunk of a layer in a tile map asset: its place, in chunks, and its 256 cells as the text of <see cref="TileChunkText"/>.
/// </summary>
[DataContract]
public sealed class TileChunkAsset
{
    [DataMember(10)]
    public Int2 Position { get; set; }

    [DataMember(20)]
    [DataStyle(ScalarStyle.Literal)]
    public string Cells { get; set; } = "";
}
