using Stride.Core;
using Stride.Core.Mathematics;

namespace ColorRise.Tessera;

/// <summary>
/// One tile of a frame of a flipbook, at its place in the frame.
/// </summary>
[DataContract("FlipbookCell")]
public sealed class FlipbookCell
{
    /// <summary>
    /// The tile, of the flipbook's tile set.
    /// </summary>
    [DataMember(10)]
    public TileId Tile { get; set; }

    /// <summary>
    /// Where the tile is, in pixels, x right and y up, from where the frame's first cell is.
    /// </summary>
    /// <userdoc>Where the tile is, in pixels, from the frame's first tile: x to the right, y up.</userdoc>
    [DataMember(20)]
    public Int2 Offset { get; set; }

    /// <summary>
    /// How the tile is turned.
    /// </summary>
    /// <userdoc>How the tile is turned: mirrored left to right, top to bottom, or across its diagonal.</userdoc>
    [DataMember(30)]
    public TileFlip Flip { get; set; }
}
