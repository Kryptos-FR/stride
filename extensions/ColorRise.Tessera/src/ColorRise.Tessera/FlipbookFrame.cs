using Stride.Core;

namespace ColorRise.Tessera;

/// <summary>
/// One picture of a flipbook animation: one or more tiles, shown for a time.
/// </summary>
[DataContract("FlipbookFrame")]
public sealed class FlipbookFrame
{
    /// <summary>
    /// How long the frame is shown, in seconds. A frame whose duration is not above 0 is never shown.
    /// </summary>
    /// <userdoc>How long the frame is shown, in seconds.</userdoc>
    [DataMember(10)]
    public float Duration { get; set; } = 0.1f;

    /// <summary>
    /// The tiles of the frame; the first one is where the flipbook's pivot is measured from.
    /// </summary>
    /// <userdoc>The tiles of the frame, each at its offset from the first.</userdoc>
    [DataMember(20)]
    public List<FlipbookCell> Cells { get; } = [];
}
