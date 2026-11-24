using Stride.Core;

namespace ColorRise.Tessera.Assets;

/// <summary>
/// A tile set of a map, and the slot its cells name it by.
/// </summary>
[DataContract]
public sealed class TileSetSlot
{
    /// <userdoc>The number the cells of the map name this tile set by.</userdoc>
    [DataMember(10)]
    [Display(Browsable = false)]
    public int Slot { get; set; }

    /// <userdoc>The tile set.</userdoc>
    [DataMember(20)]
    [Display("Tile set")]
    public TileSet? TileSet { get; set; }
}
