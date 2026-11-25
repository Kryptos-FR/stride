using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ColorRise.Tessera.Assets;
using ColorRise.Tessera.Editor.Avalonia.Controls;
using ColorRise.Tessera.Editor.Avalonia.Views;
using ColorRise.Tessera.Editor.FlipbookEditor;
using ColorRise.Tessera.Editor.Tests;
using Stride.Assets.Editor.Avalonia;
using Stride.Assets.Editor.Testing;
using Stride.Core.Presentation.Avalonia.Commands;
using Stride.Core.Presentation.Avalonia.Services;
using StrideColor = Stride.Core.Mathematics.Color;
using Vector2 = Stride.Core.Mathematics.Vector2;

namespace ColorRise.Tessera.Editor.Avalonia.Tests;

/// <summary>
/// The view of the flipbook editor, on the editor of TestTown's flipbook, in a window of 1000 by 700, as a person uses it.
/// </summary>
public sealed class FlipbookEditorViewTests
{
    private const string Location = "Characters/Hero";
    private const string TownLocation = "Tiles/Town";

    [AvaloniaFact]
    public async Task The_editor_opens_from_the_session_with_its_view()
    {
        await using var session = await OpenAsync();
        var flipbook = session.FlipbookAt(Location);

        session.Session.EditorCollection.OpenAssetEditor(flipbook);

        var editor = Assert.IsType<FlipbookEditorViewModel>(Assert.Single(session.Session.EditorCollection.Editors));
        // The editor area of Game Studio makes the view of an editor with this template
        var area = new ContentControl { Content = editor, ContentTemplate = new EditorViewSelector { Session = session.Session } };
        var window = CanvasProbe.Show(area, 1000, 700);
        try
        {
            var view = Assert.Single(area.GetVisualDescendants().OfType<FlipbookEditorView>());
            Assert.Same(editor, view.DataContext);
        }
        finally
        {
            window.Close();
            await session.Session.EditorCollection.CloseAssetEditorAsync(flipbook, false);
        }
    }

