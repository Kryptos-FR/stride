using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ColorRise.Tessera.Editor.Canvas;
using RectangleF = Stride.Core.Mathematics.RectangleF;
using StrideColor = Stride.Core.Mathematics.Color;
using Vector2 = Stride.Core.Mathematics.Vector2;

namespace ColorRise.Tessera.Editor.Avalonia.Controls;

/// <summary>
/// Draws a scene with hard pixels, zooms around the cursor with the wheel, pans with the middle button or with Space and
/// the left button, and tells the rest of the pointer and the keys to its input.
/// </summary>
public sealed class TileCanvas : Control
{
    /// <summary>
    /// What the canvas draws.
    /// </summary>
    public static readonly StyledProperty<ICanvasScene?> SceneProperty = AvaloniaProperty.Register<TileCanvas, ICanvasScene?>(nameof(Scene));

    /// <summary>
    /// What hears the pointer and the keys.
    /// </summary>
    public static readonly StyledProperty<ICanvasInput?> InputProperty = AvaloniaProperty.Register<TileCanvas, ICanvasInput?>(nameof(Input));

    /// <summary>
    /// The zoom and the pan; the canvas has a view of its own when none is given.
    /// </summary>
    public static readonly StyledProperty<CanvasView> ViewProperty = AvaloniaProperty.Register<TileCanvas, CanvasView>(nameof(View), coerce: (canvas, view) => view ?? ((TileCanvas)canvas).ownView);

    /// <summary>
    /// Whether the grid of the scene is drawn.
    /// </summary>
    public static readonly StyledProperty<bool> ShowGridProperty = AvaloniaProperty.Register<TileCanvas, bool>(nameof(ShowGrid), true);

    /// <summary>
    /// The colour of the grid's lines.
    /// </summary>
    public static readonly StyledProperty<Color> GridColorProperty = AvaloniaProperty.Register<TileCanvas, Color>(nameof(GridColor), Color.FromArgb(64, 255, 255, 255));

    /// <summary>
    /// Whether the canvas shows all of the scene the first time it has a size and the scene has bounds.
    /// </summary>
    public static readonly StyledProperty<bool> FitOnFirstLayoutProperty = AvaloniaProperty.Register<TileCanvas, bool>(nameof(FitOnFirstLayout));

    /// <summary>
    /// What is drawn under the scene.
    /// </summary>
    public static readonly StyledProperty<IBrush?> BackgroundProperty = AvaloniaProperty.Register<TileCanvas, IBrush?>(nameof(Background), new ImmutableSolidColorBrush(Color.FromRgb(38, 38, 38)));

    // Lines are one screen pixel wide; hatch lines are this many pixels apart
    private const double HatchSpacing = 6;
    // The smallest cell of the grid that is drawn, in screen pixels
    private const double SmallestGridCell = 4;

    private readonly CanvasView ownView = new();
    private readonly CanvasFrame frame = new();
    private (Vector2 Last, MouseButton Button)? pan;
    // The buttons whose press the input heard and whose release it has not
    private readonly HashSet<CanvasButton> held = [];
    // The turns of the wheel not yet given to the zoom, less than one step
    private double wheel;
    private bool spaceDown;
    private bool fitted;
    // A fit asked by Fit, or a centring asked by Centre, done once the scene has bounds and the canvas a size
    private bool fitAsked;
    private bool centreAsked;

    static TileCanvas()
    {
        AffectsRender<TileCanvas>(SceneProperty, ViewProperty, ShowGridProperty, GridColorProperty, BackgroundProperty);
        FocusableProperty.OverrideDefaultValue<TileCanvas>(true);
        ClipToBoundsProperty.OverrideDefaultValue<TileCanvas>(true);
    }

    /// <summary>
    /// Made by the editor.
    /// </summary>
    public TileCanvas()
    {
        CoerceValue(ViewProperty);
    }

    /// <inheritdoc cref="SceneProperty"/>
    public ICanvasScene? Scene
    {
        get => GetValue(SceneProperty);
        set => SetValue(SceneProperty, value);
    }

    /// <inheritdoc cref="InputProperty"/>
    public ICanvasInput? Input
    {
        get => GetValue(InputProperty);
        set => SetValue(InputProperty, value);
    }

