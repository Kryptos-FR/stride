namespace ColorRise.Tessera.Rendering;

/// <summary>
/// The state sort key that orders the flipbooks of one depth by their y, so that the lower is drawn over the higher. The
/// sort modes put the top 24 bits of a render object's state key after its depth: the y is kept there, at 256 steps a unit,
/// from -32768 to 32768.
/// </summary>
internal static class FlipbookSortKey
{
    /// <summary>
    /// The steps of the key in a unit of y.
    /// </summary>
    public const float StepsPerUnit = 256;

    /// <summary>
    /// The y above which every key is 0; the y under -Range all have the largest key.
    /// </summary>
    public const float Range = 32768;

    private const double LargestStep = 16777215;

    /// <summary>
    /// The state sort key of a flipbook at a world y: smaller for a higher y, so that it is drawn first; 0 for a y that is not
    /// a number. The low byte, which the sort modes drop, is 0.
    /// </summary>
    public static uint FromY(float y)
    {
        if (float.IsNaN(y))
        {
            return 0;
        }
        return (uint)Math.Clamp(((double)Range - y) * StepsPerUnit, 0, LargestStep) << 8;
    }
}
