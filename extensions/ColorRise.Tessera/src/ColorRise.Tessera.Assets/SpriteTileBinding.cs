using Stride.Core;

namespace ColorRise.Tessera.Assets;

/// <summary>
/// The number that the tile of a sprite keeps, by the sprite's name.
/// </summary>
[DataContract]
public sealed class SpriteTileBinding
{
    /// <summary>
    /// The number of the sprite's tile, which the tile's id keeps.
    /// </summary>
    [DataMember(10)]
    public int Number { get; set; }

    /// <summary>
    /// The name of the sprite in the sheet.
    /// </summary>
    [DataMember(20)]
    public string SpriteName { get; set; } = "";
}
