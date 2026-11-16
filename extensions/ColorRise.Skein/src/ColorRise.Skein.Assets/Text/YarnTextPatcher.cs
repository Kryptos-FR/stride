using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ColorRise.Skein.Assets.Text;

/// <summary>
/// The changes the graph and the property grid make to a script, as edits of its text: each one changes only the lines
/// it is about, and every other character of the file stays.
/// </summary>
/// <remarks>
/// A node given to an operation is one of the document given with it. New lines end with the line break of the file.
/// </remarks>
public static partial class YarnTextPatcher
{
    /// <summary>
    /// Gives a header its value: the first header of that name keeps its line and takes the value; without one, a new
    /// line goes after the last header.
    /// </summary>
    /// <exception cref="ArgumentException">The key or the value cannot be written as a header.</exception>
    public static IReadOnlyList<YarnTextEdit> SetHeader(YarnDocument document, YarnNodeSpan node, string key, string value)
    {
        Check(document, node);
        CheckHeader(key, value);

        foreach (var header in node.Headers)
        {
            if (header.Key == key)
                return header.Value == value ? [] : [ReplaceValue(document, header, value)];
        }

        return [InsertHeaders(document, node, node.Headers.Count, [(key, value)])];
    }

    /// <summary>
    /// Gives the headers of one name their values, for a name a node may carry several times (<c>when:</c>): the lines
    /// that are there take the values in order, lines in excess go, and lines in want come after the last of the name.
    /// </summary>
    /// <exception cref="ArgumentException">The key or a value cannot be written as a header.</exception>
    public static IReadOnlyList<YarnTextEdit> SetHeaders(YarnDocument document, YarnNodeSpan node, string key, IReadOnlyList<string> values)
    {
        Check(document, node);
        ArgumentNullException.ThrowIfNull(values);
        foreach (var value in values)
        {
            CheckHeader(key, value);
        }

        var edits = new List<YarnTextEdit>();
        var used = 0;
        var after = node.Headers.Count;
        for (var i = 0; i < node.Headers.Count; i++)
        {
            var header = node.Headers[i];
            if (header.Key != key)
                continue;

            if (used < values.Count)
            {
                if (header.Value != values[used])
                {
                    edits.Add(ReplaceValue(document, header, values[used]));
                }

                used++;
                after = i + 1;
            }
            else
            {
                edits.Add(new YarnTextEdit(header.LineStart, header.LineEnd - header.LineStart, ""));
            }
        }

        if (used < values.Count)
        {
            edits.Add(InsertHeaders(document, node, after, [.. values.Skip(used).Select(v => (key, v))]));
            edits.Sort((a, b) => a.Offset.CompareTo(b.Offset));
        }

        return edits;
    }

    /// <summary>
    /// Removes every header of a name, with its line.
    /// </summary>
    public static IReadOnlyList<YarnTextEdit> RemoveHeader(YarnDocument document, YarnNodeSpan node, string key)
    {
        Check(document, node);
        ArgumentNullException.ThrowIfNull(key);

        return [.. node.Headers.Where(h => h.Key == key).Select(h => new YarnTextEdit(h.LineStart, h.LineEnd - h.LineStart, ""))];
    }

    /// <summary>
    /// Writes the place of a node on the graph: its <c>position:</c> header takes the value; without one, a new line
    /// goes after the title.
    /// </summary>
    public static IReadOnlyList<YarnTextEdit> SetPosition(YarnDocument document, YarnNodeSpan node, int x, int y)
    {
        Check(document, node);

        var value = PositionText(x, y);
        var titleIndex = -1;
        for (var i = 0; i < node.Headers.Count; i++)
        {
            var header = node.Headers[i];
            if (header.Key == "position")
                return header.Value == value ? [] : [ReplaceValue(document, header, value)];
            if (header.Key == "title" && titleIndex < 0)
                titleIndex = i;
        }

        return [InsertHeaders(document, node, titleIndex + 1, [("position", value)])];
    }

