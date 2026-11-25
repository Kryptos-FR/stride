using Stride.Core;

namespace ColorRise.Tessera;

/// <summary>
/// The frames of a flipbook animation for one direction.
/// </summary>
[DataContract("FlipbookClip")]
public sealed class FlipbookClip
{
    /// <summary>
    /// The direction the clip is for; <see cref="FlipbookDirection.None"/> for any direction the animation has no clip for.
    /// </summary>
    /// <userdoc>The direction the clip is for. "None" is shown for any direction the animation has no clip for.</userdoc>
    [DataMember(10)]
    public FlipbookDirection Direction { get; set; }

    /// <summary>
    /// The frames, in the order they are shown.
    /// </summary>
    [DataMember(20)]
    public List<FlipbookFrame> Frames { get; } = [];

    /// <summary>
    /// How long one pass lasts, in seconds: the durations above 0 added up; for <see cref="FlipbookLoopMode.PingPong"/>, there
    /// and back, each end frame once.
    /// </summary>
    public double GetPassLength(FlipbookLoopMode mode) => FrameTimeline.PassLength(Frames, static frame => frame.Duration, mode);

    /// <summary>
    /// The index in <see cref="Frames"/> of the frame shown at a time, in seconds: -1 without a frame; the first frame when
    /// no frame lasts or the time is not finite.
    /// </summary>
    public int FrameAt(double time, FlipbookLoopMode mode) => FrameTimeline.IndexAt(Frames, static frame => frame.Duration, time, mode);
}
