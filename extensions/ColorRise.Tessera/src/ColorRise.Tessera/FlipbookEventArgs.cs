namespace ColorRise.Tessera;

/// <summary>
/// What a flipbook component shows when it raises an event.
/// </summary>
/// <param name="animation">The name of the animation.</param>
/// <param name="direction">The direction of the clip shown, which a fallback can make differ from the component's.</param>
/// <param name="frameIndex">The index of the frame in its clip.</param>
public sealed class FlipbookEventArgs(string animation, FlipbookDirection direction, int frameIndex) : EventArgs
{
    /// <summary>
    /// The name of the animation.
    /// </summary>
    public string Animation { get; } = animation;

    /// <summary>
    /// The direction of the clip shown, which a fallback can make differ from the component's.
    /// </summary>
    public FlipbookDirection Direction { get; } = direction;

    /// <summary>
    /// The index of the frame in its clip.
    /// </summary>
    public int FrameIndex { get; } = frameIndex;
}
