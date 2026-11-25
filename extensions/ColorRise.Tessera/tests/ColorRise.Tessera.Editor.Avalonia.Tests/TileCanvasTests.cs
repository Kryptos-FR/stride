using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using ColorRise.Tessera.Editor.Avalonia.Controls;
using ColorRise.Tessera.Editor.Canvas;
using ColorRise.Tessera.Editor.Pictures;
using Rectangle = Stride.Core.Mathematics.Rectangle;
using RectangleF = Stride.Core.Mathematics.RectangleF;
using StrideColor = Stride.Core.Mathematics.Color;
using Vector2 = Stride.Core.Mathematics.Vector2;

namespace ColorRise.Tessera.Editor.Avalonia.Tests;

/// <summary>
/// The canvas of the tile editors: what it draws, at which pixels, and what it does with the pointer and the keys.
/// </summary>
public sealed class TileCanvasTests
{
    [AvaloniaFact]
    public void Sprites_are_drawn_with_hard_pixels_at_the_zoom()
    {
        var scene = new TestScene();
        scene.Sprites.Add(new CanvasSprite(Quarters(), new Rectangle(0, 0, 2, 2), new RectangleF(0, 0, 2, 2)));
        var (canvas, window) = Show(scene, zoom: 8);

        Assert.Equal(StrideColor.Red, CanvasProbe.Pixel(window, 3, 3));
        Assert.Equal(StrideColor.Lime, CanvasProbe.Pixel(window, 12, 3));
        Assert.Equal(StrideColor.Blue, CanvasProbe.Pixel(window, 3, 12));
        Assert.Equal(StrideColor.Yellow, CanvasProbe.Pixel(window, 12, 12));
        // No blend at the seam
        Assert.Equal(StrideColor.Red, CanvasProbe.Pixel(window, 7, 7));
        Assert.Equal(StrideColor.Yellow, CanvasProbe.Pixel(window, 8, 8));
        Assert.Equal(Background(canvas), CanvasProbe.Pixel(window, 40, 40));
    }

    [AvaloniaFact]
    public void The_bitmap_of_a_picture_that_is_gone_is_disposed()
    {
        // The bitmaps of the pictures of earlier tests that are gone are disposed first, so that only this one can change
        // the count; no window holds the picture, so that nothing but the picture's own life decides
        CollectAndSweep();
        var before = PictureBitmaps.LiveBitmaps;

        var picture = MakeTheBitmapOfAPictureThatIsDropped(before);
        CollectAndSweep();

        Assert.False(picture.TryGetTarget(out _), "The picture is still alive.");
        Assert.Equal(before, PictureBitmaps.LiveBitmaps);
    }

    [AvaloniaFact]
    public void A_part_of_a_picture_and_an_opacity_are_drawn()
    {
        var scene = new TestScene();
        scene.Sprites.Add(new CanvasSprite(Quarters(), new Rectangle(1, 0, 1, 1), new RectangleF(0, 0, 2, 2), 0.5f));
        var (canvas, window) = Show(scene, zoom: 8);

        var background = Background(canvas);
        var pixel = CanvasProbe.Pixel(window, 8, 8);
        Assert.InRange(pixel.R, background.R / 2 - 2, background.R / 2 + 2);
        Assert.InRange(pixel.G, (255 + background.G) / 2 - 2, (255 + background.G) / 2 + 2);
        Assert.InRange(pixel.B, background.B / 2 - 2, background.B / 2 + 2);
    }

    [AvaloniaFact]
    public void The_grid_is_drawn_when_a_cell_is_at_least_four_pixels()
    {
        var scene = new TestScene { GridSize = new Vector2(2, 2) };
        var (canvas, window) = Show(scene, zoom: 8);
        canvas.ShowGrid = true;
        canvas.GridColor = Colors.White;

        Assert.Equal(StrideColor.White, CanvasProbe.Pixel(window, 16, 5));
        Assert.NotEqual(StrideColor.White, CanvasProbe.Pixel(window, 5, 5));

        canvas.View.Zoom = 1;
        for (var x = 0; x < 64; x++)
        {
            Assert.NotEqual(StrideColor.White, CanvasProbe.Pixel(window, x, 5));
        }
    }

