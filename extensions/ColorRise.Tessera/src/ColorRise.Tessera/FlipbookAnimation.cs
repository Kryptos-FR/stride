using Stride.Core;

namespace ColorRise.Tessera;

/// <summary>
/// A named animation of a flipbook, such as <c>walk</c>, with a clip for each direction it has.
/// </summary>
[DataContract("FlipbookAnimation")]
public sealed class FlipbookAnimation
{
    /// <summary>
    /// The name game code plays it by, compared as it is written.
    /// </summary>
    [DataMember(10)]
    public string Name { get; set; } = "";

    /// <summary>
    /// What the animation does at the end of its frames.
    /// </summary>
    /// <userdoc>What the animation does at the end of its frames: start again, stop on the last, or play back and forth.</userdoc>
    [DataMember(20)]
    [Display("Loop mode")]
    public FlipbookLoopMode LoopMode { get; set; }

    /// <summary>
    /// The clips, one for each direction the animation has.
    /// </summary>
    [DataMember(30)]
    public List<FlipbookClip> Clips { get; } = [];

    /// <summary>
    /// The clip of a direction; else, for a diagonal, the clip of its horizontal part, then of its vertical part; else the
    /// clip <see cref="FlipbookDirection.None"/>; else the first clip; null without a clip. Of two clips of one direction, the
    /// first.
    /// </summary>
    public FlipbookClip? FindClip(FlipbookDirection direction)
    {
        if (Clips.Count == 0)
        {
            return null;
        }
        return Find(direction)
            ?? (direction.HorizontalPart() is { } horizontal ? Find(horizontal) : null)
            ?? (direction.VerticalPart() is { } vertical ? Find(vertical) : null)
            ?? Find(FlipbookDirection.None)
            ?? Clips[0];
    }

    private FlipbookClip? Find(FlipbookDirection direction)
    {
        foreach (var clip in Clips)
        {
            if (clip.Direction == direction)
            {
                return clip;
            }
        }
        return null;
    }
}
