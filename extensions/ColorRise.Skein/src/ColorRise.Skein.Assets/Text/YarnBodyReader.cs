namespace ColorRise.Skein.Assets.Text;

/// <summary>
/// Reads the statements of the body of a node, line by line, without the compiler: a body that does not compile still
/// has its statements. Options and line group items hold the lines indented under them; blocks hold what is between
/// their start and their end.
/// </summary>
internal static class YarnBodyReader
{
    private const int TabWidth = 4;

    /// <summary>
    /// Reads the statements of a node of a text.
    /// </summary>
    public static IReadOnlyList<YarnStatementSpan> Read(string text, YarnNodeSpan node)
    {
        var statements = new List<YarnStatementSpan>();
        var frames = new List<Frame>();
        var lineNumber = LineOf(text, node.BodyStart);
        var start = node.BodyStart;
        while (start < node.BodyEnd)
        {
            var end = text.AsSpan(start, node.BodyEnd - start).IndexOfAny('\r', '\n');
            end = end < 0 ? node.BodyEnd : start + end;
            var next = end < node.BodyEnd ? AfterLineBreak(text, end) : end;
            ReadLine(text, start, end, next, lineNumber, statements, frames);
            start = next;
            lineNumber++;
        }

        return statements;
    }

    private static void ReadLine(string text, int start, int end, int next, int lineNumber, List<YarnStatementSpan> statements, List<Frame> frames)
    {
        var contentStart = start;
        while (contentStart < end && text[contentStart] is ' ' or '\t')
        {
            contentStart++;
        }

        var contentEnd = YarnDocument.CommentStart(text, contentStart, end);
        while (contentEnd > contentStart && char.IsWhiteSpace(text[contentEnd - 1]))
        {
            contentEnd--;
        }

        if (contentEnd == contentStart)
            return;

        // A line no deeper than an option is out of it
        var indent = IndentOf(text, start, contentStart);
        while (frames.Count > 0 && !frames[^1].IsBlock && indent <= frames[^1].Indent)
        {
            frames.RemoveAt(frames.Count - 1);
        }

        var statement = new YarnStatementSpan
        {
            Kind = YarnStatementKind.Line,
            Line = lineNumber,
            LineStart = start,
            LineEnd = next,
            Indentation = text[start..contentStart],
            Depth = frames.Count,
            Parent = frames.Count > 0 ? frames[^1].Owner : -1,
            TextStart = contentStart,
            TextEnd = contentEnd,
            Text = text[contentStart..contentEnd],
        };

        if (text.AsSpan(contentStart, contentEnd - contentStart).StartsWith("<<"))
        {
            ReadCommand(text, contentStart, contentEnd, statement, statements, frames);
            return;
        }

        var kind = text.AsSpan(contentStart, contentEnd - contentStart) switch
        {
            ['-', '>', ..] => YarnStatementKind.Option,
            ['=', '>', ..] => YarnStatementKind.LineGroupItem,
            _ => YarnStatementKind.Line,
        };
        var textStart = contentStart;
        if (kind != YarnStatementKind.Line)
        {
            textStart += 2;
            while (textStart < contentEnd && text[textStart] is ' ' or '\t')
            {
                textStart++;
            }
        }

        statement = ReadText(text, textStart, contentEnd, statement with { Kind = kind });
        statements.Add(statement);
        if (statement.IsBranch)
        {
            frames.Add(new Frame(IsBlock: false, indent, statements.Count - 1));
        }
    }

    // The text of a line, then its condition and its hashtags
    private static YarnStatementSpan ReadText(string text, int start, int end, YarnStatementSpan statement)
    {
        var textEnd = end;
        var depth = 0;
        for (var i = start; i < end; i++)
        {
            var c = text[i];
            if (c == '\\')
            {
                i++;
            }
            else if (c == '{')
            {
                depth++;
            }
            else if (c == '}')
            {
                depth = Math.Max(depth - 1, 0);
            }
            else if (depth == 0 && (c == '#' || (c == '<' && i + 1 < end && text[i + 1] == '<')))
            {
                textEnd = i;
                break;
            }
        }

        var rest = textEnd;
        while (textEnd > start && char.IsWhiteSpace(text[textEnd - 1]))
        {
            textEnd--;
        }

        string? condition = null;
        var isOnce = false;
        var hashtags = new List<string>();
        while (rest < end)
        {
            if (char.IsWhiteSpace(text[rest]))
            {
                rest++;
            }
            else if (text[rest] == '#')
            {
                var tagEnd = rest + 1;
                while (tagEnd < end && !char.IsWhiteSpace(text[tagEnd]))
                {
                    tagEnd++;
                }

                hashtags.Add(text[(rest + 1)..tagEnd]);
                rest = tagEnd;
            }
            else if (text.AsSpan(rest, end - rest).StartsWith("<<"))
            {
                var close = CommandEnd(text, rest, end);
                var inner = text[(rest + 2)..(close < 0 ? end : close - 2)].Trim();
                (condition, isOnce) = ReadCondition(inner);
                rest = close < 0 ? end : close;
            }
            else
            {
                break;
            }
        }

        return statement with
        {
            TextStart = start,
            TextEnd = textEnd,
            Text = text[start..textEnd],
            Condition = condition,
            IsOnce = isOnce,
            Hashtags = hashtags,
        };
    }