    /// <summary>
    /// Adds a node with an empty body at the end of the file.
    /// </summary>
    /// <param name="headers">Headers to write after the position, in order; null for none.</param>
    /// <exception cref="ArgumentException">A key or a value cannot be written as a header.</exception>
    public static IReadOnlyList<YarnTextEdit> AddNode(YarnDocument document, string title, int x, int y, IReadOnlyList<(string Key, string Value)>? headers = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        CheckHeader("title", title);

        var newLine = document.NewLine;
        var text = new StringBuilder();
        if (!IsLineStart(document.Text, document.Text.Length))
        {
            text.Append(newLine);
        }

        // A last node still being typed is closed first: the new node is not a part of its body
        if (document.Nodes is [.., { IsClosed: false }])
        {
            text.Append("===").Append(newLine);
        }

        text.Append("title: ").Append(title).Append(newLine);
        text.Append("position: ").Append(PositionText(x, y)).Append(newLine);
        foreach (var (key, value) in headers ?? [])
        {
            CheckHeader(key, value);
            text.Append(key).Append(": ").Append(value).Append(newLine);
        }

        text.Append("---").Append(newLine).Append("===").Append(newLine);
        return [new YarnTextEdit(document.Text.Length, 0, text.ToString())];
    }

    /// <summary>
    /// Removes a node, with the blank lines that follow it.
    /// </summary>
    public static IReadOnlyList<YarnTextEdit> DeleteNode(YarnDocument document, YarnNodeSpan node)
    {
        Check(document, node);

        var text = document.Text;
        var end = node.End;
        while (end < text.Length)
        {
            var lineEnd = end;
            while (lineEnd < text.Length && text[lineEnd] is ' ' or '\t')
            {
                lineEnd++;
            }

            if (lineEnd < text.Length && text[lineEnd] is not ('\r' or '\n'))
                break;

            end = lineEnd == text.Length ? lineEnd : AfterLineBreak(text, lineEnd);
        }

        return [new YarnTextEdit(node.HeaderStart, end - node.HeaderStart, "")];
    }

    /// <summary>
    /// Adds a copy of a node after it, under another title. The lines of the copy have no line id: an id is of one line.
    /// </summary>
    /// <param name="position">The place of the copy on the graph; null to keep the one of the node.</param>
    /// <exception cref="ArgumentException">The title cannot be written as a header.</exception>
    public static IReadOnlyList<YarnTextEdit> DuplicateNode(YarnDocument document, YarnNodeSpan node, string newTitle, (int X, int Y)? position = null)
    {
        Check(document, node);
        CheckHeader("title", newTitle);

        var newLine = document.NewLine;
        var prefix = IsLineStart(document.Text, node.End) ? "" : newLine;
        var copy = document.Text[node.HeaderStart..node.End];
        if (!node.IsClosed)
        {
            // A node still being typed is closed first, and its copy too
            prefix += "===" + newLine;
            copy += prefix;
        }

        // The copy is patched as a file of its own, with the same operations
        var parsed = YarnDocument.Parse(copy);
        copy = YarnTextEdit.Apply(copy, RenameNode(parsed, parsed.Nodes[0], newTitle));
        if (position is var (x, y))
        {
            parsed = YarnDocument.Parse(copy);
            copy = YarnTextEdit.Apply(copy, SetPosition(parsed, parsed.Nodes[0], x, y));
        }

        var body = YarnDocument.Parse(copy).Nodes[0];
        copy = copy[..body.BodyStart] + WithoutLineIds(copy, body.BodyStart, body.BodyEnd) + copy[body.BodyEnd..];

        return [new YarnTextEdit(node.End, 0, prefix + copy)];
    }

