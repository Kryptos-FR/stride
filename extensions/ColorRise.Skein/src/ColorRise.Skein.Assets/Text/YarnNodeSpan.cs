using System.Globalization;

namespace ColorRise.Skein.Assets.Text;

/// <summary>
/// One header line of a node: where it is in the text, and what it says.
/// </summary>
/// <param name="Key">The name before the colon.</param>
/// <param name="Value">The text after the colon, without the spaces around it and without a comment.</param>
/// <param name="LineStart">The place of the first character of the line.</param>
/// <param name="LineEnd">The place after the line and its line break.</param>
/// <param name="ValueStart">The place of the value; where a value would go when there is none.</param>
public readonly record struct YarnHeaderSpan(string Key, string Value, int LineStart, int LineEnd, int ValueStart);

/// <summary>
/// One node of a script, as the places of its parts in the text.
/// </summary>
public sealed record YarnNodeSpan
{
    /// <summary>
    /// The value of the <c>title:</c> header; empty when the node has none yet.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// The rank of the node among the nodes of the same title in the file, from 0: the members of a node group share a title.
    /// </summary>
    public required int Index { get; init; }

    /// <summary>
    /// The header lines, in the order of the text.
    /// </summary>
    public required IReadOnlyList<YarnHeaderSpan> Headers { get; init; }

    /// <summary>
    /// The line of the first header, from 1: what the compiler calls the start of the node.
    /// </summary>
    public required int Line { get; init; }

    /// <summary>
    /// The place of the first character of the first header line.
    /// </summary>
    public required int HeaderStart { get; init; }

    /// <summary>
    /// The place of the <c>---</c> line, or -1 when the node has none yet.
    /// </summary>
    public required int DelimiterStart { get; init; }

    /// <summary>
    /// The place of the first line of the body. Equal to <see cref="BodyEnd"/> when the body is empty.
    /// </summary>
    public required int BodyStart { get; init; }

    /// <summary>
    /// The place of the <c>===</c> line; the end of the text when the node is not closed yet.
    /// </summary>
    public required int BodyEnd { get; init; }

    /// <summary>
    /// The place after the <c>===</c> line and its line break.
    /// </summary>
    public required int End { get; init; }

    /// <summary>
    /// Whether the node has its <c>===</c> line.
    /// </summary>
    public required bool IsClosed { get; init; }

    /// <summary>
    /// The value of the first header of a name, or null.
    /// </summary>
    public string? Header(string key)
    {
        foreach (var header in Headers)
        {
            if (header.Key == key)
                return header.Value;
        }

        return null;
    }

    /// <summary>
    /// The place of the node on the graph, from its <c>position:</c> header; null without one that reads as two numbers.
    /// </summary>
    public (int X, int Y)? Position
    {
        get
        {
            var parts = Header("position")?.Split(',');
            if (parts is { Length: 2 }
                && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
                && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y)
                && double.IsFinite(x) && double.IsFinite(y))
            {
                return ((int)Math.Round(x), (int)Math.Round(y));
            }

            return null;
        }
    }
}
