using Stride.Core;

namespace ColorRise.Skein.Editor.Nodes;

/// <summary>
/// The headers of one Yarn node, as the property grid shows them. It is a view of the script's text, which stays the truth:
/// a change here is written back to the text, and a change of the text makes a new object.
/// </summary>
[DataContract]
public sealed class YarnNodeHeaders
{
    /// <summary>
    /// The node's title, which is its name in jumps and detours.
    /// </summary>
    [DataMember(10)]
    public string Title { get; set; } = "";

    /// <summary>
    /// The words of the <c>tags:</c> header.
    /// </summary>
    [DataMember(20)]
    public List<string> Tags { get; } = [];

    /// <summary>
    /// The <c>color:</c> header, which tints the node's card.
    /// </summary>
    [DataMember(30)]
    public string? Color { get; set; }

    /// <summary>
    /// The <c>cluster:</c> header, which frames the node with the others of the same cluster.
    /// </summary>
    [DataMember(40)]
    public string? Cluster { get; set; }

    /// <summary>
    /// The conditions of the <c>when:</c> headers, which make the node a member of a node group.
    /// </summary>
    [DataMember(50)]
    public List<string> When { get; } = [];

    /// <summary>
    /// The other headers, in the order of the text.
    /// </summary>
    [DataMember(60)]
    public List<YarnHeader> Others { get; } = [];
}

/// <summary>
/// One free <c>key: value</c> header of a Yarn node.
/// </summary>
[DataContract]
public sealed class YarnHeader
{
    /// <summary>
    /// The header's name.
    /// </summary>
    [DataMember(10)]
    public string Key { get; set; } = "";

    /// <summary>
    /// The header's text.
    /// </summary>
    [DataMember(20)]
    public string Value { get; set; } = "";
}
