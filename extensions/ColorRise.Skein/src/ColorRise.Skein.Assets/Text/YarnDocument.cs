namespace ColorRise.Skein.Assets.Text;

/// <summary>
/// The nodes of a script's text and where their parts are, read without the compiler: a script that does not compile
/// still has its nodes here.
/// </summary>
/// <remarks>
/// It knows the frame of a file (headers, <c>---</c>, body, <c>===</c>, comments) and nothing of what a body says.
/// A document is of one text: after a change, parse again.
/// </remarks>
public sealed class YarnDocument
{
    /// <summary>
    /// The mark a file may start with to say it is Unicode.
    /// </summary>
    internal const char ByteOrderMark = (char)0xFEFF;

    // The statements of the nodes, read when first asked for
    private readonly Dictionary<YarnNodeSpan, IReadOnlyList<YarnStatementSpan>> statements = new(ReferenceEqualityComparer.Instance);

    private YarnDocument(string text, string newLine, IReadOnlyList<YarnNodeSpan> nodes)
    {
        Text = text;
        NewLine = newLine;
        Nodes = nodes;
    }

    /// <summary>
    /// The text the document was read from.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// The line break of the file: the first one it has, or <c>\n</c> for a file of one line.
    /// </summary>
    public string NewLine { get; }

    /// <summary>
    /// The nodes, in the order of the text.
    /// </summary>
    public IReadOnlyList<YarnNodeSpan> Nodes { get; }

    /// <summary>
    /// The node of a title; <paramref name="index"/> picks among the members of a node group.
    /// </summary>
    public YarnNodeSpan? Find(string title, int index = 0)
    {
        foreach (var node in Nodes)
        {
            if (node.Title == title && node.Index == index)
                return node;
        }

        return null;
    }

    /// <summary>
    /// The statements of the body of a node of the document, in the order of the text.
    /// </summary>
    public IReadOnlyList<YarnStatementSpan> StatementsOf(YarnNodeSpan node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (!Nodes.Any(n => ReferenceEquals(n, node)))
            throw new ArgumentException("The node is not of this document: parse the text again after each change.", nameof(node));

        if (!statements.TryGetValue(node, out var read))
        {
            statements[node] = read = YarnBodyReader.Read(Text, node);
        }

        return read;
    }

    /// <summary>
    /// The node a place of the text is in, or null between two nodes.
    /// </summary>
    public YarnNodeSpan? NodeAt(int offset)
    {
        foreach (var node in Nodes)
        {
            // The end of an open node is the end of the text, which is still in it
            if (offset >= node.HeaderStart && (offset < node.End || (!node.IsClosed && offset == node.End)))
                return node;
        }

        return null;
    }

    /// <summary>
    /// Reads the nodes of a text. Never fails: what is not a node yet is left out.
    /// </summary>
    public static YarnDocument Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var nodes = new List<YarnNodeSpan>();
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        string? newLine = null;

        List<YarnHeaderSpan>? headers = null;
        var headerStart = 0;
        var headerLine = 0;
        var delimiterStart = -1;
        var bodyStart = -1;

        var lineNumber = 0;

        // A byte order mark is of the file, not of its first line
        var start = text.Length > 0 && text[0] == ByteOrderMark ? 1 : 0;
        while (start < text.Length)
        {
            // A line ends with \r\n, \n or \r, as for Yarn Spinner
            var end = text.AsSpan(start).IndexOfAny('\r', '\n');
            int next;
            if (end < 0)
            {
                end = text.Length;
                next = end;
            }
            else
            {
                end += start;
                next = text[end] == '\r' && end + 1 < text.Length && text[end + 1] == '\n' ? end + 2 : end + 1;
                newLine ??= text[end..next];
            }

            lineNumber++;
            var content = Content(text, start, end);

            if (bodyStart >= 0)
            {
                if (content.SequenceEqual("==="))
                {
                    Close(bodyEnd: start, end: next, isClosed: true);
                }
            }
            else if (content.Length > 0)
            {
                if (headers is null && content[0] == '#')
                {
                    // A tag of the file, before its first node
                }
                else if (content.SequenceEqual("==="))
                {
                    // A node closed before its body began
                    if (headers is not null)
                    {
                        bodyStart = start;
                        Close(bodyEnd: start, end: next, isClosed: true);
                    }
                }
                else
                {
                    if (headers is null)
                    {
                        headers = [];
                        headerStart = start;
                        headerLine = lineNumber;
                    }

                    if (content.StartsWith("---"))
                    {
                        delimiterStart = start;
                        bodyStart = next;
                    }
                    else
                    {
                        headers.Add(Header(text, start, end, next));
                    }
                }
            }

            start = next;
        }

