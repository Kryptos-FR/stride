using Stride.Core;
using Stride.Graphics;

namespace ColorRise.Tessera.Assets;

/// <summary>
/// The sprites of a sprite sheet, each bound by its name to a number that its tile keeps: a sprite moved in the sheet keeps
/// its tile, and a tile's id is the source's id and that number.
/// </summary>
[DataContract("SpriteSheetTileSetSource")]
[Display("Sprite sheet")]
public sealed class SpriteSheetTileSetSource : TileSetSource
{
    /// <userdoc>The sprite sheet whose sprites are tiles.</userdoc>
    [DataMember(30)]
    [Display("Sprite sheet")]
    public SpriteSheet? Sheet { get; set; }

    /// <summary>
    /// The number of each sprite name.
    /// </summary>
    [DataMember(40)]
    [Display(Browsable = false)]
    public List<SpriteTileBinding> Bindings { get; } = [];

    /// <summary>
    /// The last number given: a number is never given twice, so that a map never takes the tile of another sprite.
    /// </summary>
    [DataMember(50)]
    [Display(Browsable = false)]
    public int LastNumber { get; set; }
}
