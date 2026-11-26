namespace Thornwick;

/// <summary>
/// The time a frame of the game counts for, so that a long frame moves nothing more than one short one does.
/// </summary>
public static class FrameTime
{
    /// <summary>
    /// The longest time a frame counts for, in seconds.
    /// </summary>
    public const float Longest = 0.1f;

    /// <summary>
    /// The seconds a frame of the game counts for: its time, 0 when it is negative, <see cref="Longest"/> at most.
    /// </summary>
    public static float Of(TimeSpan elapsed) => Math.Clamp((float)elapsed.TotalSeconds, 0, Longest);
}
