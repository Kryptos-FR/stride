namespace ColorRise.Tessera;

/// <summary>
/// What a flipbook animation does at the end of its frames.
/// </summary>
public enum FlipbookLoopMode
{
    /// <summary>
    /// Starts again from the first frame.
    /// </summary>
    Loop,
    /// <summary>
    /// Stops on the last frame.
    /// </summary>
    Once,
    /// <summary>
    /// Plays back to the first frame, then forward again: 1, 2, 3, 2, 1, 2, ... The two end frames are shown once a pass.
    /// </summary>
    PingPong,
}