    /// <summary>
    /// Adds <c>&lt;&lt;jump target&gt;&gt;</c> as the last line of a node's body.
    /// </summary>
    /// <param name="detour">Whether to add <c>&lt;&lt;detour target&gt;&gt;</c> instead: the dialogue comes back once the target ends.</param>
    public static IReadOnlyList<YarnTextEdit> AppendJump(YarnDocument document, YarnNodeSpan node, string target, bool detour = false)
    {
        Check(document, node);
        ArgumentException.ThrowIfNullOrEmpty(target);

        var newLine = document.NewLine;
        var text = new StringBuilder();
        if (!IsLineStart(document.Text, node.BodyEnd))
        {
            text.Append(newLine);
        }

        if (node.DelimiterStart < 0)
        {
            text.Append("---").Append(newLine);
        }

        text.Append(detour ? "<<detour " : "<<jump ").Append(target).Append(">>").Append(newLine);
        return [new YarnTextEdit(node.BodyEnd, 0, text.ToString())];
    }

    /// <summary>
    /// Gives a node another title. The jumps to the node are <see cref="RenameJumps"/>'s to change.
    /// </summary>
    /// <exception cref="ArgumentException">The title cannot be written as a header.</exception>
    public static IReadOnlyList<YarnTextEdit> RenameNode(YarnDocument document, YarnNodeSpan node, string newTitle)
    {
        Check(document, node);
        CheckHeader("title", newTitle);

        foreach (var header in node.Headers)
        {
            if (header.Key == "title")
                return header.Value == newTitle ? [] : [ReplaceValue(document, header, newTitle)];
        }

        return [InsertHeaders(document, node, 0, [("title", newTitle)])];
    }

    /// <summary>
    /// Makes what names a node in a file name another: every <c>&lt;&lt;jump&gt;&gt;</c> and <c>&lt;&lt;detour&gt;&gt;</c>
    /// to it, and the <c>visited</c>, <c>visited_count</c> and <c>has_any_content</c> calls about it in commands, in
    /// <c>{}</c> and in <c>when:</c> headers. Comments and the text of lines stay.
    /// </summary>
    public static IReadOnlyList<YarnTextEdit> RenameJumps(YarnDocument document, string oldTitle, string newTitle)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrEmpty(oldTitle);
        ArgumentException.ThrowIfNullOrEmpty(newTitle);

        var text = document.Text;
        var edits = new List<YarnTextEdit>();
        foreach (var node in document.Nodes)
        {
            foreach (var header in node.Headers)
            {
                if (header.Key == "when")
                {
                    RenameCalls(header.ValueStart, header.ValueStart + header.Value.Length);
                }
            }

            var start = node.BodyStart;
            while (start < node.BodyEnd)
            {
                var end = text.AsSpan(start, node.BodyEnd - start).IndexOfAny('\r', '\n');
                end = end < 0 ? node.BodyEnd : start + end;
                var contentEnd = YarnDocument.CommentStart(text, start, end);

                // What is between << >> or { } is code; the rest of a line is its text
                foreach (var code in CodeRegex().EnumerateMatches(text.AsSpan(start, contentEnd - start)))
                {
                    var codeStart = start + code.Index;
                    var jump = JumpRegex().Match(text, codeStart, code.Length);
                    if (jump.Success && jump.Groups["target"].Value == oldTitle)
                    {
                        edits.Add(new YarnTextEdit(jump.Groups["target"].Index, oldTitle.Length, newTitle));
                    }
                    else
                    {
                        RenameCalls(codeStart, codeStart + code.Length);
                    }
                }

                start = end < node.BodyEnd ? AfterLineBreak(text, end) : end;
            }
        }

        return edits;

