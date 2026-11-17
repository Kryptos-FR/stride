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
    /// Adds <c>&lt;&lt;jump target&gt;&gt;</c> as the last line of what an option, a line group item or a branch of a
    /// block holds, indented as what it holds.
    /// </summary>
    /// <param name="branch">A statement of the node that <see cref="YarnStatementSpan.IsBranch"/>.</param>
    /// <param name="detour">Whether to add <c>&lt;&lt;detour target&gt;&gt;</c> instead: the dialogue comes back once the target ends.</param>
    public static IReadOnlyList<YarnTextEdit> AppendJump(YarnDocument document, YarnNodeSpan node, YarnStatementSpan branch, string target, bool detour = false)
    {
        var statements = Check(document, node, branch);
        ArgumentException.ThrowIfNullOrEmpty(target);
        if (!branch.IsBranch)
            throw new ArgumentException("A jump goes under an option, a line group item or a branch of a block.", nameof(branch));

        var index = IndexOf(statements, branch);
        var last = LastWithin(statements, index);
        var indentation = last > index ? statements[index + 1].Indentation : Deeper(branch.Indentation);
        var offset = statements[last].LineEnd;
        var text = (IsLineStart(document.Text, offset) ? "" : document.NewLine) + indentation + (detour ? "<<detour " : "<<jump ") + target + ">>" + document.NewLine;
        return [new YarnTextEdit(offset, 0, text)];
    }

    /// <summary>
    /// Gives a line, an option or a line group item another text. Its condition, its hashtags (its line id among them)
    /// and its comment stay.
    /// </summary>
    /// <param name="text">The text as the user wrote it: the characters that would end it (<c>#</c>, <c>//</c>,
    /// <c>&lt;&lt;</c>) are escaped.</param>
    /// <exception cref="ArgumentException">The text cannot be the text of the statement.</exception>
    public static IReadOnlyList<YarnTextEdit> SetStatementText(YarnDocument document, YarnNodeSpan node, YarnStatementSpan statement, string text)
    {
        Check(document, node, statement);
        if (!statement.IsText)
            throw new ArgumentException("Only a line, an option or a line group item has a text to edit.", nameof(statement));

        var written = TextOfStatement(text);
        if (written == statement.Text)
            return [];

        // A condition or a hashtag right after the text is kept apart from it
        var source = document.Text;
        var apart = statement.TextEnd < statement.LineEnd && source[statement.TextEnd] is not (' ' or '\t' or '\r' or '\n') ? " " : "";
        return [new YarnTextEdit(statement.TextStart, statement.TextEnd - statement.TextStart, written + apart)];
    }

    /// <summary>
    /// Removes a statement that holds nothing, with its line.
    /// </summary>
    /// <exception cref="ArgumentException">Other statements are in it.</exception>
    public static IReadOnlyList<YarnTextEdit> DeleteStatement(YarnDocument document, YarnNodeSpan node, YarnStatementSpan statement)
    {
        var statements = Check(document, node, statement);
        var index = IndexOf(statements, statement);
        if (LastWithin(statements, index) > index || statement.Kind is YarnStatementKind.If or YarnStatementKind.Once)
            throw new ArgumentException("The statement holds others: remove them in the text.", nameof(statement));

        return [new YarnTextEdit(statement.LineStart, statement.LineEnd - statement.LineStart, "")];
    }

    /// <summary>
    /// Gives a line, an option or a line group item another condition, or none: <c>&lt;&lt;if ...&gt;&gt;</c>,
    /// <c>&lt;&lt;once&gt;&gt;</c> or <c>&lt;&lt;once if ...&gt;&gt;</c> after its text. Its text and its hashtags stay.
    /// </summary>
    /// <param name="condition">The condition, as Yarn writes it; null or empty for none.</param>
    /// <param name="once">Whether the statement runs one time only.</param>
    /// <exception cref="ArgumentException">The statement is not text, or the condition cannot be in a command.</exception>
    public static IReadOnlyList<YarnTextEdit> SetStatementCondition(YarnDocument document, YarnNodeSpan node, YarnStatementSpan statement, string? condition, bool once)
    {
        Check(document, node, statement);
        if (!statement.IsText)
            throw new ArgumentException("Only a line, an option or a line group item has a condition after it.", nameof(statement));

        condition = string.IsNullOrWhiteSpace(condition) ? null : CommandText(condition, nameof(condition));
        if (condition == statement.Condition && once == statement.IsOnce)
            return [];

        var command = (once, condition) switch
        {
            (true, null) => "<<once>>",
            (true, { } c) => $"<<once if {c}>>",
            (false, { } c) => $"<<if {c}>>",
            _ => null,
        };

        if (statement.ConditionStart < 0)
            return command is null ? [] : [new YarnTextEdit(statement.TextEnd, 0, " " + command)];

        // Without a condition, the spaces before it go too
        return command is null
            ? [new YarnTextEdit(statement.TextEnd, statement.ConditionEnd - statement.TextEnd, "")]
            : [new YarnTextEdit(statement.ConditionStart, statement.ConditionEnd - statement.ConditionStart, command)];
    }

    /// <summary>
    /// Gives a command another text: what is between its <c>&lt;&lt;</c> and its <c>&gt;&gt;</c>.
    /// </summary>
    /// <param name="text">The command, as Yarn writes it: <c>set $met to true</c>.</param>
    /// <exception cref="ArgumentException">The statement is not a command, or the text cannot be one.</exception>
    public static IReadOnlyList<YarnTextEdit> SetCommandText(YarnDocument document, YarnNodeSpan node, YarnStatementSpan statement, string text)
    {
        Check(document, node, statement);
        if (statement.Kind != YarnStatementKind.Command)
            throw new ArgumentException("Only a command has a text to edit this way.", nameof(statement));

        var written = CommandText(text, nameof(text));
        return written == statement.Text ? [] : [new YarnTextEdit(statement.TextStart, statement.TextEnd - statement.TextStart, written)];
    }

    // What can be between << and >>: one line, without a >> of its own
    private static string CommandText(string text, string parameter)
    {
        var written = text.Trim();
        if (written.Length == 0)
            throw new ArgumentException("A command needs a text.", parameter);
        if (written.Contains('\n') || written.Contains('\r'))
            throw new ArgumentException("A command is one line.", parameter);
        if (written.Contains(">>", StringComparison.Ordinal))
            throw new ArgumentException("'>>' would end the command.", parameter);

        return written;
    }

    /// <summary>
    /// Adds a line at the end of a node: before the jumps and the <c>&lt;&lt;stop&gt;&gt;</c> that end it, indented as
    /// the statements at the top of its body.
    /// </summary>
    /// <param name="text">The text as the user wrote it, as for <see cref="SetStatementText"/>.</param>
    /// <exception cref="ArgumentException">The text cannot be a line.</exception>
    public static IReadOnlyList<YarnTextEdit> AddLine(YarnDocument document, YarnNodeSpan node, string text)
    {
        Check(document, node);
        var written = TextOfStatement(text);
        var statements = document.StatementsOf(node);

        var index = statements.Count;
        while (index > 0 && statements[index - 1] is { Depth: 0 } last
            && (last.Kind == YarnStatementKind.Jump || (last.Kind == YarnStatementKind.Command && last.Text is "stop" or "return")))
        {
            index--;
        }

        var newLine = document.NewLine;
        var line = new StringBuilder();
        int offset;
        string indentation;
        if (index < statements.Count)
        {
            offset = statements[index].LineStart;
            indentation = statements[index].Indentation;
        }
        else
        {
            offset = statements.Count > 0 ? statements[^1].LineEnd : node.BodyStart;
            indentation = statements.FirstOrDefault(s => s.Depth == 0)?.Indentation ?? "";
            if (!IsLineStart(document.Text, offset))
            {
                line.Append(newLine);
            }

            if (node.DelimiterStart < 0)
            {
                line.Append("---").Append(newLine);
            }
        }

        line.Append(indentation).Append(written).Append(newLine);
        return [new YarnTextEdit(offset, 0, line.ToString())];
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

    private static IReadOnlyList<YarnStatementSpan> Check(YarnDocument document, YarnNodeSpan node, YarnStatementSpan statement)
    {
        Check(document, node);
        ArgumentNullException.ThrowIfNull(statement);
        var statements = document.StatementsOf(node);
        if (!statements.Any(s => ReferenceEquals(s, statement)))
            throw new ArgumentException("The statement is not of this node: read the statements again after each change.", nameof(statement));

        return statements;
    }

    private static int IndexOf(IReadOnlyList<YarnStatementSpan> statements, YarnStatementSpan statement)
    {
        for (var i = 0; i < statements.Count; i++)
        {
            if (ReferenceEquals(statements[i], statement))
                return i;
        }

        return -1;
    }

    // The last statement that is in a statement, or the statement itself when nothing is in it
    private static int LastWithin(IReadOnlyList<YarnStatementSpan> statements, int index)
    {
        var last = index;
        for (var i = index + 1; i < statements.Count && IsWithin(statements, i, index); i++)
        {
            last = i;
        }

        return last;
    }

    private static bool IsWithin(IReadOnlyList<YarnStatementSpan> statements, int index, int container)
    {
        for (var parent = statements[index].Parent; parent >= 0; parent = statements[parent].Parent)
        {
            if (parent == container)
                return true;
        }

        return false;
    }

    // One step deeper than an indentation: a tab where it has tabs, else four spaces
    private static string Deeper(string indentation) => indentation + (indentation.Contains('\t') ? "\t" : "    ");

    /// <summary>
    /// The text of a line or an option as the user wrote it, made safe for the file: one line, without the spaces
    /// around it, and with what would end it escaped.
    /// </summary>
    private static string TextOfStatement(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        text = text.Trim();
        if (text.AsSpan().IndexOfAny('\r', '\n') >= 0)
            throw new ArgumentException("A line of the dialogue is one line of text.", nameof(text));
        if (text.Length == 0)
            throw new ArgumentException("A line of the dialogue needs a text.", nameof(text));
        if (text.StartsWith("->", StringComparison.Ordinal) || text.StartsWith("=>", StringComparison.Ordinal) || text.StartsWith("===", StringComparison.Ordinal))
            throw new ArgumentException($"A line cannot start with '{text[..2]}': in the text, that starts an option, a line group item or the end of the node.", nameof(text));

        var escaped = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            var next = i + 1 < text.Length ? text[i + 1] : '\0';
            if (c == '\\' && next != '\0')
            {
                // Already escaped
                escaped.Append(c).Append(next);
                i++;
            }
            else if (c == '#' || (c == '/' && next == '/') || (c == '<' && next == '<'))
            {
                escaped.Append('\\').Append(c);
            }
            else
            {
                escaped.Append(c);
            }
        }

        return escaped.ToString();
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
