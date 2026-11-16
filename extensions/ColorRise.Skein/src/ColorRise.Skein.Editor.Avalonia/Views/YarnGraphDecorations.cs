using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using ColorRise.Skein.Editor.Graph;
using ColorRise.Skein.Editor.ViewModels;
using Stride.Core.Presentation.Avalonia.Controls;

namespace ColorRise.Skein.Editor.Avalonia.Views;

/// <summary>
/// What the graph of a script shows behind its cards: its background, and a frame around the cards that go together
/// (the nodes of a node group, the nodes of a cluster, the nodes with one value of the header the cards are grouped by).
/// </summary>
/// <remarks>
/// The graph control has no layer for a plugin to draw on, so this control sits under a graph with a transparent
/// background and follows its zoom and its offset.
/// </remarks>
public sealed class YarnGraphDecorations : Control
{
    private const double LabelSize = 11;

    private NodeGraphControl? graph;
    private YarnScriptEditorViewModel? editor;

    /// <summary>
    /// Gives the graph to draw behind and the editor whose cards it shows; null for none.
    /// </summary>
    public void Attach(NodeGraphControl? graph, YarnScriptEditorViewModel? editor)
    {
        if (this.graph is not null)
        {
            this.graph.PropertyChanged -= OnGraphPropertyChanged;
            this.graph.LayoutUpdated -= OnGraphLayoutUpdated;
        }

        this.graph = graph;
        this.editor = editor;
        if (graph is not null)
        {
            graph.PropertyChanged += OnGraphPropertyChanged;
            graph.LayoutUpdated += OnGraphLayoutUpdated;
        }

        InvalidateVisual();
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.FillRectangle(YarnPalette.GraphBackground, new Rect(Bounds.Size));
        if (graph is null || editor is null)
            return;

        using var clip = context.PushClip(new Rect(Bounds.Size));
        foreach (var frame in Frames(editor))
        {
            Rect? area = null;
            foreach (var card in frame.Cards)
            {
                if (graph.GetNodePosition(card) is { } position && graph.ContainerFromNode(card) is { } container)
                {
                    var rect = new Rect(position, container.Bounds.Size);
                    area = area?.Union(rect) ?? rect;
                }
            }

            if (area is not { } found)
                continue;

            var topLeft = graph.GraphToView(found.TopLeft);
            var view = new Rect(topLeft, found.Size * graph.Zoom).Inflate(frame.Padding * graph.Zoom);
            var radius = 8 * graph.Zoom;
            context.DrawRectangle(new ImmutableSolidColorBrush(frame.Color, 0.10), new ImmutablePen(new ImmutableSolidColorBrush(frame.Color, 0.75), 1.5), view, radius, radius);

            var label = new FormattedText(frame.Label, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, Typeface.Default, LabelSize, new ImmutableSolidColorBrush(frame.Color));
            context.DrawText(label, new Point(view.X + 6, view.Y - label.Height - 1));
        }
    }

    /// <summary>
    /// The frames of the cards of an editor: the widest first, so that a frame inside another is drawn over it.
    /// </summary>
    internal static IReadOnlyList<Frame> Frames(YarnScriptEditorViewModel editor)
    {
        var frames = new List<Frame>();
        if (editor.GroupBy is { } header && header != "cluster")
        {
            foreach (var group in editor.Cards.Where(c => !string.IsNullOrEmpty(c.GroupValue)).GroupBy(c => c.GroupValue!, StringComparer.Ordinal))
            {
                frames.Add(new Frame($"{header}: {group.Key}", YarnPalette.OfValue(group.Key), 40, [.. group]));
            }
        }

        foreach (var group in editor.Cards.Where(c => c.Cluster is not null).GroupBy(c => c.Cluster!, StringComparer.Ordinal))
        {
            frames.Add(new Frame(group.Key, YarnPalette.OfValue(group.Key), 20, [.. group]));
        }

        foreach (var group in editor.Cards.Where(c => c.GroupCount > 1).GroupBy(c => c.Title, StringComparer.Ordinal))
        {
            frames.Add(new Frame(group.Key, YarnPalette.NodeGroupFrame, 10, [.. group]));
        }

        return frames;
    }

    private void OnGraphPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == NodeGraphControl.ZoomProperty || e.Property == NodeGraphControl.OffsetProperty)
        {
            InvalidateVisual();
        }
    }

    // A card that is dragged, or that changes its size, moves its frames
    private void OnGraphLayoutUpdated(object? sender, EventArgs e) => InvalidateVisual();

    /// <summary>
    /// A frame around cards.
    /// </summary>
    /// <param name="Label">What the frame says at its top.</param>
    /// <param name="Color">The colour of its line and of its label.</param>
    /// <param name="Padding">How far the frame is from its cards, in the coordinates of the graph.</param>
    /// <param name="Cards">The cards inside.</param>
    internal sealed record Frame(string Label, Color Color, double Padding, IReadOnlyList<YarnNodeCardViewModel> Cards);
}