    [AvaloniaFact]
    public async Task The_tree_names_the_animations_and_their_clips()
    {
        await using var session = await OpenAsync();
        var editor = await session.EditFlipbookAsync(Location);
        var (view, window) = Show(editor);
        try
        {
            Assert.Equal(["walk", "Down", "Left", "idle", "Any direction"], TreeTexts(view).Select(block => block.Text));
            Assert.Same(editor.Animations[0].Clips[0], view.FindControl<TreeView>("AnimationTree")!.SelectedItem);

            Click(window, TreeTexts(view).Single(block => block.Text == "idle"));

            Assert.Same(editor.Animations[1], editor.SelectedItem);
            Assert.Equal(["1"], StripLabels(view));
            var addClip = view.FindControl<Button>("AddClipButton")!;
            Assert.True(addClip.IsEnabled);
            editor.SelectedItem = null;
            Assert.False(addClip.IsEnabled);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task The_preview_draws_the_frame_and_plays_when_the_canvas_ticks()
    {
        // Frames of 2 s: the canvas's own ticks, on the clock of the test, do not reach the second frame
        await using var session = await OpenAsync(asset => asset.Animations[0].Clips[0].Frames.ForEach(frame => frame.Duration = 2));
        var editor = await session.EditFlipbookAsync(Location);
        var (view, window) = Show(editor);
        try
        {
            var canvas = view.FindControl<TileCanvas>("Canvas")!;
            // The frame stands on the pivot: 16 pixels wide around it, 16 high above it; this is the middle of its top-left quarter
            var quarter = InPreview(view, window, new Vector2(-4, -12));
            Assert.Equal(StrideColor.Red, CanvasProbe.Pixel(window, (int)quarter.X, (int)quarter.Y));
            Assert.True(canvas.IsTicking);

            for (var tick = 0; tick < 9; tick++)
            {
                canvas.Tick(TimeSpan.FromSeconds(0.25));
            }
            Assert.Equal(TestTown.Flat(1, 0), CanvasProbe.Pixel(window, (int)quarter.X, (int)quarter.Y));
            Assert.True(editor.Frames[1].IsCurrent);

            Click(window, view.FindControl<ToggleButton>("PlayButton")!);

            Assert.False(editor.IsPlaying);
            Assert.False(canvas.IsTicking);
            Assert.True(canvas.IsFocused);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task The_strip_has_a_picture_for_each_frame_and_a_click_selects_one()
    {
        await using var session = await OpenAsync();
        var editor = await session.EditFlipbookAsync(Location);
        var (view, window) = Show(editor);
        try
        {
            Assert.Equal(["1", "2", "3"], StripLabels(view));
            var picture = StripItem(view, 0).GetVisualDescendants().OfType<PictureView>().Single();
            var quarter = picture.TranslatePoint(new Point(picture.Bounds.Width / 4, picture.Bounds.Height / 4), window)!.Value;
            Assert.Equal(StrideColor.Red, CanvasProbe.Pixel(window, (int)quarter.X, (int)quarter.Y));
            Assert.Equal(64, picture.Bounds.Width);

            Click(window, StripItem(view, 1));

            Assert.Equal([editor.Frames[1]], editor.SelectedFrames);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task A_click_in_the_palette_adds_a_frame_to_the_strip()
    {
        await using var session = await OpenAsync();
        var editor = await session.EditFlipbookAsync(Location);
        var (view, window) = Show(editor);
        try
        {
            Click(window, InPalette(view, window, 3, 0));

            Assert.Equal(["1", "2", "3", "4"], StripLabels(view));
            Assert.Equal([editor.Frames[3]], view.FindControl<ListBox>("FrameStrip")!.SelectedItems!.Cast<object>());
            Assert.Equal(["Add frames"], session.Steps);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task The_buttons_of_the_strip_move_and_remove_the_selected_frames()
    {
        await using var session = await OpenAsync();
        var editor = await session.EditFlipbookAsync(Location);
        var (view, window) = Show(editor);
        try
        {
            Click(window, StripItem(view, 0));
            Click(window, view.FindControl<Button>("MoveFramesRightButton")!);

            Assert.Equal(["1:1,0", "1:0,0", "1:2,0"], Tiles(editor));
            Assert.Equal([editor.Frames[1]], view.FindControl<ListBox>("FrameStrip")!.SelectedItems!.Cast<object>());
            Assert.True(view.FindControl<TileCanvas>("Canvas")!.IsFocused);

            Click(window, view.FindControl<Button>("RemoveFramesButton")!);
            Assert.Equal(["1:1,0", "1:2,0"], Tiles(editor));

            // Delete in the strip
            Click(window, StripItem(view, 0));
            PressKey(window, Key.Delete, PhysicalKey.Delete, "");
            Assert.Equal(["1:2,0"], Tiles(editor));
            Assert.Equal(["Move frames", "Remove frames", "Remove frames"], session.Steps);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task A_drag_along_the_strip_moves_a_frame()
    {
        await using var session = await OpenAsync();
        var editor = await session.EditFlipbookAsync(Location);
        var (view, window) = Show(editor);
        try
        {
            var first = Middle(window, StripItem(view, 0));
            var last = Middle(window, StripItem(view, 2));
            window.MouseMove(first);
            window.MouseDown(first, MouseButton.Left);
            window.MouseMove(last);
            window.MouseUp(last, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(["1:1,0", "1:2,0", "1:0,0"], Tiles(editor));
            Assert.Equal(["Move frames"], session.Steps);
            Assert.Equal([editor.Frames[2]], editor.SelectedFrames);

            // Two selected frames move together, and stay selected
            editor.SelectedFrames = [editor.Frames[0], editor.Frames[1]];
            Drag(window, Middle(window, StripItem(view, 0)), Middle(window, StripItem(view, 2)));
            Assert.Equal(["1:0,0", "1:1,0", "1:2,0"], Tiles(editor));
            Assert.Equal([editor.Frames[1], editor.Frames[2]], editor.SelectedFrames);
            Assert.Equal([1, 2], view.FindControl<ListBox>("FrameStrip")!.Selection.SelectedIndexes.Order());

            // A click on one of them, without a drag, selects it alone
            Click(window, StripItem(view, 2));
            Assert.Equal([editor.Frames[2]], editor.SelectedFrames);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task A_name_typed_in_the_tree_renames_and_a_taken_name_shows_the_message()
    {
        await using var session = await OpenAsync();
        var editor = await session.EditFlipbookAsync(Location);
        var (view, window) = Show(editor);
        try
        {
            DoubleClick(window, TreeTexts(view).Single(block => block.Text == "walk"));
            Assert.True(editor.Animations[0].IsEditing);
            Type(window, "run");
            PressKey(window, Key.Enter, PhysicalKey.Enter, "\r");

            Assert.Equal(["run", "idle"], session.FlipbookAt(Location).Asset.Animations.Select(animation => animation.Name));
            Assert.Equal(["Rename animation"], session.Steps);

            DoubleClick(window, TreeTexts(view).Single(block => block.Text == "run"));
            Type(window, "idle");
            PressKey(window, Key.Enter, PhysicalKey.Enter, "\r");

            Assert.Equal("run", editor.Animations[0].Name);
            Assert.Contains("Another animation is named 'idle'.", Texts(view));

            // F2 types the name of the selected animation, and Escape leaves it
            Click(window, TreeTexts(view).Single(block => block.Text == "idle"));
            PressKey(window, Key.F2, PhysicalKey.F2, "");
            Assert.True(editor.Animations[1].IsEditing);
            Type(window, "rest");
            PressKey(window, Key.Escape, PhysicalKey.Escape, "");
            Assert.False(editor.Animations[1].IsEditing);
            Assert.Equal("idle", editor.Animations[1].Name);
            Assert.Single(session.Steps);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task The_sheet_button_opens_its_bar_and_Make_fills_the_tree()
    {
        await using var session = await OpenAsync();
        var editor = await session.EditFlipbookAsync(Location);
        var (view, window) = Show(editor);
        try
        {
            Click(window, view.FindControl<Button>("SheetButton")!);
            Assert.Contains("Drag over the frames in the palette.", Texts(view));

            Drag(window, InPalette(view, window, 0, 0), InPalette(view, window, 2, 1));

            Assert.Contains("3 by 2", Texts(view));
            Assert.Equal(["Down", "Left"], Visible<ComboBox>(view, "RowDirection").Select(box => ((SheetDirectionChoice)box.SelectedItem!).Label));
            Visible<TextBox>(view, "SheetNameBox").Single().Text = "run";
            Click(window, Visible<Button>(view, "MakeButton").Single());

            Assert.Equal(["Sheet"], session.Steps);
            Assert.Null(editor.PendingSheet);
            Assert.Empty(Visible<Button>(view, "MakeButton"));
            Assert.Equal(["walk", "Down", "Left", "idle", "Any direction", "run", "Down", "Left"], TreeTexts(view).Select(block => block.Text));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task A_flipbook_without_a_tile_set_shows_the_pick_button()
    {
        await using var session = await OpenAsync(withTileSet: false);
        var editor = await session.EditFlipbookAsync(Location);
        var (view, window) = Show(editor);
        try
        {
            var pick = view.FindControl<Button>("PickTileSetButton")!;
            Assert.True(pick.IsEffectivelyVisible);
            Assert.DoesNotContain(view.GetVisualDescendants().OfType<TilePalette>(), palette => palette.IsEffectivelyVisible);

            session.Dialogs.PickAsset = _ => session.TileSetAt(TownLocation);
            Click(window, pick);

            Assert.False(pick.IsEffectivelyVisible);
            Assert.Single(view.GetVisualDescendants().OfType<TilePalette>(), palette => palette.IsEffectivelyVisible);
            Assert.Contains("Town", Texts(view));
            Assert.True(view.FindControl<Button>("ChangeTileSetButton")!.IsEffectivelyVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task A_problem_shows_at_the_bottom()
    {
        await using var session = await OpenAsync(asset => asset.Animations[0].Clips[1].Frames[1].Cells[0].Tile = TileId.Parse("1:9,9"));
        var editor = await session.EditFlipbookAsync(Location);
        var (view, window) = Show(editor);
        try
        {
            Assert.Contains("The frame 2 of the clip Left of 'walk' uses the tile 1:9,9, which the tile set does not have.", Texts(view));
        }
        finally
        {
            window.Close();
        }
    }

    // The view in a window that routes Delete and F2 to the focused panel, as Game Studio's window does
    private static (FlipbookEditorView View, Window Window) Show(FlipbookEditorViewModel editor)
    {
        var view = new FlipbookEditorView { DataContext = editor };
        var window = CanvasProbe.Show(view, 1000, 700);
        var router = new EditCommandRouter(window);
        window.AddHandler(InputElement.KeyDownEvent, (_, e) => router.TryHandleKey(e), RoutingStrategies.Bubble);
        window.Closed += (_, _) => router.Dispose();
        Dispatcher.UIThread.RunJobs();
        return (view, window);
    }

    // A point of the preview's canvas, x right and y down from the pivot, in the window
    private static Point InPreview(FlipbookEditorView view, Window window, Vector2 point)
    {
        var canvas = view.FindControl<TileCanvas>("Canvas")!;
        var screen = canvas.View.ToScreen(point);
        return canvas.TranslatePoint(new Point(screen.X, screen.Y), window)!.Value;
    }

    // The middle of the tile (column, row) of the palette's source, in the window
    private static Point InPalette(FlipbookEditorView view, Window window, int column, int row)
    {
        var palette = Assert.Single(view.GetVisualDescendants().OfType<TilePalette>(), control => control.IsEffectivelyVisible);
        var canvas = palette.GetVisualDescendants().OfType<TileCanvas>().Single();
        var screen = canvas.View.ToScreen(new Vector2(column * 16 + 8, row * 16 + 8));
        return canvas.TranslatePoint(new Point(screen.X, screen.Y), window)!.Value;
    }

    private static Control StripItem(FlipbookEditorView view, int index)
    {
        Dispatcher.UIThread.RunJobs();
        return view.FindControl<ListBox>("FrameStrip")!.ContainerFromIndex(index)!;
    }

    private static List<string> StripLabels(FlipbookEditorView view)
    {
        Dispatcher.UIThread.RunJobs();
        return [.. Visible<TextBlock>(view.FindControl<ListBox>("FrameStrip")!, "FrameLabel").Select(block => block.Text ?? "")];
    }

    // The names of the animations and the labels of the clips, in the order of the tree
    private static List<TextBlock> TreeTexts(FlipbookEditorView view)
    {
        Dispatcher.UIThread.RunJobs();
        var tree = view.FindControl<TreeView>("AnimationTree")!;
        return [.. tree.GetVisualDescendants().OfType<TextBlock>().Where(block => block.Name is "AnimationName" or "ClipLabel" && block.IsEffectivelyVisible)];
    }

    private static List<string> Tiles(FlipbookEditorViewModel editor) => [.. editor.Frames.Select(frame => frame.Tile.ToString()!)];

    private static IEnumerable<T> Visible<T>(Control root, string name) where T : Control
    {
        Dispatcher.UIThread.RunJobs();
        return root.GetVisualDescendants().OfType<T>().Where(control => control.Name == name && control.IsEffectivelyVisible).ToList();
    }

    private static List<string> Texts(Control root)
    {
        Dispatcher.UIThread.RunJobs();
        return [.. root.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible).Select(block => block.Text ?? "")];
    }

    private static void Click(Window window, Point point)
    {
        window.MouseMove(point);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Click(Window window, Control control) => Click(window, Middle(window, control));

    private static void Drag(Window window, Point from, Point to)
    {
        window.MouseMove(from);
        window.MouseDown(from, MouseButton.Left);
        window.MouseMove(to);
        window.MouseUp(to, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    // Two clicks at once: the second one has a click count of 2
    private static void DoubleClick(Window window, Control control)
    {
        var middle = Middle(window, control);
        window.MouseDown(middle, MouseButton.Left);
        window.MouseUp(middle, MouseButton.Left);
        window.MouseDown(middle, MouseButton.Left);
        window.MouseUp(middle, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    private static Point Middle(Window window, Control control)
        => control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;

    private static void Type(Window window, string text)
    {
        foreach (var letter in text)
        {
            var key = Enum.Parse<Key>(letter.ToString(), ignoreCase: true);
            var physical = Enum.Parse<PhysicalKey>(letter.ToString(), ignoreCase: true);
            window.KeyPress(key, RawInputModifiers.None, physical, letter.ToString());
            window.KeyTextInput(letter.ToString());
            window.KeyRelease(key, RawInputModifiers.None, physical, letter.ToString());
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static void PressKey(Window window, Key key, PhysicalKey physical, string text)
    {
        window.KeyPress(key, RawInputModifiers.None, physical, text);
        window.KeyRelease(key, RawInputModifiers.None, physical, text);
        Dispatcher.UIThread.RunJobs();
    }

    private static async Task<TesseraSession> OpenAsync(Action<FlipbookAsset>? change = null, bool withTileSet = true)
    {
        var options = new StrideEditorTestSessionOptions { Dispatcher = DispatcherService.Create(), Plugins = [new TesseraEditorAvaloniaPlugin()] };
        var tileSet = TestTown.TileSet();
        var flipbook = TestTown.Flipbook(withTileSet ? tileSet : null, TownLocation);
        change?.Invoke(flipbook);
        var session = await TesseraSession.OpenAsync(options, (TownLocation, tileSet), (Location, flipbook));
        session.WriteImage("Tiles/town.png", 64, 32, TestTown.Pixel);
        return session;
    }
}