    /// <inheritdoc cref="ViewProperty"/>
    public CanvasView View
    {
        get => GetValue(ViewProperty);
        set => SetValue(ViewProperty, value);
    }

    /// <inheritdoc cref="ShowGridProperty"/>
    public bool ShowGrid
    {
        get => GetValue(ShowGridProperty);
        set => SetValue(ShowGridProperty, value);
    }

    /// <inheritdoc cref="GridColorProperty"/>
    public Color GridColor
    {
        get => GetValue(GridColorProperty);
        set => SetValue(GridColorProperty, value);
    }

    /// <inheritdoc cref="FitOnFirstLayoutProperty"/>
    public bool FitOnFirstLayout
    {
        get => GetValue(FitOnFirstLayoutProperty);
        set => SetValue(FitOnFirstLayoutProperty, value);
    }

    /// <inheritdoc cref="BackgroundProperty"/>
    public IBrush? Background
    {
        get => GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    /// <summary>
    /// Shows all of the scene: now, or as soon as the scene has bounds and the canvas a size.
    /// </summary>
    public void Fit()
    {
        fitAsked = true;
        TryFit();
    }

    /// <summary>
    /// Puts the middle of the scene in the middle of the canvas at the zoom it has: now, or as soon as the scene has bounds
    /// and the canvas a size.
    /// </summary>
    public void Centre()
    {
        centreAsked = true;
        TryFit();
    }

    /// <summary>
    /// Draws the background, the sprites, the grid, then the shapes.
    /// </summary>
    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        if (Background is { } background)
        {
            context.FillRectangle(background, new Rect(size));
        }
        if (Scene is not { } scene)
        {
            return;
        }
        var view = View;
        frame.Clear();
        scene.Collect(view.Visible(new Vector2((float)size.Width, (float)size.Height)), view.Zoom, frame);

        using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None, EdgeMode = EdgeMode.Aliased }))
        {
            foreach (var sprite in frame.Sprites)
            {
                DrawSprite(context, view, sprite);
            }
            if (ShowGrid)
            {
                DrawGrid(context, view, scene.GridSize, size);
            }
            foreach (var shape in frame.Shapes)
            {
                DrawShape(context, view, shape);
            }
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SceneProperty)
        {
            if (change.OldValue is ICanvasScene old)
            {
                old.Changed -= SceneChanged;
            }
            if (change.NewValue is ICanvasScene scene && this.IsAttachedToVisualTree())
            {
                scene.Changed += SceneChanged;
            }
            TryFit();
        }
        else if (change.Property == ViewProperty)
        {
            if (change.OldValue is CanvasView old)
            {
                old.Changed -= ViewChanged;
            }
            if (change.NewValue is CanvasView view && this.IsAttachedToVisualTree())
            {
                view.Changed += ViewChanged;
            }
        }
        else if (change.Property == FitOnFirstLayoutProperty)
        {
            TryFit();
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (Scene is { } scene)
        {
            scene.Changed += SceneChanged;
        }
        View.Changed += ViewChanged;
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (Scene is { } scene)
        {
            scene.Changed -= SceneChanged;
        }
        View.Changed -= ViewChanged;
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc />
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        TryFit();
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        var point = e.GetCurrentPoint(this);
        var position = ToVector(point.Position);
        var kind = point.Properties.PointerUpdateKind;
        if (pan is not null)
        {
            return;
        }
        if (kind == PointerUpdateKind.MiddleButtonPressed || (spaceDown && kind == PointerUpdateKind.LeftButtonPressed))
        {
            pan = (position, kind == PointerUpdateKind.MiddleButtonPressed ? MouseButton.Middle : MouseButton.Left);
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }
        if (ButtonOf(kind) is { } button && Input is { } input && !spaceDown)
        {
            e.Pointer.Capture(this);
            held.Add(button);
            input.PointerPressed(View.ToCanvas(position), button, ModifiersOf(e.KeyModifiers));
            e.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var position = ToVector(e.GetPosition(this));
        if (pan is { } panning)
        {
            View.Pan(position - panning.Last);
            pan = (position, panning.Button);
            return;
        }
        if (!spaceDown)
        {
            Input?.PointerMoved(View.ToCanvas(position), ModifiersOf(e.KeyModifiers));
        }
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (pan is { } panning)
        {
            if (e.InitialPressMouseButton == panning.Button)
            {
                pan = null;
                e.Pointer.Capture(null);
                e.Handled = true;
            }
            return;
        }
        var button = e.InitialPressMouseButton switch
        {
            MouseButton.Left => CanvasButton.Left,
            MouseButton.Right => (CanvasButton?)CanvasButton.Right,
            _ => null,
        };
        if (button is { } released && Input is { } input && !spaceDown)
        {
            held.Remove(released);
            input.PointerReleased(View.ToCanvas(ToVector(e.GetPosition(this))), released, ModifiersOf(e.KeyModifiers));
            e.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (pan is null)
        {
            Input?.PointerLeft();
        }
    }

    /// <inheritdoc />
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        pan = null;
        // The capture also ends after each release, when nothing is held any more
        if (held.Count > 0)
        {
            held.Clear();
            Input?.PointerCaptureLost();
        }
    }

    /// <inheritdoc />
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        // A touchpad gives small deltas: they add up, and each whole one is a step of zoom
        wheel += e.Delta.Y;
        var steps = (int)Math.Truncate(wheel);
        wheel -= steps;
        if (steps != 0)
        {
            View.ZoomAt(ToVector(e.GetPosition(this)), steps);
        }
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Space)
        {
            spaceDown = true;
            e.Handled = true;
            return;
        }
        if (spaceDown || pan is not null)
        {
            return;
        }
        if (KeyOf(e.Key) is { } key && Input is { } input)
        {
            e.Handled = input.KeyPressed(key, ModifiersOf(e.KeyModifiers));
        }
    }

    /// <inheritdoc />
    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (e.Key == Key.Space)
        {
            spaceDown = false;
            e.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        spaceDown = false;
    }

    private static void DrawSprite(DrawingContext context, CanvasView view, CanvasSprite sprite)
    {
        if (sprite.Opacity <= 0 || PictureBitmaps.Get(sprite.Picture) is not { } bitmap)
        {
            return;
        }
        var source = new Rect(sprite.Source.X, sprite.Source.Y, sprite.Source.Width, sprite.Source.Height);
        var destination = ToScreen(view, sprite.Destination);
        if (sprite.Opacity < 1)
        {
            using (context.PushOpacity(sprite.Opacity))
            {
                context.DrawImage(bitmap, source, destination);
            }
        }
        else
        {
            context.DrawImage(bitmap, source, destination);
        }
    }

    private void DrawGrid(DrawingContext context, CanvasView view, Vector2 cell, Size size)
    {
        if (cell.X <= 0 || cell.Y <= 0 || cell.X * view.Zoom < SmallestGridCell || cell.Y * view.Zoom < SmallestGridCell)
        {
            return;
        }
        var brush = new ImmutableSolidColorBrush(GridColor);
        var visible = view.Visible(new Vector2((float)size.Width, (float)size.Height));
        for (var column = MathF.Ceiling(visible.Left / cell.X); column * cell.X <= visible.Right; column++)
        {
            var x = Math.Round(view.ToScreen(new Vector2(column * cell.X, 0)).X);
            context.FillRectangle(brush, new Rect(x, 0, 1, size.Height));
        }
        for (var row = MathF.Ceiling(visible.Top / cell.Y); row * cell.Y <= visible.Bottom; row++)
        {
            var y = Math.Round(view.ToScreen(new Vector2(0, row * cell.Y)).Y);
            context.FillRectangle(brush, new Rect(0, y, size.Width, 1));
        }
    }

    private static void DrawShape(DrawingContext context, CanvasView view, CanvasShape shape)
    {
        var rect = ToScreen(view, shape.Bounds);
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }
        var brush = new ImmutableSolidColorBrush(Color.FromArgb(shape.Color.A, shape.Color.R, shape.Color.G, shape.Color.B));
        switch (shape.Mark)
        {
            case CanvasMark.Fill:
                context.FillRectangle(brush, rect);
                break;
            case CanvasMark.Outline:
                context.FillRectangle(brush, new Rect(rect.Left, rect.Top, rect.Width, 1));
                context.FillRectangle(brush, new Rect(rect.Left, rect.Bottom - 1, rect.Width, 1));
                context.FillRectangle(brush, new Rect(rect.Left, rect.Top + 1, 1, Math.Max(rect.Height - 2, 0)));
                context.FillRectangle(brush, new Rect(rect.Right - 1, rect.Top + 1, 1, Math.Max(rect.Height - 2, 0)));
                break;
            case CanvasMark.Hatch:
            {
                var pen = new ImmutablePen(brush, 1);
                using (context.PushClip(rect))
                {
                    // Lines from the top left to the bottom right, through the pixel centres; one goes through the top left corner
                    for (var start = -Math.Ceiling(rect.Height / HatchSpacing) * HatchSpacing; start < rect.Width; start += HatchSpacing)
                    {
                        var from = new Point(rect.Left + start + 0.5, rect.Top + 0.5);
                        context.DrawLine(pen, from, from + new Vector(rect.Height, rect.Height));
                    }
                }
                break;
            }
            case CanvasMark.Cross:
            {
                var pen = new ImmutablePen(brush, 1);
                using (context.PushClip(rect))
                {
                    context.DrawLine(pen, new Point(rect.Left + 0.5, rect.Top + 0.5), new Point(rect.Right - 0.5, rect.Bottom - 0.5));
                    context.DrawLine(pen, new Point(rect.Right - 0.5, rect.Top + 0.5), new Point(rect.Left + 0.5, rect.Bottom - 0.5));
                }
                break;
            }
        }
    }

    // A canvas rectangle on whole screen pixels, so that the edges of two tiles side by side meet without a gap
    private static Rect ToScreen(CanvasView view, RectangleF bounds)
    {
        var topLeft = view.ToScreen(new Vector2(bounds.Left, bounds.Top));
        var bottomRight = view.ToScreen(new Vector2(bounds.Right, bounds.Bottom));
        var (left, top) = (Math.Round(topLeft.X), Math.Round(topLeft.Y));
        return new Rect(left, top, Math.Round(bottomRight.X) - left, Math.Round(bottomRight.Y) - top);
    }

    private static Vector2 ToVector(Point point) => new((float)point.X, (float)point.Y);

    private static CanvasButton? ButtonOf(PointerUpdateKind kind) => kind switch
    {
        PointerUpdateKind.LeftButtonPressed => CanvasButton.Left,
        PointerUpdateKind.RightButtonPressed => CanvasButton.Right,
        _ => null,
    };

    private static CanvasModifiers ModifiersOf(KeyModifiers modifiers)
        => (modifiers.HasFlag(KeyModifiers.Shift) ? CanvasModifiers.Shift : CanvasModifiers.None)
            | (modifiers.HasFlag(KeyModifiers.Control) ? CanvasModifiers.Control : CanvasModifiers.None)
            | (modifiers.HasFlag(KeyModifiers.Alt) ? CanvasModifiers.Alt : CanvasModifiers.None);

    private static CanvasKey? KeyOf(Key key) => key switch
    {
        Key.B => CanvasKey.B,
        Key.P => CanvasKey.P,
        Key.F => CanvasKey.F,
        Key.E => CanvasKey.E,
        Key.I => CanvasKey.I,
        Key.R => CanvasKey.R,
        Key.X => CanvasKey.X,
        Key.Y => CanvasKey.Y,
        Key.Z => CanvasKey.Z,
        Key.Escape => CanvasKey.Escape,
        Key.Delete => CanvasKey.Delete,
        Key.Enter => CanvasKey.Enter,
        _ => null,
    };

    private void SceneChanged(object? sender, EventArgs e)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            TryFit();
            InvalidateVisual();
        }
        else
        {
            Dispatcher.UIThread.Post(() =>
            {
                TryFit();
                InvalidateVisual();
            });
        }
    }

    private void ViewChanged(object? sender, EventArgs e) => Dispatcher.UIThread.Post(InvalidateVisual);

    private void TryFit()
    {
        if (!(FitOnFirstLayout && !fitted || fitAsked || centreAsked) || Scene?.Bounds is not { } bounds || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }
        var size = new Vector2((float)Bounds.Width, (float)Bounds.Height);
        if (centreAsked && !fitAsked && !(FitOnFirstLayout && !fitted))
        {
            centreAsked = false;
            View.Centre(bounds, size);
            return;
        }
        fitted = true;
        fitAsked = false;
        centreAsked = false;
        View.Fit(bounds, size);
    }
}