    [AvaloniaFact]
    public void Shapes_are_drawn_over_the_sprites()
    {
        var scene = new TestScene();
        var under = new TilePicture(1, 1);
        under[0, 0] = StrideColor.Gray;
        scene.Sprites.Add(new CanvasSprite(under, new Rectangle(0, 0, 1, 1), new RectangleF(0, 0, 4, 4)));
        scene.Shapes.Add(new CanvasShape(CanvasMark.Fill, new RectangleF(0, 0, 2, 2), StrideColor.Red));
        scene.Shapes.Add(new CanvasShape(CanvasMark.Outline, new RectangleF(2, 0, 2, 2), StrideColor.Lime));
        scene.Shapes.Add(new CanvasShape(CanvasMark.Hatch, new RectangleF(0, 2, 2, 2), StrideColor.Blue));
        scene.Shapes.Add(new CanvasShape(CanvasMark.Cross, new RectangleF(2, 2, 2, 2), StrideColor.Yellow));
        var (_, window) = Show(scene, zoom: 8);

        Assert.Equal(StrideColor.Red, CanvasProbe.Pixel(window, 8, 8));
        Assert.Equal(StrideColor.Lime, CanvasProbe.Pixel(window, 16, 8));
        Assert.Equal(StrideColor.Gray, CanvasProbe.Pixel(window, 24, 8));
        Assert.Equal(StrideColor.Blue, CanvasProbe.Pixel(window, 2, 18));
        Assert.Equal(StrideColor.Yellow, CanvasProbe.Pixel(window, 19, 19));
    }

    [AvaloniaFact]
    public void A_picture_that_changed_is_drawn_again()
    {
        var scene = new TestScene();
        var picture = Quarters();
        scene.Sprites.Add(new CanvasSprite(picture, new Rectangle(0, 0, 2, 2), new RectangleF(0, 0, 2, 2)));
        var (_, window) = Show(scene, zoom: 8);
        Assert.Equal(StrideColor.Red, CanvasProbe.Pixel(window, 3, 3));

        picture[0, 0] = StrideColor.White;
        picture.Touch();
        scene.RaiseChanged();

        Assert.Equal(StrideColor.White, CanvasProbe.Pixel(window, 3, 3));
    }

    [AvaloniaFact]
    public void The_wheel_zooms_around_the_cursor_and_the_input_hears_nothing()
    {
        var (canvas, window) = Show(new TestScene(), zoom: 1);
        var input = new RecordingInput();
        canvas.Input = input;
        var before = canvas.View.ToCanvas(new Vector2(32, 32));

        window.MouseWheel(new Point(32, 32), new Vector(0, 1));

        Assert.Equal(2, canvas.View.Zoom);
        Assert.Equal(before, canvas.View.ToCanvas(new Vector2(32, 32)));
        Assert.Empty(input.Events);
    }

    [AvaloniaFact]
    public void A_press_whose_pointer_is_taken_away_tells_the_input()
    {
        var (canvas, window) = Show(new TestScene(), zoom: 1);
        var input = new RecordingInput();
        canvas.Input = input;
        IPointer? pointer = null;
        canvas.AddHandler(InputElement.PointerPressedEvent, (_, e) => pointer = e.Pointer, RoutingStrategies.Bubble, handledEventsToo: true);

        window.MouseDown(new Point(10, 10), MouseButton.Left);
        pointer!.Capture(null);
        Assert.Equal(["press", "lost"], Kinds(input));

        // A press that ends with its own release loses nothing
        window.MouseUp(new Point(10, 10), MouseButton.Left);
        input.Events.Clear();
        window.MouseDown(new Point(10, 10), MouseButton.Left);
        window.MouseUp(new Point(10, 10), MouseButton.Left);
        Assert.Equal(["press", "release"], Kinds(input));
    }

    [AvaloniaFact]
    public void Small_wheel_deltas_add_up_to_one_step()
    {
        // As a touchpad gives them
        var (canvas, window) = Show(new TestScene(), zoom: 1);

        window.MouseWheel(new Point(32, 32), new Vector(0, 0.25));
        Assert.Equal(1, canvas.View.Zoom);
        for (var i = 0; i < 3; i++)
        {
            window.MouseWheel(new Point(32, 32), new Vector(0, 0.25));
        }
        Assert.Equal(2, canvas.View.Zoom);
    }

    [AvaloniaFact]
    public void The_middle_button_pans()
    {
        var (canvas, window) = Show(new TestScene(), zoom: 2);
        var input = new RecordingInput();
        canvas.Input = input;

        window.MouseDown(new Point(10, 10), MouseButton.Middle);
        window.MouseMove(new Point(30, 25));
        window.MouseUp(new Point(30, 25), MouseButton.Middle);

        Assert.Equal(new Vector2(-10, -7.5f), canvas.View.Origin);
        Assert.Empty(input.Events);
    }

