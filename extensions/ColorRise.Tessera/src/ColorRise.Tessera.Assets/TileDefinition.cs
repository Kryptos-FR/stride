using Stride.Core;

namespace ColorRise.Tessera.Assets;

/// <summary>
/// The data of one tile of a tile set: what a game knows of it, and its animation.
/// </summary>
[DataContract]
public sealed class TileDefinition
{
    /// <summary>
    /// The tile the data is for.
    /// </summary>
    [DataMember(10)]
    [Display(Browsable = false)]
    public TileId Tile { get; set; }

    /// <userdoc>Solid tiles stop the characters on a layer that has collision.</userdoc>
    [DataMember(20)]
    public TileCollision Collision { get; set; }

    /// <userdoc>What the tile is to the game, such as water or door.</userdoc>
    [DataMember(30)]
    public string? Kind { get; set; }

    /// <summary>
    /// The game's own values for the tile, by name, each of one of the five types a tile's property can have.
    /// </summary>
    /// <userdoc>The game's own values for the tile: each one a bool, an int, a float, a string or a colour.</userdoc>
    [DataMember(40)]
    public Dictionary<string, TileProperty> Properties { get; } = [];

    /// <userdoc>The frames the tile shows in turn, with their durations in seconds.</userdoc>
    [DataMember(50)]
    public List<TileAnimationFrame> Animation { get; } = [];

    /// <summary>
    /// Whether the definition says nothing that the tile would not have without it.
    /// </summary>
    [DataMemberIgnore]
    public bool IsDefault => Collision == TileCollision.None && string.IsNullOrEmpty(Kind) && Properties.Count == 0 && Animation.Count == 0;
}
