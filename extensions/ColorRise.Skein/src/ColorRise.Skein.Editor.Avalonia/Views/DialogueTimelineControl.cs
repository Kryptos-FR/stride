using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ColorRise.Skein.Editor.Player;

namespace ColorRise.Skein.Editor.Avalonia.Views;

/// <summary>
/// The dialogue timeline, drawn: a column per step of the run, with its number and the kind of step in the header, and a
/// row per lane, its spans as bars and its marks as shapes. The column of the cursor is lit, the steps after it are
/// dimmed, and a dot marks the steps the last replay made or made differ.
/// </summary>
/// <remarks>
/// A click on a column, or the left and right arrows, runs <see cref="GoToStepCommand"/> with the index of the step. The
/// tooltip tells what the item under the pointer is, or what the step did. In a <see cref="ScrollViewer"/>, the titles of
/// the lanes and the header stay in view.
/// </remarks>
public sealed class DialogueTimelineControl : Control
{
    /// <summary>
    /// The steps of the run.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<TimelineStep>?> StepsProperty =
        AvaloniaProperty.Register<DialogueTimelineControl, IReadOnlyList<TimelineStep>?>(nameof(Steps));

    /// <summary>
    /// The lanes of the run.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<TimelineLane>?> LanesProperty =
        AvaloniaProperty.Register<DialogueTimelineControl, IReadOnlyList<TimelineLane>?>(nameof(Lanes));

    /// <summary>
    /// The index of the step the run is at, or -1.
    /// </summary>
    public static readonly StyledProperty<int> CurrentStepProperty =
        AvaloniaProperty.Register<DialogueTimelineControl, int>(nameof(CurrentStep), -1);

    /// <summary>
    /// Moves the run to a step: runs with the index of the step clicked.
    /// </summary>
    public static readonly StyledProperty<ICommand?> GoToStepCommandProperty =
        AvaloniaProperty.Register<DialogueTimelineControl, ICommand?>(nameof(GoToStepCommand));

    /// <summary>
    /// The background of the header and of the titles of the lanes, which the steps scroll under.
    /// </summary>
    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.Register<DialogueTimelineControl, IBrush?>(nameof(Background));

    internal const double TitleWidth = 84;
    internal const double StepWidth = 72;
    internal const double HeaderHeight = 36;
    internal const double LaneHeight = 24;
    private const double MarkSize = 9;

    private ScrollViewer? scroll;

    static DialogueTimelineControl()
    {
        AffectsMeasure<DialogueTimelineControl>(StepsProperty, LanesProperty);
        AffectsRender<DialogueTimelineControl>(StepsProperty, LanesProperty, CurrentStepProperty, BackgroundProperty);
        FocusableProperty.OverrideDefaultValue<DialogueTimelineControl>(true);
    }

    public IReadOnlyList<TimelineStep>? Steps { get => GetValue(StepsProperty); set => SetValue(StepsProperty, value); }

    public IReadOnlyList<TimelineLane>? Lanes { get => GetValue(LanesProperty); set => SetValue(LanesProperty, value); }

    public int CurrentStep { get => GetValue(CurrentStepProperty); set => SetValue(CurrentStepProperty, value); }

    public ICommand? GoToStepCommand { get => GetValue(GoToStepCommandProperty); set => SetValue(GoToStepCommandProperty, value); }