    [AvaloniaFact]
    public void Space_and_the_left_button_pan()
    {
        var (canvas, window) = Show(new TestScene(), zoom: 2);
        var input = new RecordingInput();
        canvas.Input = input;
        canvas.Focus();

        window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
        window.MouseDown(new Point(10, 10), MouseButton.Left);
        window.MouseMove(new Point(30, 25));
        window.MouseUp(new Point(30, 25), MouseButton.Left);
        window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");

        Assert.Equal(new Vector2(-10, -7.5f), canvas.View.Origin);
        Assert.Empty(input.Events);
    }

    [AvaloniaFact]
    public void The_pointer_reaches_the_input_in_canvas_units()
    {
        var input = new RecordingInput();
        var canvas = new TileCanvas { Width = 100, Height = 100, Scene = new TestScene(), Input = input };
        canvas.View.Zoom = 4;
        canvas.View.Origin = new Vector2(10, 20);
        var window = CanvasProbe.Show(Beside(canvas, new Border { Width = 40 }), 140, 100);
        window.MouseMove(new Point(40, 80));
        input.Events.Clear();

        window.MouseDown(new Point(40, 80), MouseButton.Left);
        window.MouseMove(new Point(44, 80));
        window.MouseUp(new Point(44, 80), MouseButton.Left);
        window.MouseDown(new Point(44, 80), MouseButton.Right);
        window.MouseUp(new Point(44, 80), MouseButton.Right);
        window.MouseMove(new Point(120, 50));

        Assert.Equal(
            ["press 20,40 Left None", "move 21,40 None", "release 21,40 Left None", "press 21,40 Right None", "release 21,40 Right None", "left"],
            input.Events);
    }

    [AvaloniaFact]
    public void A_key_reaches_the_input_only_while_the_canvas_has_the_focus()
    {
        var input = new RecordingInput();
        var canvas = new TileCanvas { Width = 64, Height = 64, Scene = new TestScene(), Input = input };
        var box = new TextBox { Width = 60 };
        var window = CanvasProbe.Show(Beside(canvas, box), 140, 64);
        window.MouseDown(new Point(10, 10), MouseButton.Left);
        window.MouseUp(new Point(10, 10), MouseButton.Left);
        input.Events.Clear();

        window.KeyPress(Key.B, RawInputModifiers.None, PhysicalKey.B, "b");
        window.KeyRelease(Key.B, RawInputModifiers.None, PhysicalKey.B, "b");
        window.KeyPress(Key.Z, RawInputModifiers.Shift, PhysicalKey.Z, "Z");
        window.KeyRelease(Key.Z, RawInputModifiers.Shift, PhysicalKey.Z, "Z");
        Assert.Equal(["key B None", "key Z Shift"], input.Events);

        box.Focus();
        window.KeyPress(Key.B, RawInputModifiers.None, PhysicalKey.B, "b");
        window.KeyTextInput("b");
        window.KeyRelease(Key.B, RawInputModifiers.None, PhysicalKey.B, "b");
        Assert.Equal(["key B None", "key Z Shift"], input.Events);
        Assert.Equal("b", box.Text);
    }

    [AvaloniaFact]
    public void A_key_the_input_did_not_use_is_not_marked_handled()
    {
        var (canvas, window) = Show(new TestScene(), zoom: 1);
        var input = new RecordingInput { Handles = false };
        canvas.Input = input;
        canvas.Focus();
        var handled = new List<bool>();
        window.AddHandler(InputElement.KeyDownEvent, (_, e) => handled.Add(e.Handled), RoutingStrategies.Bubble, handledEventsToo: true);

        window.KeyPress(Key.I, RawInputModifiers.None, PhysicalKey.I, "i");
        window.KeyRelease(Key.I, RawInputModifiers.None, PhysicalKey.I, "i");
        input.Handles = true;
        window.KeyPress(Key.I, RawInputModifiers.None, PhysicalKey.I, "i");
        window.KeyRelease(Key.I, RawInputModifiers.None, PhysicalKey.I, "i");

        Assert.Equal([false, true], handled);
        Assert.Equal(["key I None", "key I None"], input.Events);
    }

