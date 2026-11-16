namespace ColorRise.Skein.Editor.Graph;

/// <summary>
/// What the graph of a script shows as a node: the card of a node, or the stub of a node of another script.
/// </summary>
public interface IYarnGraphNode
{
    /// <summary>
    /// The title of the node.
    /// </summary>
    string Title { get; }

    /// <summary>
    /// Where the top left corner of the node is on the graph.
    /// </summary>
    YarnGraphPoint Position { get; }
}
