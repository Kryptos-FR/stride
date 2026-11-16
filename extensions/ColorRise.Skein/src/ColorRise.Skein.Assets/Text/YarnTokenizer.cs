using Antlr4.Runtime;
using Yarn.Compiler;

namespace ColorRise.Skein.Assets.Text;

/// <summary>
/// What a piece of the text of a script is, for an editor that colours it.
/// </summary>
public enum YarnTokenKind
{
    /// <summary>
    /// A <c>//</c> comment.
    /// </summary>
    Comment,

    /// <summary>
    /// The name of a header.
    /// </summary>
    HeaderKey,

    /// <summary>
    /// The value of a header.
    /// </summary>
    HeaderValue,

    /// <summary>
    /// The <c>---</c> and <c>===</c> around a body.
    /// </summary>
    Delimiter,

    /// <summary>
    /// The <c>&lt;&lt;</c> and <c>&gt;&gt;</c> of a command.
    /// </summary>
    Command,

    /// <summary>
    /// A word of the language: <c>if</c>, <c>set</c>, <c>jump</c>, <c>true</c>.
    /// </summary>
    Keyword,

    /// <summary>
    /// The text of a command of the game.
    /// </summary>
    CommandText,

    /// <summary>
    /// The node a jump or a detour names.
    /// </summary>
    NodeName,

    /// <summary>
    /// A variable.
    /// </summary>
    Variable,

    /// <summary>
    /// A number.
    /// </summary>
    Number,

    /// <summary>
    /// A string in quotes.
    /// </summary>
    String,

    /// <summary>
    /// An operator, a parenthesis, or the braces of an expression in a line.
    /// </summary>
    Operator,

    /// <summary>
    /// The name of a function, a type or an enum.
    /// </summary>
    Function,

    /// <summary>
    /// The arrow of an option or of a line group.
    /// </summary>
    Option,

    /// <summary>
    /// A hashtag of a line, as <c>#line:0a1b2c</c>.
    /// </summary>
    Tag,

    /// <summary>
    /// The name of who says a line, with its colon.
    /// </summary>
    Speaker,

    /// <summary>
    /// A markup tag in a line, as <c>[wave]</c>.
    /// </summary>
    Markup,
}

/// <summary>
/// A piece of the text of a script that is not plain text.
/// </summary>
public readonly record struct YarnToken(int Offset, int Length, YarnTokenKind Kind);

/// <summary>
/// Cuts the text of a script into the pieces Yarn Spinner's own lexer sees, so that an editor colours a script as the
/// compiler of the version in use reads it.
/// </summary>
public static class YarnTokenizer
{
    /// <summary>
    /// Gives the pieces of a text that are not plain text, in order. Never fails: a text the lexer stumbles on gives the
    /// pieces before the place.
    /// </summary>
    public static IReadOnlyList<YarnToken> Tokenize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var start = text.Length > 0 && text[0] == YarnDocument.ByteOrderMark ? 1 : 0;
        var tokens = new List<YarnToken>();
        try
        {
            var lexer = new YarnSpinnerLexer(CharStreams.fromString(start == 0 ? text : text[start..]));
            lexer.RemoveErrorListeners();
            Classify(lexer, text, start, tokens);
        }
        catch (Exception exception) when (exception is RecognitionException or InvalidOperationException or ArgumentException or IndexOutOfRangeException)
        {
            // What was read before stays
        }