    [AvaloniaFact]
    public void Fit_shows_the_whole_scene_now_or_once_it_has_bounds()
    {
        var scene = new TestScene { Bounds = new RectangleF(0, 0, 16, 8) };
        var (canvas, window) = Show(scene, zoom: 1);

        canvas.Fit();
        // 6 would need 96 pixels
        Assert.Equal(4, canvas.View.Zoom);

        scene.Bounds = null;
        canvas.Fit();
        Assert.Equal(4, canvas.View.Zoom);
        scene.Bounds = new RectangleF(0, 0, 32, 32);
        scene.RaiseChanged();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(2, canvas.View.Zoom);
        window.Close();
    }

    [AvaloniaFact]
    public void Centre_keeps_the_zoom_and_puts_the_scene_in_the_middle_now_or_once_it_has_bounds()
    {
        var scene = new TestScene();
        var (canvas, window) = Show(scene, zoom: 2);

        canvas.Centre();
        Assert.Equal(Vector2.Zero, canvas.View.Origin);
        scene.Bounds = new RectangleF(100, 40, 16, 8);
        scene.RaiseChanged();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(2, canvas.View.Zoom);
        // The middle (108, 44) is at the middle of the 64 pixels, 16 canvas units from the corner
        Assert.Equal(new Vector2(92, 28), canvas.View.Origin);
        window.Close();
    }

    [AvaloniaFact]
    public void A_replacing_sprite_hides_what_is_under_it()
    {
        foreach (var replace in new[] { true, false })
        {
            var scene = new TestScene();
            var red = new TilePicture(1, 1);
            red[0, 0] = StrideColor.Red;
            // Left half transparent, right half blue
            var half = new TilePicture(2, 1);
            (half[0, 0], half[1, 0]) = (StrideColor.Transparent, StrideColor.Blue);
            scene.Sprites.Add(new CanvasSprite(red, new Rectangle(0, 0, 1, 1), new RectangleF(0, 0, 4, 4)));
            scene.Sprites.Add(new CanvasSprite(half, new Rectangle(0, 0, 2, 1), new RectangleF(0, 0, 4, 4), Replace: replace));
            var (canvas, window) = Show(scene, zoom: 8);

            Assert.Equal(replace ? Background(canvas) : StrideColor.Red, CanvasProbe.Pixel(window, 8, 16));
            Assert.Equal(StrideColor.Blue, CanvasProbe.Pixel(window, 24, 16));
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_corner_mark_is_a_triangle_in_the_top_left()
    {
        var scene = new TestScene();
        scene.Shapes.Add(new CanvasShape(CanvasMark.Corner, new RectangleF(0, 0, 4, 4), StrideColor.Red));
        var (canvas, window) = Show(scene, zoom: 8);

        // Its sides are a third of the 32 pixels of the bounds
        Assert.Equal(StrideColor.Red, CanvasProbe.Pixel(window, 1, 1));
        Assert.Equal(StrideColor.Red, CanvasProbe.Pixel(window, 8, 1));
        Assert.Equal(StrideColor.Red, CanvasProbe.Pixel(window, 1, 8));
        Assert.Equal(Background(canvas), CanvasProbe.Pixel(window, 8, 8));
        Assert.Equal(Background(canvas), CanvasProbe.Pixel(window, 16, 16));
    }

    [AvaloniaFact]
    public void A_label_is_written_in_the_corner_of_its_bounds()
    {
        var scene = new TestScene();
        scene.Labels.Add(new CanvasLabel("2, 4", new RectangleF(0, 0, 16, 16), StrideColor.White));
        var (canvas, window) = Show(scene, zoom: 8);

        var background = Background(canvas);
        var corner = CanvasProbe.Pixel(window, 0, 0);
        Assert.True(corner.R < background.R && corner.G < background.G && corner.B < background.B, $"The corner is {corner}, not darker than the background.");
        Assert.True(TextPixels(window) > 0, "No pixel of the text.");

        // 8 pixels high: too small for a text
        canvas.View.Zoom = 0.5f;
        Assert.Equal(background, CanvasProbe.Pixel(window, 0, 0));
        Assert.Equal(0, TextPixels(window));
    }

    [AvaloniaFact]
    public void A_tick_gives_the_scene_the_time_and_at_most_a_quarter_of_a_second()
    {
        var scene = new TestClock();
        var (canvas, _) = Show(scene, zoom: 1);

        canvas.Tick(TimeSpan.FromSeconds(0.1));
        canvas.Tick(TimeSpan.FromSeconds(3));

        Assert.Equal([0.1, 0.25], scene.Advances);
    }

    [AvaloniaFact]
    public void The_timer_runs_only_for_a_clock_that_runs()
    {
        var (canvas, window) = Show(new TestScene(), zoom: 1);
        Assert.False(canvas.IsTicking);

        var clock = new TestClock();
        canvas.Scene = clock;
        Assert.False(canvas.IsTicking);
        clock.IsRunning = true;
        Assert.True(canvas.IsTicking);
        clock.IsRunning = false;
        Assert.False(canvas.IsTicking);

        clock.IsRunning = true;
        window.Content = null;
        Assert.False(canvas.IsTicking);
        window.Content = canvas;
        Assert.True(canvas.IsTicking);
        canvas.Scene = new TestScene();
        Assert.False(canvas.IsTicking);
        window.Close();
    }

    [AvaloniaFact]
    public async Task The_canvas_ticks_by_itself()
    {
        var scene = new TestClock { IsRunning = true };
        var (_, window) = Show(scene, zoom: 1);

        await CanvasProbe.WaitUntilAsync(() => scene.Advances.Count > 0, "a tick of the canvas");

        Assert.InRange(scene.Advances[0], 0, 0.25);
        window.Close();
    }

    // A canvas of 64 by 64 pixels filling its window, at a zoom, without the grid
    private static (TileCanvas Canvas, Window Window) Show(ICanvasScene scene, float zoom)
    {
        var canvas = new TileCanvas { Scene = scene, ShowGrid = false };
        canvas.View.Zoom = zoom;
        return (canvas, CanvasProbe.Show(canvas, 64, 64));
    }

    // What the input heard, without the moves
    // A picture drawn once, then dropped with its window: nothing of this frame keeps it
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<TilePicture> MakeTheBitmapOfAPictureThatIsDropped(int before)
    {
        var picture = Quarters();
        Assert.NotNull(PictureBitmaps.Get(picture));
        Assert.Equal(before + 1, PictureBitmaps.LiveBitmaps);
        return new WeakReference<TilePicture>(picture);
    }

    private static void CollectAndSweep()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        PictureBitmaps.Sweep();
    }

    // The pixels of the top-left 32 by 16 pixels that are almost white, as the text of a label is
    private static int TextPixels(Window window)
    {
        var count = 0;
        for (var y = 0; y < 16; y++)
        {
            for (var x = 0; x < 32; x++)
            {
                var pixel = CanvasProbe.Pixel(window, x, y);
                if (pixel.R > 160 && pixel.G > 160 && pixel.B > 160)
                {
                    count++;
                }
            }
        }
        return count;
    }

    private static List<string> Kinds(RecordingInput input) => [.. input.Events.Select(e => e.Split(' ')[0]).Where(kind => kind != "move")];

    private static StackPanel Beside(Control first, Control second)
        => new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Left, Children = { first, second } };