    private static void ReadCommand(string text, int start, int end, YarnStatementSpan statement, List<YarnStatementSpan> statements, List<Frame> frames)
    {
        var close = CommandEnd(text, start, end);
        var innerStart = start + 2;
        var innerEnd = close < 0 ? end : close - 2;
        while (innerStart < innerEnd && char.IsWhiteSpace(text[innerStart]))
        {
            innerStart++;
        }

        while (innerEnd > innerStart && char.IsWhiteSpace(text[innerEnd - 1]))
        {
            innerEnd--;
        }

        var inner = text[innerStart..innerEnd];
        var wordEnd = 0;
        while (wordEnd < inner.Length && !char.IsWhiteSpace(inner[wordEnd]))
        {
            wordEnd++;
        }

        var word = inner[..wordEnd];
        var argument = inner[wordEnd..].Trim();
        statement = statement with { TextStart = innerStart, TextEnd = innerEnd, Text = inner, Kind = YarnStatementKind.Command };
        switch (word)
        {
            case "if":
                Open(statement with { Kind = YarnStatementKind.If, Condition = argument });
                break;
            case "once":
                var (condition, _) = ReadCondition(inner);
                Open(statement with { Kind = YarnStatementKind.Once, Condition = condition, IsOnce = true });
                break;
            case "elseif":
                Branch(statement with { Kind = YarnStatementKind.ElseIf, Condition = argument });
                break;
            case "else":
                Branch(statement with { Kind = YarnStatementKind.Else });
                break;
            case "endif" or "endonce":
                if (CloseOptions())
                    frames.RemoveAt(frames.Count - 1);
                break;
            case "jump":
                statements.Add(statement with { Kind = YarnStatementKind.Jump, Target = argument });
                break;
            case "detour":
                statements.Add(statement with { Kind = YarnStatementKind.Detour, Target = argument });
                break;
            default:
                statements.Add(statement);
                break;
        }

        void Open(YarnStatementSpan block)
        {
            statements.Add(block);
            frames.Add(new Frame(IsBlock: true, 0, statements.Count - 1));
        }

        // The branch of a block that is open takes the place of the one before; without a block, it stands alone
        void Branch(YarnStatementSpan branch)
        {
            if (!CloseOptions())
            {
                statements.Add(branch);
                return;
            }

            var parent = frames.Count > 1 ? frames[^2].Owner : -1;
            statements.Add(branch with { Depth = frames.Count - 1, Parent = parent });
            frames[^1] = frames[^1] with { Owner = statements.Count - 1 };
        }

        // The options of a block end with it
        bool CloseOptions()
        {
            while (frames.Count > 0 && !frames[^1].IsBlock)
            {
                frames.RemoveAt(frames.Count - 1);
            }

            return frames.Count > 0;
        }
    }

    // "if $x", "once", "once if $x"
    private static (string? Condition, bool IsOnce) ReadCondition(string inner)
    {
        var isOnce = false;
        if (inner == "once" || inner.StartsWith("once ", StringComparison.Ordinal))
        {
            isOnce = true;
            inner = inner[4..].TrimStart();
        }

        if (inner == "if" || inner.StartsWith("if ", StringComparison.Ordinal) || inner.StartsWith("if(", StringComparison.Ordinal))
            return (inner[2..].Trim(), isOnce);

        return (inner.Length == 0 ? null : inner, isOnce);
    }

    // The place after the >> of a command that starts at a place, or -1 when it has none yet: a >> in a string is not its end
    private static int CommandEnd(string text, int start, int end)
    {
        var inString = false;
        for (var i = start + 2; i < end; i++)
        {
            var c = text[i];
            if (c == '\\')
            {
                i++;
            }
            else if (c == '"')
            {
                inString = !inString;
            }
            else if (!inString && c == '>' && i + 1 < end && text[i + 1] == '>')
            {
                return i + 2;
            }
        }

        return -1;
    }

    private static int IndentOf(string text, int start, int end)
    {
        var indent = 0;
        for (var i = start; i < end; i++)
        {
            indent += text[i] == '\t' ? TabWidth : 1;
        }

        return indent;
    }

    private static int LineOf(string text, int offset)
    {
        var line = 1;
        for (var i = 0; i < offset; i++)
        {
            if (text[i] == '\n' || (text[i] == '\r' && (i + 1 >= text.Length || text[i + 1] != '\n')))
            {
                line++;
            }
        }

        return line;
    }

    private static int AfterLineBreak(string text, int offset)
        => text[offset] == '\r' && offset + 1 < text.Length && text[offset + 1] == '\n' ? offset + 2 : offset + 1;

    /// <summary>
    /// What a statement is in: an option or a line group item down to the end of its indentation, or a block down to its end.
    /// </summary>
    /// <param name="Owner">The rank of the statement that holds what follows: the option, or the last branch of the block.</param>
    private readonly record struct Frame(bool IsBlock, int Indent, int Owner);
}
