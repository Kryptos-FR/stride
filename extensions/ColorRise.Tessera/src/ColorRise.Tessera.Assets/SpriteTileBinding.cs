using Stride.Core;

namespace ColorRise.Tessera.Assets;

/// <summary>
/// The number that the tile of a sprite keeps, by the sprite's name.
/// </summary>
[DataContract]
public sealed class SpriteTileBinding
{
    [DataMember(10)]
    public int Number { get; set; }

    [DataMember(20)]
    public string SpriteName { get; set; } = "";
}
