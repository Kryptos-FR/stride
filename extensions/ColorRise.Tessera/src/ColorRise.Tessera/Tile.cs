using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Graphics;

namespace ColorRise.Tessera;

/// <summary>
/// One tile of a tile set: where its pixels are, and what a game knows of it.
/// </summary>
[DataContract]
public sealed class Tile
{
    public TileId Id { get; set; }

    public Texture? Texture { get; set; }

    /// <summary>
    /// The tile's pixels in the texture, without the border around them.
    /// </summary>
    public Rectangle Region { get; set; }

    public TileCollision Collision { get; set; }

    /// <summary>
    /// What the tile is to the game, such as <c>water</c> or <c>door</c>, if anything.
    /// </summary>
    public string? Kind { get; set; }

    /// <summary>
    /// The game's own values for the tile: each one a bool, an int, a float, a string or a <see cref="Color"/>.
    /// </summary>
    public Dictionary<string, object> Properties { get; } = [];

    /// <summary>
    /// The frames the tile shows in turn, none for a tile that does not move.
    /// </summary>
    public List<TileAnimationFrame> Animation { get; } = [];

    public bool IsSolid => (Collision & TileCollision.Solid) != 0;

    /// <summary>
    /// The tile shown at a time, in seconds.
    /// </summary>
    public TileId FrameAt(double time) => Id;
}
