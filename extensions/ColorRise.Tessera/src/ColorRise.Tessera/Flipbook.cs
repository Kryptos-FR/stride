using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Core.Serialization;
using Stride.Core.Serialization.Contents;
using Stride.Engine.Design;

namespace ColorRise.Tessera;

/// <summary>
/// Animations made of the tiles of a tile set, such as a character that walks in four directions: each animation has a
/// clip of frames for each direction it has.
/// </summary>
[DataContract]
[ContentSerializer(typeof(DataContentSerializer<Flipbook>))]
[ReferenceSerializer, DataSerializerGlobal(typeof(ReferenceSerializer<Flipbook>), Profile = "Content")]
[DataSerializerGlobal(typeof(CloneSerializer<Flipbook>), Profile = "Clone")]
public sealed class Flipbook
{
    /// <summary>
    /// The tile set whose tiles the frames show.
    /// </summary>
    public TileSet? TileSet { get; set; }

    /// <summary>
    /// The point of a frame that is on the entity, as a part of the size of the frame's first tile from its bottom-left
    /// corner: (0.5, 0) is the middle of its bottom edge, where a character's feet are.
    /// </summary>
    public Vector2 Pivot { get; set; } = new(0.5f, 0);

    /// <summary>
    /// The animations.
    /// </summary>
    public List<FlipbookAnimation> Animations { get; } = [];

    /// <summary>
    /// The first animation of a name, compared as it is written; null for a null name or a name the flipbook does not have.
    /// </summary>
    public FlipbookAnimation? FindAnimation(string? name)
    {
        if (name is null)
        {
            return null;
        }
        foreach (var animation in Animations)
        {
            if (animation.Name == name)
            {
                return animation;
            }
        }
        return null;
    }
}
