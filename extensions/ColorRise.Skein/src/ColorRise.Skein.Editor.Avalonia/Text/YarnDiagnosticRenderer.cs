using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using ColorRise.Skein.Assets.Compilation;
using ColorRise.Skein.Editor.Avalonia.Views;

namespace ColorRise.Skein.Editor.Avalonia.Text;

/// <summary>
/// Underlines in the text of a script what the compiler says of it. The marks follow the text while it is typed, until
/// the next messages of the compiler replace them.
/// </summary>
internal sealed class YarnDiagnosticRenderer(TextDocument document) : IBackgroundRenderer
{
    private const double WaveStep = 2.5;
    private const double WaveHeight = 1.5;

    private readonly List<Mark> marks = [];

    /// <inheritdoc/>
    public KnownLayer Layer => KnownLayer.Selection;

    /// <summary>
    /// Marks the places of messages about the text as the document has it.
    /// </summary>
    public void Show(IReadOnlyList<YarnDiagnostic> diagnostics)
    {
        marks.Clear();
        foreach (var diagnostic in diagnostics)
        {
            if (RangeOf(diagnostic) is var (start, end))
            {
                var from = document.CreateAnchor(start);
                var to = document.CreateAnchor(end);
                from.MovementType = AnchorMovementType.AfterInsertion;
                to.MovementType = AnchorMovementType.BeforeInsertion;
                marks.Add(new Mark(from, to, diagnostic));
            }
        }
    }

    /// <summary>
    /// The message marked at a place of the text: the most severe one, or null.
    /// </summary>
    public YarnDiagnostic? At(int offset)
        => marks.Where(m => !m.Start.IsDeleted && !m.End.IsDeleted && offset >= m.Start.Offset && offset <= m.End.Offset)
            .OrderBy(m => m.Diagnostic.Severity)
            .FirstOrDefault()?.Diagnostic;

    /// <inheritdoc/>
    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (!textView.VisualLinesValid || textView.VisualLines.Count == 0)
            return;

        var first = textView.VisualLines[0].FirstDocumentLine.Offset;
        var last = textView.VisualLines[^1].LastDocumentLine.EndOffset;
        foreach (var mark in marks)
        {
            if (mark.Start.IsDeleted || mark.End.IsDeleted || mark.End.Offset <= mark.Start.Offset || mark.End.Offset < first || mark.Start.Offset > last)
                continue;

            var pen = new Pen(YarnPalette.OfSeverity(mark.Diagnostic.Severity), 1);
            var segment = new TextSegment { StartOffset = mark.Start.Offset, EndOffset = mark.End.Offset };
            foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
            {
                drawingContext.DrawGeometry(null, pen, Wave(rect));
            }
        }
    }

    /// <summary>
    /// The range of the text a message is about: at least one character, the whole line when the message gives none.
    /// </summary>
    private (int Start, int End)? RangeOf(YarnDiagnostic diagnostic)
    {
        if (diagnostic.Line < 1 || diagnostic.Line > document.LineCount)
            return null;

        var line = document.GetLineByNumber(diagnostic.Line);
        var start = line.Offset + Math.Clamp(diagnostic.Column - 1, 0, line.Length);
        var end = start;
        if (diagnostic.EndLine >= diagnostic.Line && diagnostic.EndLine <= document.LineCount)
        {
            var endLine = document.GetLineByNumber(diagnostic.EndLine);
            end = endLine.Offset + Math.Clamp(diagnostic.EndColumn - 1, 0, endLine.Length);
        }

        if (end <= start)
        {
            if (line.Length == 0)
                return null;

            // A place without a length, or past the end of its line
            (start, end) = start < line.EndOffset ? (start, start + 1) : (line.Offset, line.EndOffset);
        }

        return (start, end);
    }

    private static StreamGeometry Wave(Rect rect)
    {
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        var y = rect.Bottom - WaveHeight;
        context.BeginFigure(new Point(rect.Left, y), false);
        var up = true;
        for (var x = rect.Left + WaveStep; x < rect.Right + WaveStep; x += WaveStep)
        {
            context.LineTo(new Point(Math.Min(x, rect.Right), up ? y - WaveHeight : y + WaveHeight));
            up = !up;
        }

        context.EndFigure(false);
        return geometry;
    }

    private sealed record Mark(TextAnchor Start, TextAnchor End, YarnDiagnostic Diagnostic);
}