        void RenameCalls(int from, int to)
        {
            foreach (Match call in CallRegex().Matches(text[from..to]))
            {
                var title = call.Groups["title"];
                if (title.Value == oldTitle)
                {
                    edits.Add(new YarnTextEdit(from + title.Index, oldTitle.Length, newTitle));
                }
            }
        }
    }

    private static void Check(YarnDocument document, YarnNodeSpan node)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(node);
        if (!document.Nodes.Any(n => ReferenceEquals(n, node)))
            throw new ArgumentException("The node is not of this document: parse the text again after each change.", nameof(node));
    }

    private static void CheckHeader(string key, string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(value);
        if (key.Any(c => char.IsWhiteSpace(c) || c == ':'))
            throw new ArgumentException($"'{key}' cannot name a header: a name is one word.", nameof(key));
        if (value.AsSpan().IndexOfAny('\r', '\n') >= 0)
            throw new ArgumentException($"The value of the header '{key}' must be one line.", nameof(value));
    }

    private static string PositionText(int x, int y) => string.Create(CultureInfo.InvariantCulture, $"{x},{y}");

    private static YarnTextEdit ReplaceValue(YarnDocument document, YarnHeaderSpan header, string value)
    {
        if (header.Value.Length > 0 || value.Length == 0)
            return new YarnTextEdit(header.ValueStart, header.Value.Length, value);

        // "color:" with nothing after the colon: the value comes after one space, whatever spaces were there
        var text = document.Text;
        var end = header.ValueStart;
        while (end < text.Length && text[end] is ' ' or '\t')
        {
            end++;
        }

        var commentFollows = end < text.Length && text[end] is not ('\r' or '\n');
        return new YarnTextEdit(header.ValueStart, end - header.ValueStart, " " + value + (commentFollows ? " " : ""));
    }

    // New header lines, before the header of a rank; a rank past the last header puts them after it
    private static YarnTextEdit InsertHeaders(YarnDocument document, YarnNodeSpan node, int index, IReadOnlyList<(string Key, string Value)> headers)
    {
        var newLine = document.NewLine;
        var text = new StringBuilder();
        int offset;
        if (index < node.Headers.Count)
        {
            offset = node.Headers[index].LineStart;
        }
        else
        {
            offset = node.Headers.Count > 0 ? node.Headers[^1].LineEnd : node.HeaderStart;

            // The last header of a file being typed may have no line break yet
            if (!IsLineStart(document.Text, offset))
            {
                text.Append(newLine);
            }
        }

        foreach (var (key, value) in headers)
        {
            text.Append(key).Append(": ").Append(value).Append(newLine);
        }

        return new YarnTextEdit(offset, 0, text.ToString());
    }

    private static bool IsLineStart(string text, int offset) => offset == 0 || text[offset - 1] is '\r' or '\n' or YarnDocument.ByteOrderMark;

    private static int AfterLineBreak(string text, int offset)
        => text[offset] == '\r' && offset + 1 < text.Length && text[offset + 1] == '\n' ? offset + 2 : offset + 1;

    private static string WithoutLineIds(string text, int start, int end)
    {
        var result = new StringBuilder();
        while (start < end)
        {
            var lineEnd = text.AsSpan(start, end - start).IndexOfAny('\r', '\n');
            lineEnd = lineEnd < 0 ? end : start + lineEnd;
            var contentEnd = YarnDocument.CommentStart(text, start, lineEnd);
            result.Append(LineIdRegex().Replace(text[start..contentEnd], ""));

            start = lineEnd < end ? AfterLineBreak(text, lineEnd) : lineEnd;
            result.Append(text, contentEnd, start - contentEnd);
        }

        return result.ToString();
    }

    [GeneratedRegex(@"<<.*?>>|\{.*?\}")]
    private static partial Regex CodeRegex();

    [GeneratedRegex(@"\G<<\s*(?:jump|detour)\s+(?<target>[^\s{}<>]+)\s*>>")]
    private static partial Regex JumpRegex();

    [GeneratedRegex(@"\b(?:visited|visited_count|has_any_content)\s*\(\s*""(?<title>[^""]*)""\s*\)")]
    private static partial Regex CallRegex();

    [GeneratedRegex(@"[ \t]*(?<!\\)#line:[^\s#]+")]
    private static partial Regex LineIdRegex();
}
