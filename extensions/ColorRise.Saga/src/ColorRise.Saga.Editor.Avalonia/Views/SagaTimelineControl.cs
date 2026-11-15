using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ColorRise.Saga.Editor.Player;

namespace ColorRise.Saga.Editor.Avalonia.Views;

/// <summary>
/// The story timeline, drawn: a column per step of the run, with its number and the kind of step in the header, and a row per
/// lane, its spans as bars and its marks as shapes. The column of the cursor is lit, the steps after it are dimmed, the step a
/// replay stopped before is red, and a dot marks the steps that play differently since the last replay.
/// </summary>
/// <remarks>
/// A click on a column, or the left and right arrows, runs <see cref="GoToStepCommand"/> with the index of the step. The tooltip
/// tells what the item under the pointer is, or what the step did. In a <see cref="ScrollViewer"/>, the titles of the lanes and
/// the header stay in view.
/// </remarks>
public sealed class SagaTimelineControl : Control
{
    public static readonly StyledProperty<IReadOnlyList<TimelineStep>?> StepsProperty =
        AvaloniaProperty.Register<SagaTimelineControl, IReadOnlyList<TimelineStep>?>(nameof(Steps));

    public static readonly StyledProperty<IReadOnlyList<TimelineLane>?> LanesProperty =
        AvaloniaProperty.Register<SagaTimelineControl, IReadOnlyList<TimelineLane>?>(nameof(Lanes));

    public static readonly StyledProperty<int> CurrentStepProperty =
        AvaloniaProperty.Register<SagaTimelineControl, int>(nameof(CurrentStep), -1);

    public static readonly StyledProperty<ICommand?> GoToStepCommandProperty =
        AvaloniaProperty.Register<SagaTimelineControl, ICommand?>(nameof(GoToStepCommand));

    public static readonly StyledProperty<SagaTimelineBrushes?> BrushesProperty =
        AvaloniaProperty.Register<SagaTimelineControl, SagaTimelineBrushes?>(nameof(Brushes));

    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.Register<SagaTimelineControl, IBrush?>(nameof(Background));

    private const double TitleWidth = 120;
    private const double StepWidth = 72;
    private const double HeaderHeight = 36;
    private const double LaneHeight = 24;
    private const double MarkSize = 9;

    private ScrollViewer? scroll;

    static SagaTimelineControl()
    {
        AffectsMeasure<SagaTimelineControl>(StepsProperty, LanesProperty);
        AffectsRender<SagaTimelineControl>(StepsProperty, LanesProperty, CurrentStepProperty, BrushesProperty, BackgroundProperty);
        FocusableProperty.OverrideDefaultValue<SagaTimelineControl>(true);
    }

    public IReadOnlyList<TimelineStep>? Steps { get => GetValue(StepsProperty); set => SetValue(StepsProperty, value); }

    public IReadOnlyList<TimelineLane>? Lanes { get => GetValue(LanesProperty); set => SetValue(LanesProperty, value); }

    /// <summary>
    /// The index of the step the run is at, or -1.
    /// </summary>
    public int CurrentStep { get => GetValue(CurrentStepProperty); set => SetValue(CurrentStepProperty, value); }

    /// <summary>
    /// Moves the run to a step: runs with the index of the step clicked.
    /// </summary>
    public ICommand? GoToStepCommand { get => GetValue(GoToStepCommandProperty); set => SetValue(GoToStepCommandProperty, value); }

    /// <summary>
    /// The colors of the items, by kind, and of the cursor, the future and the divergence.
    /// </summary>
    public SagaTimelineBrushes? Brushes { get => GetValue(BrushesProperty); set => SetValue(BrushesProperty, value); }