    public IBrush? Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }

    /// <summary>
    /// The middle of the column of a step, in the control.
    /// </summary>
    public Point ColumnCentre(int step) => new(ColumnX(step) + StepWidth / 2, HeaderHeight / 2);

    /// <summary>
    /// What the tooltip tells at a point of the control: the item under it, else what the step of its column did.
    /// </summary>
    public string? TipAt(Point point)
    {
        if (StepAt(point) is not { } step)
            return null;

        var lane = (int)Math.Floor((point.Y - HeaderHeight) / LaneHeight);
        if (point.Y > HeaderHeight + (scroll?.Offset.Y ?? 0) && Lanes is { } lanes && lane >= 0 && lane < lanes.Count)
        {
            var items = lanes[lane].Items.Where(x => x.Start <= step && step <= x.End).ToList();
            if (items.Count > 0)
                return string.Join("\n", items.Select(x => x.Tip));
        }

        return Steps![step].Tip;
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        scroll = this.FindAncestorOfType<ScrollViewer>();
        if (scroll is not null)
            scroll.ScrollChanged += OnScrollChanged;
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (scroll is not null)
            scroll.ScrollChanged -= OnScrollChanged;

        scroll = null;
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        // The cursor in view, once the steps are laid out
        if (change.Property == CurrentStepProperty && CurrentStep >= 0)
        {
            Dispatcher.UIThread.Post(() => this.BringIntoView(new Rect(ColumnX(CurrentStep) - TitleWidth, 0, StepWidth + TitleWidth, HeaderHeight)), DispatcherPriority.Background);
        }
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
        => new(TitleWidth + (Steps?.Count ?? 0) * StepWidth + 8, HeaderHeight + (Lanes?.Count ?? 0) * LaneHeight + 4);

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || StepAt(e.GetPosition(this)) is not { } step)
            return;

        Focus();
        GoTo(step);
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var tip = TipAt(e.GetPosition(this));
        if (!Equals(ToolTip.GetTip(this), tip))
            ToolTip.SetTip(this, tip);
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var count = Steps?.Count ?? 0;
        var step = e.Key switch
        {
            Key.Left when CurrentStep > 0 => CurrentStep - 1,
            Key.Right when CurrentStep < count - 1 => CurrentStep + 1,
            Key.Home when count > 0 => 0,
            Key.End when count > 0 => count - 1,
            _ => (int?)null,
        };
        if (step is { } index)
        {
            GoTo(index);
            e.Handled = true;
        }
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var steps = Steps ?? [];
        var lanes = Lanes ?? [];
        var foreground = TextElement.GetForeground(this) ?? Brushes.White;
        var typeface = new Typeface(TextElement.GetFontFamily(this));
        var bounds = new Rect(Bounds.Size);
        var offset = scroll?.Offset ?? default;
        var height = HeaderHeight + lanes.Count * LaneHeight;
        var header = Background ?? PlayBrushes.Header;

        // Hit testable everywhere; the lanes on alternate backgrounds
        context.FillRectangle(Brushes.Transparent, bounds);
        for (var lane = 1; lane < lanes.Count; lane += 2)
        {
            context.FillRectangle(PlayBrushes.AlternateLane, new Rect(0, LaneY(lane), bounds.Width, LaneHeight));
        }

        // The column of the cursor, behind the items
        if (CurrentStep >= 0 && CurrentStep < steps.Count)
            context.FillRectangle(PlayBrushes.Cursor, new Rect(ColumnX(CurrentStep), 0, StepWidth, height));

        for (var lane = 0; lane < lanes.Count; lane++)
        {
            DrawLane(context, lanes[lane], LaneY(lane), foreground, typeface);
        }

        // The steps after the cursor, faded into the background over their items
        if (CurrentStep >= 0 && CurrentStep < steps.Count - 1)
        {
            var future = header is ISolidColorBrush solid ? new SolidColorBrush(solid.Color, 0.6) : PlayBrushes.AlternateLane;
            context.FillRectangle(future, new Rect(ColumnX(CurrentStep + 1), HeaderHeight, (steps.Count - CurrentStep - 1) * StepWidth, height - HeaderHeight));
        }

        // The header and the titles stay in view while scrolling
        context.FillRectangle(header, new Rect(0, offset.Y, bounds.Width, HeaderHeight));
        foreach (var step in steps)
        {
            DrawHeader(context, step, offset.Y, foreground, typeface);
        }

        context.FillRectangle(header, new Rect(offset.X, offset.Y, TitleWidth, Math.Max(height - offset.Y, HeaderHeight)));
        for (var lane = 0; lane < lanes.Count; lane++)
        {
            var y = LaneY(lane);
            if (y + LaneHeight <= offset.Y + HeaderHeight)
                continue;

            DrawText(context, lanes[lane].Title, new Rect(offset.X + 6, y, TitleWidth - 10, LaneHeight), foreground, typeface, 12, FontWeight.Normal);
        }
    }

    private void DrawHeader(DrawingContext context, TimelineStep step, double top, IBrush foreground, Typeface typeface)
    {
        var x = ColumnX(step.Index);
        if (step.Index == CurrentStep)
            context.FillRectangle(PlayBrushes.Cursor, new Rect(x, top, StepWidth, HeaderHeight));

        var text = step.IsFuture ? PlayBrushes.Faint : foreground;
        var weight = step.Index == CurrentStep ? FontWeight.Bold : FontWeight.Normal;
        DrawText(context, step.Index.ToString(CultureInfo.CurrentCulture), new Rect(x, top + 2, StepWidth, 14), text, typeface, 10, weight, TextAlignment.Center);
        DrawText(context, step.Glyph, new Rect(x, top + 15, StepWidth, 18), text, typeface, 14, weight, TextAlignment.Center);
        if (step.Changed)
            context.DrawEllipse(PlayBrushes.Changed, null, new Point(x + StepWidth - 10, top + 9), 3.5, 3.5);

        context.DrawLine(new Pen(PlayBrushes.Grid, 1), new Point(x + 0.5, top), new Point(x + 0.5, top + HeaderHeight));
    }

    private static void DrawLane(DrawingContext context, TimelineLane lane, double y, IBrush foreground, Typeface typeface)
    {
        foreach (var item in lane.Items.Where(x => !x.IsMark))
        {
            var bar = new Rect(ColumnX(item.Start) + 3, y + 3, (item.End - item.Start + 1) * StepWidth - 6, LaneHeight - 6);
            context.DrawRectangle(PlayBrushes.Of(item.Kind), null, bar, 3, 3);
            if (item.IsOpen)
            {
                // Still holding after the last step: an arrow at its end
                Polygon(context, PlayBrushes.SpanText, new Point(bar.Right - 9, bar.Top + 4), new Point(bar.Right - 4, bar.Center.Y), new Point(bar.Right - 9, bar.Bottom - 4));
            }

            DrawText(context, item.Label, bar.Deflate(new Thickness(5, 0, item.IsOpen ? 12 : 5, 0)), PlayBrushes.SpanText, typeface, 11, FontWeight.Normal);
        }

        // The marks of a step side by side, their labels after them; on a bar (a pick in its node) after the bar's label
        foreach (var cell in lane.Items.Where(x => x.IsMark).GroupBy(x => x.Start))
        {
            var onBar = lane.Items.Any(x => !x.IsMark && x.Start <= cell.Key && cell.Key <= x.End);
            var x = ColumnX(cell.Key) + (onBar ? StepWidth - 8 - cell.Count() * (MarkSize + 2) : 6);
            var centre = y + LaneHeight / 2;
            foreach (var mark in cell)
            {
                DrawMark(context, mark.Kind, new Point(x + MarkSize / 2, centre), PlayBrushes.Of(mark.Kind));
                x += MarkSize + 2;
            }

            if (!onBar)
            {
                DrawText(context, string.Join(", ", cell.Select(m => m.Label)), new Rect(x + 1, y, ColumnX(cell.Key) + StepWidth - x - 3, LaneHeight), foreground, typeface, 11, FontWeight.Normal);
            }
        }
    }

    private static void DrawMark(DrawingContext context, TimelineItemKind kind, Point centre, IBrush brush)
    {
        const double r = MarkSize / 2;
        switch (kind)
        {
            case TimelineItemKind.Pick or TimelineItemKind.Command:
                Polygon(context, brush, new Point(centre.X, centre.Y - r - 1), new Point(centre.X + r + 1, centre.Y), new Point(centre.X, centre.Y + r + 1), new Point(centre.X - r - 1, centre.Y));
                break;
            case TimelineItemKind.Variable or TimelineItemKind.Answer or TimelineItemKind.Set:
                context.DrawRectangle(brush, null, new Rect(centre.X - r, centre.Y - r, MarkSize, MarkSize), 1.5, 1.5);
                break;
            case TimelineItemKind.Choice:
                Polygon(context, brush, new Point(centre.X - r, centre.Y - r), new Point(centre.X + r, centre.Y), new Point(centre.X - r, centre.Y + r));
                break;
            default:
                context.DrawEllipse(brush, null, centre, r, r);
                break;
        }
    }

    private static void Polygon(DrawingContext context, IBrush brush, params Point[] points)
    {
        var shape = new StreamGeometry();
        using (var geometry = shape.Open())
        {
            geometry.BeginFigure(points[0], true);
            foreach (var point in points.Skip(1))
            {
                geometry.LineTo(point);
            }

            geometry.EndFigure(true);
        }

        context.DrawGeometry(brush, null, shape);
    }

    private static void DrawText(DrawingContext context, string text, Rect area, IBrush brush, Typeface typeface, double size, FontWeight weight,
        TextAlignment alignment = TextAlignment.Left)
    {
        if (area.Width <= 4 || text.Length == 0)
            return;

        var formatted = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface(typeface.FontFamily, weight: weight), size, brush)
        {
            MaxTextWidth = area.Width,
            MaxLineCount = 1,
            Trimming = TextTrimming.CharacterEllipsis,
            TextAlignment = alignment,
        };
        context.DrawText(formatted, new Point(area.X, area.Y + (area.Height - formatted.Height) / 2));
    }

    private void GoTo(int step)
    {
        if (GoToStepCommand is { } command && command.CanExecute(step))
            command.Execute(step);
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e) => InvalidateVisual();

    // The step of the column under a point, or null
    private int? StepAt(Point point)
    {
        var index = (int)Math.Floor((point.X - TitleWidth) / StepWidth);
        return point.X >= TitleWidth + (scroll?.Offset.X ?? 0) && index >= 0 && index < (Steps?.Count ?? 0) ? index : null;
    }

    private static double ColumnX(int step) => TitleWidth + step * StepWidth;

    private static double LaneY(int lane) => HeaderHeight + lane * LaneHeight;
}