    // Two by two pixels: red, green on top, blue, yellow below
    private static TilePicture Quarters()
    {
        var picture = new TilePicture(2, 2);
        (picture[0, 0], picture[1, 0], picture[0, 1], picture[1, 1]) = (StrideColor.Red, StrideColor.Lime, StrideColor.Blue, StrideColor.Yellow);
        return picture;
    }

    private static StrideColor Background(TileCanvas canvas)
    {
        var color = Assert.IsAssignableFrom<ISolidColorBrush>(canvas.Background).Color;
        return new StrideColor(color.R, color.G, color.B, color.A);
    }

    /// <summary>
    /// A scene that gives back the sprites and shapes the test put in it.
    /// </summary>
    private sealed class TestScene : ICanvasScene
    {
        public List<CanvasSprite> Sprites { get; } = [];

        public List<CanvasShape> Shapes { get; } = [];

        public List<CanvasLabel> Labels { get; } = [];

        public RectangleF? Bounds { get; set; }

        public Vector2 GridSize { get; set; }

        public event EventHandler? Changed;

        public void Collect(RectangleF visible, float zoom, CanvasFrame frame)
        {
            frame.Sprites.AddRange(Sprites);
            frame.Shapes.AddRange(Shapes);
            frame.Labels.AddRange(Labels);
        }

        public void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// A scene that time changes, which writes down the time each tick gives it.
    /// </summary>
    private sealed class TestClock : ICanvasScene, ICanvasClock
    {
        public List<double> Advances { get; } = [];

        public RectangleF? Bounds => null;

        public Vector2 GridSize => Vector2.Zero;

        public bool IsRunning
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    IsRunningChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public event EventHandler? Changed;

        public event EventHandler? IsRunningChanged;

        public void Collect(RectangleF visible, float zoom, CanvasFrame frame)
        {
        }

        public void Advance(double seconds)
        {
            Advances.Add(seconds);
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
