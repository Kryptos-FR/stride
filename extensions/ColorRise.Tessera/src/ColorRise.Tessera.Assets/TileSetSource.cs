using Stride.Core;

namespace ColorRise.Tessera.Assets;

/// <summary>
/// Where some tiles of a tile set come from: an image cut in a grid, or the sprites of a sprite sheet.
/// </summary>
[DataContract(Inherited = true)]
public abstract class TileSetSource
{
    /// <summary>
    /// The id of the source, 1 to 255: the first part of the ids of its tiles.
    /// </summary>
    [DataMember(10)]
    [Display(Browsable = false)]
    public int Id { get; set; }

    /// <userdoc>The name of the source, as the palette shows it.</userdoc>
    [DataMember(20)]
    public string Name { get; set; } = "";
}