    /// <summary>
    /// The background of the header and of the titles of the lanes, which the steps scroll under.
    /// </summary>
    public IBrush? Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }

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
            Dispatcher.UIThread.Post(() => this.BringIntoView(new Rect(ColumnX(CurrentStep) - TitleWidth, 0, StepWidth + TitleWidth, HeaderHeight)), DispatcherPriority.Background);
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
        var brushes = Brushes ?? SagaTimelineBrushes.Fallback;
        var foreground = TextElement.GetForeground(this) ?? global::Avalonia.Media.Brushes.White;
        var typeface = new Typeface(TextElement.GetFontFamily(this));
        var bounds = new Rect(Bounds.Size);
        var offset = scroll?.Offset ?? default;
        var height = HeaderHeight + lanes.Count * LaneHeight;
        var header = Background ?? brushes.Header;

        // Hit testable everywhere; the lanes on alternate backgrounds
        context.FillRectangle(global::Avalonia.Media.Brushes.Transparent, bounds);
        for (var lane = 1; lane < lanes.Count; lane += 2)
            context.FillRectangle(brushes.AlternateLane, new Rect(0, LaneY(lane), bounds.Width, LaneHeight));

        // The columns: the cursor lit, the divergence red, behind the items
        foreach (var step in steps)
        {
            var column = new Rect(ColumnX(step.Index), 0, StepWidth, height);
            if (step.Index == CurrentStep)
                context.FillRectangle(brushes.Cursor, column);
            if (step.IsDiverged)
                context.FillRectangle(brushes.Diverged, column);
        }

        for (var lane = 0; lane < lanes.Count; ++lane)
            DrawLane(context, lanes[lane], LaneY(lane), brushes, typeface);

        // The steps after the cursor, dimmed over their items
        if (CurrentStep >= 0 && CurrentStep < steps.Count - 1)
            context.FillRectangle(brushes.Future, new Rect(ColumnX(CurrentStep + 1), HeaderHeight, (steps.Count - CurrentStep - 1) * StepWidth, height - HeaderHeight));

        // The header and the titles stay in view while scrolling
        context.FillRectangle(header, new Rect(0, offset.Y, bounds.Width, HeaderHeight));
        foreach (var step in steps)
            DrawHeader(context, step, offset.Y, brushes, foreground, typeface);
        context.FillRectangle(header, new Rect(offset.X, offset.Y, TitleWidth, Math.Max(height - offset.Y, HeaderHeight)));
        for (var lane = 0; lane < lanes.Count; ++lane)
        {
            var y = LaneY(lane);
            if (y + LaneHeight <= offset.Y + HeaderHeight)
                continue;
            DrawText(context, lanes[lane].Title, new Rect(offset.X + 6, y, TitleWidth - 10, LaneHeight), foreground, typeface, 12, FontWeight.Normal);
        }
    }

    private void DrawHeader(DrawingContext context, TimelineStep step, double top, SagaTimelineBrushes brushes, IBrush foreground, Typeface typeface)
    {
        var x = ColumnX(step.Index);
        if (step.Index == CurrentStep)
            context.FillRectangle(brushes.Cursor, new Rect(x, top, StepWidth, HeaderHeight));
        var text = step.IsDiverged ? brushes.DivergedText : step.IsFuture ? brushes.FutureText : foreground;
        var weight = step.Index == CurrentStep ? FontWeight.Bold : FontWeight.Normal;
        DrawText(context, step.Index.ToString(CultureInfo.CurrentCulture), new Rect(x, top + 2, StepWidth, 14), text, typeface, 10, weight, TextAlignment.Center);
        DrawText(context, step.Glyph, new Rect(x, top + 15, StepWidth, 18), text, typeface, 14, weight, TextAlignment.Center);
        if (step.PlaysDifferently)
            context.DrawEllipse(brushes.PlaysDifferently, null, new Point(x + StepWidth - 10, top + 9), 3.5, 3.5);
        context.DrawLine(new Pen(brushes.Grid, 1), new Point(x + 0.5, top), new Point(x + 0.5, top + HeaderHeight));
    }

    private static void DrawLane(DrawingContext context, TimelineLane lane, double y, SagaTimelineBrushes brushes, Typeface typeface)
    {
        foreach (var item in lane.Items.Where(x => !x.IsMark))
        {
            var bar = new Rect(ColumnX(item.Start) + 3, y + 3, (item.End - item.Start + 1) * StepWidth - 6, LaneHeight - 6);
            context.DrawRectangle(brushes.Of(item.Kind), null, bar, 3, 3);
            if (item.IsOpen)
            {
                // Still holding after the last step: an arrow at its end
                var arrow = new StreamGeometry();
                using (var geometry = arrow.Open())
                {
                    geometry.BeginFigure(new Point(bar.Right - 9, bar.Top + 4), true);
                    geometry.LineTo(new Point(bar.Right - 4, bar.Center.Y));
                    geometry.LineTo(new Point(bar.Right - 9, bar.Bottom - 4));
                    geometry.EndFigure(true);
                }
                context.DrawGeometry(brushes.SpanText, null, arrow);
            }
            DrawText(context, item.Label, bar.Deflate(new Thickness(5, 0, item.IsOpen ? 12 : 5, 0)), brushes.SpanText, typeface, 11, FontWeight.Normal);
        }

        // The marks of a step side by side, their labels after them
        foreach (var cell in lane.Items.Where(x => x.IsMark).GroupBy(x => x.Start))
        {
            var x = ColumnX(cell.Key) + 6;
            var center = y + LaneHeight / 2;
            foreach (var mark in cell)
            {
                DrawMark(context, mark.Kind, new Point(x + MarkSize / 2, center), brushes.Of(mark.Kind));
                x += MarkSize + 2;
            }
            DrawText(context, string.Join(", ", cell.Select(m => m.Label)), new Rect(x + 1, y, ColumnX(cell.Key) + StepWidth - x - 3, LaneHeight),
                brushes.Of(cell.Last().Kind), typeface, 11, FontWeight.Normal);
        }
    }

    private static void DrawMark(DrawingContext context, TimelineItemKind kind, Point center, IBrush brush)
    {
        const double r = MarkSize / 2;
        switch (kind)
        {
            case TimelineItemKind.Event or TimelineItemKind.Ended:
                Polygon(context, brush, new Point(center.X, center.Y - r - 1), new Point(center.X + r + 1, center.Y), new Point(center.X, center.Y + r + 1), new Point(center.X - r - 1, center.Y));
                break;
            case TimelineItemKind.Variable or TimelineItemKind.Answer:
                context.DrawRectangle(brush, null, new Rect(center.X - r, center.Y - r, MarkSize, MarkSize), 1.5, 1.5);
                break;
            case TimelineItemKind.Signal:
                Polygon(context, brush, new Point(center.X - r, center.Y - r), new Point(center.X + r, center.Y), new Point(center.X - r, center.Y + r));
                break;
            default:
                context.DrawEllipse(brush, null, center, r, r);
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
                geometry.LineTo(point);
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

    // What the item under a point is, else what the step of its column did
    private string? TipAt(Point point)
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

    private static double ColumnX(int step) => TitleWidth + step * StepWidth;

    private static double LaneY(int lane) => HeaderHeight + lane * LaneHeight;
}

/// <summary>
/// The colors of the story timeline; <c>Themes/SagaStyles.axaml</c> sets them.
/// </summary>
public sealed class SagaTimelineBrushes
{
    /// <summary>
    /// The colors used when none are given.
    /// </summary>
    public static SagaTimelineBrushes Fallback { get; } = new();

    public IBrush Act { get; set; } = Brushes.SlateBlue;

    public IBrush Scenario { get; set; } = Brushes.Teal;

    public IBrush Offered { get; set; } = Brushes.CadetBlue;

    public IBrush Active { get; set; } = Brushes.DarkCyan;

    public IBrush Stage { get; set; } = Brushes.PaleTurquoise;

    public IBrush Succeeded { get; set; } = Brushes.LimeGreen;

    public IBrush Failed { get; set; } = Brushes.IndianRed;

    public IBrush Dialogue { get; set; } = Brushes.DarkOrange;

    public IBrush Choice { get; set; } = Brushes.SandyBrown;

    public IBrush Event { get; set; } = Brushes.Gold;

    public IBrush Variable { get; set; } = Brushes.LightSlateGray;

    public IBrush Answer { get; set; } = Brushes.Orchid;

    public IBrush Signal { get; set; } = Brushes.DeepSkyBlue;

    /// <summary>
    /// The text on a span.
    /// </summary>
    public IBrush SpanText { get; set; } = Brushes.White;

    /// <summary>
    /// The background of the header and of the titles, without <see cref="SagaTimelineControl.Background"/>.
    /// </summary>
    public IBrush Header { get; set; } = Brushes.Black;

    public IBrush AlternateLane { get; set; } = new SolidColorBrush(Color.FromArgb(0x10, 0xFF, 0xFF, 0xFF));

    public IBrush Grid { get; set; } = Brushes.DimGray;

    public IBrush Cursor { get; set; } = new SolidColorBrush(Color.FromArgb(0x40, 0x42, 0xA5, 0xF5));

    /// <summary>
    /// Laid over the steps after the cursor.
    /// </summary>
    public IBrush Future { get; set; } = new SolidColorBrush(Color.FromArgb(0x90, 0x20, 0x20, 0x20));

    public IBrush FutureText { get; set; } = Brushes.Gray;

    public IBrush Diverged { get; set; } = new SolidColorBrush(Color.FromArgb(0x50, 0xE5, 0x39, 0x35));

    public IBrush DivergedText { get; set; } = Brushes.IndianRed;

    /// <summary>
    /// The dot of a step that plays differently since the last replay.
    /// </summary>
    public IBrush PlaysDifferently { get; set; } = Brushes.Orange;

    /// <summary>
    /// The color of an item by its kind.
    /// </summary>
    public IBrush Of(TimelineItemKind kind) => kind switch
    {
        TimelineItemKind.Act => Act,
        TimelineItemKind.Running or TimelineItemKind.Ended => Scenario,
        TimelineItemKind.Offered => Offered,
        TimelineItemKind.Active => Active,
        TimelineItemKind.Stage => Stage,
        TimelineItemKind.Succeeded => Succeeded,
        TimelineItemKind.Failed => Failed,
        TimelineItemKind.Dialogue => Dialogue,
        TimelineItemKind.Choice => Choice,
        TimelineItemKind.Event => Event,
        TimelineItemKind.Variable => Variable,
        TimelineItemKind.Answer => Answer,
        _ => Signal,
    };
}