        return tokens;
    }

    private static void Classify(YarnSpinnerLexer lexer, string text, int shift, List<YarnToken> tokens)
    {
        var vocabulary = lexer.Vocabulary;
        var inBody = false;
        // On the line being read: whether a header's colon was passed, whether a line's text started, and the last word of a command
        var afterColon = false;
        var textStarted = false;
        var lastKeyword = "";
        // The lexer gives the text of a line in several tokens: they are read as one piece
        var runStart = -1;
        var runEnd = -1;

        void EndRun()
        {
            if (runStart >= 0)
            {
                AddText(text, runStart, runEnd - runStart, !textStarted, tokens);
                textStarted = true;
                runStart = -1;
            }
        }

        for (var token = lexer.NextToken(); token is not null && token.Type != TokenConstants.EOF; token = lexer.NextToken())
        {
            // The lexer adds tokens of its own for the indentation, which are no text
            if (token.StartIndex < 0 || token.StopIndex < token.StartIndex)
                continue;

            var offset = token.StartIndex + shift;
            var end = Math.Min(text.Length, token.StopIndex + 1 + shift);
            if (token.Type == YarnSpinnerLexer.TEXT)
            {
                if (runStart >= 0 && offset != runEnd)
                {
                    EndRun();
                }

                runStart = runStart < 0 ? offset : runStart;
                runEnd = end;
                continue;
            }

            EndRun();

            // Some tokens take the spaces around them
            while (offset < end && text[offset] is ' ' or '\t')
            {
                offset++;
            }

            while (end > offset && text[end - 1] is ' ' or '\t')
            {
                end--;
            }

            var length = end - offset;
            if (length == 0 && token.Type != YarnSpinnerLexer.NEWLINE)
                continue;

            var name = vocabulary.GetSymbolicName(token.Type) ?? "";
            switch (name)
            {
                case "NEWLINE" or "COMMAND_NEWLINE" or "COMMAND_TEXT_NEWLINE" or "COMMAND_ID_NEWLINE":
                    afterColon = false;
                    textStarted = false;
                    lastKeyword = "";
                    break;
                case "WS":
                    break;
                case "COMMENT":
                    Add(tokens, offset, length, YarnTokenKind.Comment);
                    break;
                case "BODY_START":
                    inBody = true;
                    Add(tokens, offset, length, YarnTokenKind.Delimiter);
                    break;
                case "BODY_END":
                    inBody = false;
                    Add(tokens, offset, length, YarnTokenKind.Delimiter);
                    break;
                case "HEADER_TITLE" or "HEADER_WHEN":
                    Add(tokens, offset, length, YarnTokenKind.HeaderKey);
                    break;
                case "HEADER_DELIMITER":
                    afterColon = true;
                    break;
                case "HEADER_TEXT":
                    Add(tokens, offset, length, YarnTokenKind.HeaderValue);
                    break;
                case "ID" when !inBody:
                    Add(tokens, offset, length, afterColon ? YarnTokenKind.HeaderValue : YarnTokenKind.HeaderKey);
                    break;
                case "ID":
                    Add(tokens, offset, length, lastKeyword is "COMMAND_JUMP" or "COMMAND_DETOUR" ? YarnTokenKind.NodeName : YarnTokenKind.Function);
                    break;
                case "COMMAND_START" or "COMMAND_END":
                    Add(tokens, offset, length, YarnTokenKind.Command);
                    break;
                case "COMMAND_TEXT":
                    Add(tokens, offset, length, YarnTokenKind.CommandText);
                    break;
                case "SHORTCUT_ARROW" or "LINE_GROUP_ARROW":
                    Add(tokens, offset, length, YarnTokenKind.Option);
                    break;
                case "HASHTAG" or "HASHTAG_TEXT":
                    Add(tokens, offset, length, YarnTokenKind.Tag);
                    break;
                case "VAR_ID":
                    Add(tokens, offset, length, YarnTokenKind.Variable);
                    break;
                case "NUMBER":
                    Add(tokens, offset, length, YarnTokenKind.Number);
                    break;
                case "STRING":
                    Add(tokens, offset, length, YarnTokenKind.String);
                    break;
                case "FUNC_ID":
                    Add(tokens, offset, length, YarnTokenKind.Function);
                    break;
                case "KEYWORD_TRUE" or "KEYWORD_FALSE" or "KEYWORD_NULL" or "EXPRESSION_WHEN_ALWAYS" or "EXPRESSION_AS":
                    Add(tokens, offset, length, YarnTokenKind.Keyword);
                    break;
                case "EXPRESSION_START" or "EXPRESSION_END" or "LPAREN" or "RPAREN" or "COMMA" or "DOT":
                    Add(tokens, offset, length, YarnTokenKind.Operator);
                    break;
                default:
                    if (name.StartsWith("COMMAND_", StringComparison.Ordinal))
                    {
                        lastKeyword = name;
                        Add(tokens, offset, length, YarnTokenKind.Keyword);
                    }
                    else if (name.StartsWith("OPERATOR_", StringComparison.Ordinal))
                    {
                        Add(tokens, offset, length, YarnTokenKind.Operator);
                    }

                    break;
            }
        }

        EndRun();
    }

    // The lexer cuts some pieces in two (the first letter of a command, the '#' of a hashtag): two pieces of a kind
    // that touch are one
    private static void Add(List<YarnToken> tokens, int offset, int length, YarnTokenKind kind)
    {
        if (tokens.Count > 0 && tokens[^1] is var last && last.Kind == kind && last.Offset + last.Length == offset && kind is YarnTokenKind.CommandText or YarnTokenKind.Tag)
        {
            tokens[^1] = last with { Length = last.Length + length };
        }
        else
        {
            tokens.Add(new YarnToken(offset, length, kind));
        }
    }

    /// <summary>
    /// Finds in the text of a line who says it and its markup tags.
    /// </summary>
    private static void AddText(string text, int offset, int length, bool isLineStart, List<YarnToken> tokens)
    {
        var end = offset + length;
        var position = offset;

        // A name is what comes before the first colon of the line, when it holds neither markup nor an expression
        if (isLineStart)
        {
            for (var i = offset; i < end; i++)
            {
                if (text[i] is '[' or '{')
                    break;

                if (text[i] == ':' && i > offset && text[i - 1] != '\\')
                {
                    tokens.Add(new YarnToken(offset, i + 1 - offset, YarnTokenKind.Speaker));
                    position = i + 1;
                    break;
                }
            }
        }

        while (position < end)
        {
            var open = text.IndexOf('[', position, end - position);
            if (open < 0)
                break;

            if (open > 0 && text[open - 1] == '\\')
            {
                position = open + 1;
                continue;
            }

            var close = text.IndexOf(']', open, end - open);
            if (close < 0)
                break;

            tokens.Add(new YarnToken(open, close + 1 - open, YarnTokenKind.Markup));
            position = close + 1;
        }
    }
}
