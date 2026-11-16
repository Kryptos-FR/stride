using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using ColorRise.Skein.Editor.Avalonia.Views;

namespace ColorRise.Skein.Editor.Avalonia.Text;

/// <summary>
/// Lights, in the text of a script, the line the run of the dialogue player is at.
/// </summary>
internal sealed class YarnPlayLineRenderer(TextDocument document) : IBackgroundRenderer
{
    /// <summary>
    /// The line of the run, from 1, or null when the run is not in the text.
    /// </summary>
    public int? Line { get; set; }

    /// <inheritdoc/>
    public KnownLayer Layer => KnownLayer.Background;

    /// <inheritdoc/>
    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (Line is not { } number || number < 1 || number > document.LineCount || !textView.VisualLinesValid)
            return;

        var line = document.GetLineByNumber(number);
        foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, line, extendToFullWidthAtLineEnd: true))
        {
            // The whole width of the pane, with a bar at its start
            drawingContext.FillRectangle(PlayBrushes.CurrentLine, new Rect(0, rect.Top, textView.Bounds.Width, rect.Height));
            drawingContext.FillRectangle(PlayBrushes.Current, new Rect(0, rect.Top, 3, rect.Height));
        }
    }
}
