using Avalonia.Media;
using Avalonia.Media.Immutable;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using ColorRise.Skein.Assets.Text;

namespace ColorRise.Skein.Editor.Avalonia.Text;

/// <summary>
/// Colours the text of a Yarn script, from the tokens of Yarn Spinner's own lexer: what the editor shows as a command or
/// as a node's name is what the compiler reads as one.
/// </summary>
internal sealed class YarnHighlighter : DocumentColorizingTransformer
{
    // The text that is none of these (what a character says) keeps the editor's colour
    private static readonly Dictionary<YarnTokenKind, IBrush> Dark = new()
    {
        [YarnTokenKind.Comment] = Brush(0x6A, 0x99, 0x55),
        [YarnTokenKind.HeaderKey] = Brush(0x9C, 0xDC, 0xFE),
        [YarnTokenKind.HeaderValue] = Brush(0xD7, 0xBA, 0x7D),
        [YarnTokenKind.Delimiter] = Brush(0x80, 0x80, 0x80),
        [YarnTokenKind.Command] = Brush(0xC5, 0x86, 0xC0),
        [YarnTokenKind.Keyword] = Brush(0xC5, 0x86, 0xC0),
        [YarnTokenKind.CommandText] = Brush(0xDC, 0xDC, 0xAA),
        [YarnTokenKind.NodeName] = Brush(0x4E, 0xC9, 0xB0),
        [YarnTokenKind.Variable] = Brush(0x9C, 0xDC, 0xFE),
        [YarnTokenKind.Number] = Brush(0xB5, 0xCE, 0xA8),
        [YarnTokenKind.String] = Brush(0xCE, 0x91, 0x78),
        [YarnTokenKind.Operator] = Brush(0xB4, 0xB4, 0xB4),
        [YarnTokenKind.Function] = Brush(0xDC, 0xDC, 0xAA),
        [YarnTokenKind.Option] = Brush(0xE3, 0xB5, 0x65),
        [YarnTokenKind.Tag] = Brush(0x80, 0x80, 0x80),
        [YarnTokenKind.Speaker] = Brush(0x8F, 0xC7, 0xE8),
        [YarnTokenKind.Markup] = Brush(0xD7, 0xBA, 0x7D),
    };

    private static readonly Dictionary<YarnTokenKind, IBrush> Light = new()
    {
        [YarnTokenKind.Comment] = Brush(0x00, 0x80, 0x00),
        [YarnTokenKind.HeaderKey] = Brush(0x04, 0x51, 0xA5),
        [YarnTokenKind.HeaderValue] = Brush(0x7A, 0x56, 0x00),
        [YarnTokenKind.Delimiter] = Brush(0x80, 0x80, 0x80),
        [YarnTokenKind.Command] = Brush(0xAF, 0x00, 0xDB),
        [YarnTokenKind.Keyword] = Brush(0xAF, 0x00, 0xDB),
        [YarnTokenKind.CommandText] = Brush(0x79, 0x5E, 0x26),
        [YarnTokenKind.NodeName] = Brush(0x1A, 0x7F, 0x8E),
        [YarnTokenKind.Variable] = Brush(0x00, 0x10, 0x80),
        [YarnTokenKind.Number] = Brush(0x09, 0x86, 0x58),
        [YarnTokenKind.String] = Brush(0xA3, 0x15, 0x15),
        [YarnTokenKind.Operator] = Brush(0x55, 0x55, 0x55),
        [YarnTokenKind.Function] = Brush(0x79, 0x5E, 0x26),
        [YarnTokenKind.Option] = Brush(0xB0, 0x5A, 0x00),
        [YarnTokenKind.Tag] = Brush(0x80, 0x80, 0x80),
        [YarnTokenKind.Speaker] = Brush(0x0B, 0x5F, 0xA5),
        [YarnTokenKind.Markup] = Brush(0x8B, 0x69, 0x14),
    };

    private readonly TextDocument document;
    private IReadOnlyList<YarnToken>? tokens;

    /// <summary>
    /// Colours a document.
    /// </summary>
    public YarnHighlighter(TextDocument document)
    {
        this.document = document;
        document.Changed += (_, _) => tokens = null;
    }

    /// <summary>
    /// Whether the colours are the ones for a dark background.
    /// </summary>
    public bool IsDark { get; set; }

    /// <summary>
    /// The tokens of the text as it is, in the order of the text.
    /// </summary>
    public IReadOnlyList<YarnToken> Tokens => tokens ??= YarnTokenizer.Tokenize(document.Text);

    /// <summary>
    /// The token at a place of the text, or null where the text is plain.
    /// </summary>
    public YarnToken? TokenAt(int offset)
    {
        var all = Tokens;
        for (var i = FirstEndingAfter(all, offset); i < all.Count && all[i].Offset <= offset; i++)
        {
            if (offset < all[i].Offset + all[i].Length)
                return all[i];
        }

        return null;
    }

    /// <inheritdoc/>
    protected override void ColorizeLine(DocumentLine line)
    {
        var all = Tokens;
        var brushes = IsDark ? Dark : Light;
        var (start, end) = (line.Offset, line.EndOffset);
        for (var i = FirstEndingAfter(all, start); i < all.Count && all[i].Offset < end; i++)
        {
            var token = all[i];
            var from = Math.Max(token.Offset, start);
            var to = Math.Min(token.Offset + token.Length, end);
            if (from < to && brushes.TryGetValue(token.Kind, out var brush))
            {
                ChangeLinePart(from, to, element => element.TextRunProperties.SetForegroundBrush(brush));
            }
        }
    }

    // The index of the first token that ends after a place: the tokens are in the order of the text and do not overlap
    private static int FirstEndingAfter(IReadOnlyList<YarnToken> tokens, int offset)
    {
        var (low, high) = (0, tokens.Count);
        while (low < high)
        {
            var middle = (low + high) / 2;
            if (tokens[middle].Offset + tokens[middle].Length <= offset)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }

    private static IBrush Brush(byte r, byte g, byte b) => new ImmutableSolidColorBrush(Color.FromRgb(r, g, b));
}
