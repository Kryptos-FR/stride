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
    /// <summary>
    /// The id of the tile in its set.
    /// </summary>
    public TileId Id { get; set; }

    /// <summary>
    /// The texture the tile's pixels are in.
    /// </summary>
    public Texture? Texture { get; set; }

    /// <summary>
    /// The tile's pixels in the texture, without the border around them.
    /// </summary>
    public Rectangle Region { get; set; }

    /// <summary>
    /// Whether the tile stops what moves on the map.
    /// </summary>
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

    /// <summary>
    /// Whether the tile has the solid flag.
    /// </summary>
    public bool IsSolid => (Collision & TileCollision.Solid) != 0;

    /// <summary>
    /// The tile shown at a time, in seconds: the frames loop, a negative time counts back from the end of the loop, and a
    /// frame whose duration is not above 0 is not shown.
    /// </summary>
    /// <returns>The tile itself when it has no frames; the first frame's when no frame lasts, or the time is not finite.</returns>
    public TileId FrameAt(double time) => FrameAt(Animation, Id, time);

    /// <summary>
    /// The tile shown at a time by a tile with these frames, as <see cref="FrameAt(double)"/> finds it: for the editors,
    /// which have the frames of a tile's data and no <see cref="Tile"/>.
    /// </summary>
    /// <param name="frames">The frames of the tile.</param>
    /// <param name="own">The tile itself, shown when it has no frames.</param>
    /// <param name="time">In seconds.</param>
    public static TileId FrameAt(IReadOnlyList<TileAnimationFrame> frames, TileId own, double time)
    {
        ArgumentNullException.ThrowIfNull(frames);
        var index = FrameTimeline.IndexAt(frames, static frame => frame.Duration, time, FlipbookLoopMode.Loop);
        return index < 0 ? own : frames[index].Tile;
    }
}
