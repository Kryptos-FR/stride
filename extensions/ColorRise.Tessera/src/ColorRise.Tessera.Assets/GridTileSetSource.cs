using Stride.Core;
using Stride.Core.Assets;
using Stride.Core.IO;
using Stride.Core.Mathematics;

namespace ColorRise.Tessera.Assets;

/// <summary>
/// An image cut in a grid of tiles of one size. A tile's id is the source's id, its column and its row.
/// </summary>
[DataContract("GridTileSetSource")]
[Display("Image grid")]
public sealed class GridTileSetSource : TileSetSource
{
    /// <userdoc>The image of the tiles.</userdoc>
    [DataMember(30)]
    [SourceFileMember(false)]
    public UFile? Image { get; set; }

    /// <summary>
    /// The size of a tile in pixels; (0, 0) takes the tile set's.
    /// </summary>
    /// <userdoc>The size of a tile in this image, in pixels. Leave it at (0, 0) to take the tile set's.</userdoc>
    [DataMember(40)]
    [Display("Tile size")]
    public Int2 TileSize { get; set; }

    /// <summary>
    /// The pixels before the first column and the first row.
    /// </summary>
    /// <userdoc>The pixels before the first column and before the first row of tiles.</userdoc>
    [DataMember(50)]
    public Int2 Margin { get; set; }

    /// <summary>
    /// The pixels between two columns and between two rows.
    /// </summary>
    /// <userdoc>The pixels between two columns and between two rows of tiles.</userdoc>
    [DataMember(60)]
    public Int2 Spacing { get; set; }
}
