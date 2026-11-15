namespace ColorRise.Skein.Editor.Graph;

/// <summary>
/// What the player did with a node in the run that is open.
/// </summary>
public enum YarnPlayMark
{
    /// <summary>
    /// The run did not reach the node, or no run is open.
    /// </summary>
    None,

    /// <summary>
    /// The run went through the node.
    /// </summary>
    Visited,

    /// <summary>
    /// The run is in the node.
    /// </summary>
    Current,
}
