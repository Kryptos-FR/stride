using System.Globalization;
using System.Text;

namespace ColorRise.Saga.Expressions;

internal enum SagaTokenKind
{
    End,
    NewLine,
    Identifier,
    Int,
    Float,
    String,
    OrOr,
    AndAnd,
    Bang,
    EqualEqual,
    BangEqual,
    Less,
    LessEqual,
    Greater,
    GreaterEqual,
    Plus,
    Minus,
    Equal,
    OpenParenthesis,
    CloseParenthesis,
    Comma,
    Semicolon,
}

internal readonly record struct SagaToken(SagaTokenKind Kind, int Position, int Length, string Text, SagaValue Value);

/// <summary>
/// Cuts a source into tokens. Identifiers may be dotted (<c>quest.mine.state</c>).
/// </summary>
internal static class SagaLexer
{
    public static List<SagaToken> Tokenize(string source)
    {
        var tokens = new List<SagaToken>();
        var index = 0;
        while (index < source.Length)
        {
            var c = source[index];
            if (c == '\n')
            {
                tokens.Add(new SagaToken(SagaTokenKind.NewLine, index, 1, "\n", default));
                index++;
                continue;
            }
            if (char.IsWhiteSpace(c))
            {
                index++;
                continue;
            }

            var start = index;
            if (IsIdentifierStart(c))
            {
                index++;
                while (index < source.Length)
                {
                    if (IsIdentifierPart(source[index]))
                        index++;
                    else if (source[index] == '.' && index + 1 < source.Length && IsIdentifierStart(source[index + 1]))
                        index++;
                    else
                        break;
                }
                tokens.Add(new SagaToken(SagaTokenKind.Identifier, start, index - start, source[start..index], default));
                continue;
            }
            if (char.IsAsciiDigit(c))
            {
                while (index < source.Length && char.IsAsciiDigit(source[index]))
                    index++;
                var isFloat = index + 1 < source.Length && source[index] == '.' && char.IsAsciiDigit(source[index + 1]);
                if (isFloat)
                {
                    index++;
                    while (index < source.Length && char.IsAsciiDigit(source[index]))
                        index++;
                }
                var text = source[start..index];
                if (isFloat)
                {
                    tokens.Add(new SagaToken(SagaTokenKind.Float, start, index - start, text, SagaValue.Float(float.Parse(text, CultureInfo.InvariantCulture))));
                }
                else
                {
                    if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
                        throw new SagaParseException(start, index - start, $"The number {text} is too large.");
                    tokens.Add(new SagaToken(SagaTokenKind.Int, start, index - start, text, SagaValue.Int(number)));
                }
                continue;
            }
            if (c == '"')
            {
                index++;
                var builder = new StringBuilder();
                var terminated = false;
                while (index < source.Length)
                {
                    var current = source[index];
                    if (current == '"')
                    {
                        index++;
                        terminated = true;
                        break;
                    }
                    if (current == '\n')
                        break;
                    if (current == '\\' && index + 1 < source.Length && source[index + 1] is '"' or '\\')
                    {
                        builder.Append(source[index + 1]);
                        index += 2;
                        continue;
                    }
                    builder.Append(current);
                    index++;
                }
                if (!terminated)
                    throw new SagaParseException(start, index - start, "Unterminated string: a string ends with \".");
                tokens.Add(new SagaToken(SagaTokenKind.String, start, index - start, source[start..index], SagaValue.String(builder.ToString())));
                continue;
            }

            var next = index + 1 < source.Length ? source[index + 1] : '\0';
            (SagaTokenKind Kind, int Length)? symbol = (c, next) switch
            {
                ('|', '|') => (SagaTokenKind.OrOr, 2),
                ('&', '&') => (SagaTokenKind.AndAnd, 2),
                ('=', '=') => (SagaTokenKind.EqualEqual, 2),
                ('!', '=') => (SagaTokenKind.BangEqual, 2),
                ('<', '=') => (SagaTokenKind.LessEqual, 2),
                ('>', '=') => (SagaTokenKind.GreaterEqual, 2),
                ('!', _) => (SagaTokenKind.Bang, 1),
                ('<', _) => (SagaTokenKind.Less, 1),
                ('>', _) => (SagaTokenKind.Greater, 1),
                ('+', _) => (SagaTokenKind.Plus, 1),
                ('-', _) => (SagaTokenKind.Minus, 1),
                ('=', _) => (SagaTokenKind.Equal, 1),
                ('(', _) => (SagaTokenKind.OpenParenthesis, 1),
                (')', _) => (SagaTokenKind.CloseParenthesis, 1),
                (',', _) => (SagaTokenKind.Comma, 1),
                (';', _) => (SagaTokenKind.Semicolon, 1),
                _ => null,
            };
            if (symbol is null)
                throw new SagaParseException(index, 1, $"Unexpected character '{c}'.");
            tokens.Add(new SagaToken(symbol.Value.Kind, index, symbol.Value.Length, source.Substring(index, symbol.Value.Length), default));
            index += symbol.Value.Length;
        }
        tokens.Add(new SagaToken(SagaTokenKind.End, source.Length, 0, string.Empty, default));
        return tokens;
    }

    private static bool IsIdentifierStart(char c) => char.IsAsciiLetter(c) || c == '_';

    private static bool IsIdentifierPart(char c) => char.IsAsciiLetterOrDigit(c) || c == '_';
}

/// <summary>
/// The first error met while parsing; the parser turns it into a <see cref="SagaDiagnostic"/>.
/// </summary>
internal sealed class SagaParseException(int position, int length, string message) : Exception(message)
{
    public int Position { get; } = position;

    public int Length { get; } = length;
}
