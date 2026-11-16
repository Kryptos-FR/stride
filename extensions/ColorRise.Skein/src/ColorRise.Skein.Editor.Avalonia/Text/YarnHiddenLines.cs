using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;

namespace ColorRise.Skein.Editor.Avalonia.Text;

/// <summary>
/// The lines of a text that a text editor does not show: each run of them is one line with a mark, which the user
/// can neither type in nor delete.
/// </summary>
/// <remarks>
/// The text view cannot leave lines out by itself: a run of hidden lines is one visual line, whose only element
/// stands for their whole text, and the lines after the first are collapsed.
/// </remarks>
internal sealed class YarnHiddenLines : VisualLineElementGenerator, IReadOnlySectionProvider
{
    private const string Mark = "⋯";
    private static readonly IBrush MarkBrush = new ImmutableSolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80));

    private readonly TextView view;
    private readonly List<Run> runs = [];

    /// <summary>
    /// Hides lines of a text view.
    /// </summary>
    public YarnHiddenLines(TextView view)
    {
        this.view = view;
    }

    /// <summary>
    /// Whether no line is hidden.
    /// </summary>
    public bool IsEmpty => runs.Count == 0;

    /// <summary>
    /// Shows every line again.
    /// </summary>
    public void Clear()
    {
        foreach (var run in runs)
        {
            run.Collapsed?.Uncollapse();
        }

        runs.Clear();
    }

    /// <summary>
    /// Hides the lines from a line to another, both included; the runs are given in the order of the text.
    /// </summary>
    public void Hide(DocumentLine first, DocumentLine last)
    {
        var collapsed = first != last ? view.CollapseLines(first.NextLine, last) : null;
        runs.Add(new Run(first, last, collapsed));
    }

    /// <summary>
    /// Whether a line is in a hidden run.
    /// </summary>
    public bool Hides(int lineNumber)
        => runs.Exists(r => r.IsAlive && lineNumber >= r.First.LineNumber && lineNumber <= r.Last.LineNumber);

    /// <inheritdoc/>
    public override int GetFirstInterestedOffset(int startOffset)
    {
        foreach (var run in runs)
        {
            if (run.IsAlive && run.Length > 0 && run.First.Offset >= startOffset)
                return run.First.Offset;
        }

        return -1;
    }

    /// <inheritdoc/>
    public override VisualLineElement? ConstructElement(int offset)
    {
        var run = runs.Find(r => r.IsAlive && r.First.Offset == offset);
        if (run is null || run.Length <= 0)
            return null;

        return new MarkElement(run.Length);
    }

    /// <inheritdoc/>
    public bool CanInsert(int offset) => !Protected().Any(range => offset > range.Start && offset < range.End);

    /// <inheritdoc/>
    public IEnumerable<ISegment> GetDeletableSegments(ISegment segment)
    {
        var (start, end) = (segment.Offset, segment.EndOffset);
        foreach (var range in Protected())
        {
            if (range.End <= start || range.Start >= end)
                continue;

            if (range.Start > start)
                yield return new TextSegment { StartOffset = start, EndOffset = range.Start };

            start = Math.Max(start, range.End);
        }

        if (start < end)
            yield return new TextSegment { StartOffset = start, EndOffset = end };
    }

    /// <summary>
    /// What the user cannot change: each run, with the line ends that part it from the lines around. Text can be
    /// typed at the end of the line before and at the start of the line after, not at the ends of the text itself.
    /// </summary>
    private IEnumerable<(int Start, int End)> Protected()
    {
        foreach (var run in runs)
        {
            if (run.IsAlive)
            {
                yield return (run.First.PreviousLine?.EndOffset ?? -1, run.Last.NextLine?.Offset ?? int.MaxValue);
            }
        }
    }

    private sealed class MarkElement(int documentLength) : FormattedTextElement(Mark, documentLength)
    {
        /// <inheritdoc/>
        public override TextRun CreateTextRun(int startVisualColumn, ITextRunConstructionContext context)
        {
            TextRunProperties.SetForegroundBrush(MarkBrush);
            return base.CreateTextRun(startVisualColumn, context);
        }
    }

    private sealed record Run(DocumentLine First, DocumentLine Last, CollapsedLineSection? Collapsed)
    {
        public bool IsAlive => !First.IsDeleted && !Last.IsDeleted;

        public int Length => Last.EndOffset - First.Offset;
    }
}