        if (headers is not null)
        {
            if (bodyStart < 0)
            {
                bodyStart = text.Length;
            }

            Close(bodyEnd: text.Length, end: text.Length, isClosed: false);
        }

        return new YarnDocument(text, newLine ?? "\n", nodes);

        void Close(int bodyEnd, int end, bool isClosed)
        {
            var title = "";
            foreach (var header in headers!)
            {
                if (header.Key == "title")
                {
                    title = header.Value;
                    break;
                }
            }

            counts.TryGetValue(title, out var index);
            counts[title] = index + 1;
            nodes.Add(new YarnNodeSpan
            {
                Title = title,
                Index = index,
                Headers = headers,
                Line = headerLine,
                HeaderStart = headerStart,
                DelimiterStart = delimiterStart,
                BodyStart = bodyStart,
                BodyEnd = bodyEnd,
                End = end,
                IsClosed = isClosed,
            });

            headers = null;
            delimiterStart = -1;
            bodyStart = -1;
        }
    }

    /// <summary>
    /// Where the comment of a line starts, or the end of the line: <c>//</c>, unless a backslash escapes it or it is
    /// in a string of an expression.
    /// </summary>
    /// <param name="header">Whether the line is a header: the value of a <c>when:</c> header is an expression.</param>
    internal static int CommentStart(string text, int start, int end, bool header = false)
    {
        // Yarn Spinner reads a string only where it reads an expression: in a command, between braces, after "when:"
        var code = header && IsWhenHeader(text, start, end) ? 1 : 0;
        var inString = false;
        for (var i = start; i < end; i++)
        {
            var c = text[i];
            var next = i + 1 < end ? text[i + 1] : '\0';
            if (c == '\\')
            {
                i++;
            }
            else if (inString)
            {
                inString = c != '"';
            }
            else if (c == '"' && code > 0)
            {
                inString = true;
            }
            else if (c == '/' && next == '/')
            {
                return i;
            }
            else if (c == '<' && next == '<')
            {
                code++;
                i++;
            }
            else if (c == '{')
            {
                code++;
            }
            else if (c == '>' && next == '>')
            {
                code = Math.Max(code - 1, 0);
                i++;
            }
            else if (c == '}')
            {
                code = Math.Max(code - 1, 0);
            }
        }

        return end;
    }

    private static bool IsWhenHeader(string text, int start, int end)
    {
        var line = text.AsSpan(start, end - start).TrimStart();
        return line.StartsWith("when", StringComparison.Ordinal) && line[4..].TrimStart().StartsWith(":", StringComparison.Ordinal);
    }

    // What a line says: without its comment and the spaces around it
    private static ReadOnlySpan<char> Content(string text, int start, int end)
        => text.AsSpan(start, CommentStart(text, start, end) - start).Trim();

    private static YarnHeaderSpan Header(string text, int start, int end, int next)
    {
        var contentEnd = CommentStart(text, start, end, header: true);
        while (contentEnd > start && char.IsWhiteSpace(text[contentEnd - 1]))
        {
            contentEnd--;
        }

        var keyStart = start;
        while (keyStart < contentEnd && char.IsWhiteSpace(text[keyStart]))
        {
            keyStart++;
        }

        var colon = text.IndexOf(':', keyStart, contentEnd - keyStart);
        if (colon < 0)
        {
            // Not a header yet: a name whose colon is still to type
            return new YarnHeaderSpan(text[keyStart..contentEnd], "", start, next, contentEnd);
        }

        var valueStart = colon + 1;
        while (valueStart < contentEnd && char.IsWhiteSpace(text[valueStart]))
        {
            valueStart++;
        }

        return new YarnHeaderSpan(text[keyStart..colon].TrimEnd(), text[valueStart..contentEnd], start, next, valueStart);
    }
}
