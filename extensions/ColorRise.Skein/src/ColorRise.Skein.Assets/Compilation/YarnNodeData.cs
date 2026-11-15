namespace ColorRise.Skein.Assets.Compilation;

/// <summary>
/// A node of a script, as the compiler read it. Lines count from 1.
/// </summary>
public sealed record YarnNodeData
{
    /// <summary>
    /// The title the script gives the node. The nodes of a node group share it.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// The name of the node in the program: the title, or for a node of a node group the unique name the compiler made.
    /// </summary>
    public required string UniqueTitle { get; init; }

    /// <summary>
    /// The name of the file, as the request gave it.
    /// </summary>
    public required string File { get; init; }

    /// <summary>
    /// The headers in the order of the text, the title among them. A <c>when:</c> header gives its condition as written.
    /// </summary>
    public IReadOnlyList<YarnHeaderData> Headers { get; init; } = [];

    /// <summary>
    /// The words of the <c>tags:</c> header.
    /// </summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>
    /// The node group the node is part of (it has a <c>when:</c> header), or null.
    /// </summary>
    public string? NodeGroup { get; init; }

    /// <summary>
    /// How specific the node's <c>when:</c> conditions are, what saliency strategies compare; -1 outside of a node group.
    /// </summary>
    public int Complexity { get; init; } = -1;

    /// <summary>
    /// The names of the characters that speak in the node, in the order they first do.
    /// </summary>
    public IReadOnlyList<string> Speakers { get; init; } = [];

    /// <summary>
    /// The first lines of the node, for a card of the graph.
    /// </summary>
    public string Preview { get; init; } = "";

    /// <summary>
    /// The options of the node, in the order of the text.
    /// </summary>
    public IReadOnlyList<YarnOptionData> Options { get; init; } = [];

    /// <summary>
    /// The jumps and detours to nodes named in the text, in the order of the text. A jump to an expression is not one.
    /// </summary>
    public IReadOnlyList<YarnJumpData> Jumps { get; init; } = [];

    /// <summary>
    /// The line of the node's first header.
    /// </summary>
    public required int HeaderStartLine { get; init; }

    /// <summary>
    /// The line after the <c>---</c> that ends the headers: the first line of the body.
    /// </summary>
    public required int BodyStartLine { get; init; }

    /// <summary>
    /// The line of the <c>===</c> that ends the node: the line after the body's last. For a node the text doesn't close,
    /// the last line the node reaches.
    /// </summary>
    public required int BodyEndLine { get; init; }
}

/// <summary>
/// A header of a node: <c>key: value</c>.
/// </summary>
public readonly record struct YarnHeaderData(string Key, string Value);

/// <summary>
/// A jump or a detour from a node to the node it names.
/// </summary>
/// <param name="Target">The title the jump names, whether a node has it or not.</param>
/// <param name="IsDetour">Whether the dialogue comes back after the target ends.</param>
/// <param name="Line">The line of the jump, from 1.</param>
/// <param name="Column">The column of the target's name, from 1.</param>
public readonly record struct YarnJumpData(string Target, bool IsDetour, int Line, int Column);

/// <summary>
/// An option of a node.
/// </summary>
/// <param name="Text">The text of the option.</param>
/// <param name="Line">The line of the option, from 1.</param>
/// <param name="Group">Which run of options of the node it belongs to: options offered together share it.</param>
public readonly record struct YarnOptionData(string Text, int Line, int Group);
