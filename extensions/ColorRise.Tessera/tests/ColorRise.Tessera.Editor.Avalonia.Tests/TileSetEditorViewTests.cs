using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ColorRise.Tessera.Assets;
using ColorRise.Tessera.Editor.Avalonia.Controls;
using ColorRise.Tessera.Editor.Avalonia.Views;
using ColorRise.Tessera.Editor.Canvas;
using ColorRise.Tessera.Editor.Tests;
using ColorRise.Tessera.Editor.TileSetEditor;
using Stride.Assets.Editor.Avalonia;
using Stride.Assets.Editor.Testing;
using Stride.Core.Presentation.Avalonia.Services;
using StrideColor = Stride.Core.Mathematics.Color;
using Vector2 = Stride.Core.Mathematics.Vector2;

namespace ColorRise.Tessera.Editor.Avalonia.Tests;

/// <summary>
/// The view of the tile set editor, in a window, as a person uses it.
/// </summary>
public sealed class TileSetEditorViewTests
{
    [AvaloniaFact]
    public async Task The_editor_opens_from_the_session_with_its_view()
    {
        await using var session = await OpenTownAsync();
        var tileSet = session.TileSetAt("Tiles/Town");

        session.Session.EditorCollection.OpenAssetEditor(tileSet);

        var editor = Assert.IsType<TileSetEditorViewModel>(Assert.Single(session.Session.EditorCollection.Editors));
        // The editor area of Game Studio makes the view of an editor with this template
        var area = new ContentControl { Content = editor, ContentTemplate = new EditorViewSelector { Session = session.Session } };
        var window = CanvasProbe.Show(area, 800, 600);
        try
        {
            var view = Assert.Single(area.GetVisualDescendants().OfType<TileSetEditorView>());
            Assert.Same(editor, view.DataContext);
        }
        finally
        {
            window.Close();
            await session.Session.EditorCollection.CloseAssetEditorAsync(tileSet, false);
        }
    }

    [AvaloniaFact]
    public async Task The_view_draws_the_selected_source()
    {
        await using var session = await OpenTownAsync();
        var editor = await session.EditTileSetAsync("Tiles/Town");
        var (view, window) = Show(editor);
        try
        {
            var middle = At(view, window, 2, 1);
            Assert.Equal(TestTown.Flat(2, 1), CanvasProbe.Pixel(window, (int)middle.X, (int)middle.Y));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task The_list_names_the_sources()
    {
        await using var session = await OpenTownAsync();
        var editor = await session.EditTileSetAsync("Tiles/Town");
        var (view, window) = Show(editor);
        try
        {
            var list = view.FindControl<ListBox>("SourceList")!;
            var texts = Texts(list);
            Assert.Contains("1", texts);
            Assert.Contains("Town", texts);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task A_click_on_the_canvas_selects_a_tile()
    {
        await using var session = await OpenTownAsync();
        var editor = await session.EditTileSetAsync("Tiles/Town");
        var (view, window) = Show(editor);
        try
        {
            var tile = At(view, window, 2, 1);
            window.MouseDown(tile, MouseButton.Left);
            window.MouseUp(tile, MouseButton.Left);

            Assert.Equal([TileId.Parse("1:2,1")], editor.SelectedTiles);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task A_drag_in_solid_mode_hatches_the_tiles()
    {
        await using var session = await OpenTownAsync();
        var editor = await session.EditTileSetAsync("Tiles/Town");
        var (view, window) = Show(editor);
        try
        {
            Click(window, view.FindControl<ToggleButton>("SolidModeButton")!);
            Assert.Equal(TileSetEditorMode.Solid, editor.Mode);

            window.MouseDown(At(view, window, 0, 0), MouseButton.Left);
            window.MouseMove(At(view, window, 1, 0));
            window.MouseUp(At(view, window, 1, 0), MouseButton.Left);

            var tiles = session.TileSetAt("Tiles/Town").Asset.Tiles;
            Assert.Equal([TileId.Parse("1:0,0"), TileId.Parse("1:1,0")], tiles.Select(definition => definition.Tile));
            Assert.All(tiles, definition => Assert.Equal(TileCollision.Solid, definition.Collision));
            Assert.Equal(["Solid"], session.Steps);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task A_proposed_image_shows_its_bar_and_add_makes_the_source()
    {
        await using var session = await OpenTownAsync();
        var editor = await session.EditTileSetAsync("Tiles/Town");
        var (view, window) = Show(editor);
        try
        {
            editor.ProposeImage(session.WriteImage("Tiles/cave.png", 64, 32, (_, _) => StrideColor.Gray));
            Dispatcher.UIThread.RunJobs();
            Assert.Contains("4 x 2 tiles", Texts(view));

            Named<TextBox>(view, "TileWidthBox").Text = "32";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(32, editor.PendingImage!.TileWidth);
            Assert.Contains("2 x 2 tiles", Texts(view));
            Named<TextBox>(view, "TileHeightBox").Text = "32";
            Assert.Contains("2 x 1 tiles", Texts(view));

            Click(window, Named<Button>(view, "AddPendingButton"));

            Assert.Equal(2, editor.Sources.Count);
            Assert.Null(editor.PendingImage);
            Assert.DoesNotContain(view.GetVisualDescendants().OfType<TextBox>(), box => box.Name == "TileWidthBox" && box.IsEffectivelyVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task A_problem_shows_at_the_bottom()
    {
        await using var session = await OpenTownAsync();
        var editor = await session.EditTileSetAsync("Tiles/Town");
        var (view, window) = Show(editor);
        try
        {
            File.Delete(session.FileOf("Tiles/town", ".png"));

            Click(window, view.FindControl<Button>("RefreshButton")!);

            Assert.Contains(Texts(view), text => text.Contains("town.png", StringComparison.Ordinal));
        }
        finally
        {
            window.Close();
        }
    }

    // The editor's view in a window of 800 by 600
    // Each source shows whole at the zoom that fits it, not at the one of the source shown before
    [AvaloniaFact]
    public async Task Another_source_is_fitted_to_its_own_size()
    {
        await using var session = await OpenTownAsync();
        var editor = await session.EditTileSetAsync("Tiles/Town");
        var (view, window) = Show(editor);
        try
        {
            editor.ProposeImage(session.WriteImage("Tiles/cave.png", 16, 16, (_, _) => StrideColor.Gray));
            Dispatcher.UIThread.RunJobs();
            Click(window, Named<Button>(view, "AddPendingButton"));
            await CanvasProbe.WaitUntilAsync(() => editor.Sources.All(source => source.Layout is { Size.X: > 0 }), "the pictures of the two sources");
            var canvas = view.FindControl<TileCanvas>("Canvas")!;
            var size = new Vector2((float)canvas.Bounds.Width, (float)canvas.Bounds.Height);

            foreach (var source in new[] { editor.Sources[1], editor.Sources[0], editor.Sources[1] })
            {
                editor.SelectedSource = source;
                Dispatcher.UIThread.RunJobs();
                var fitted = new CanvasView();
                fitted.Fit(editor.Scene.Bounds!.Value, size);
                Assert.Equal(fitted.Zoom, canvas.View.Zoom);
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task The_animation_button_enters_the_mode_and_shows_the_bar()
    {
        await using var session = await OpenTownAsync();
        var editor = await session.EditTileSetAsync("Tiles/Town");
        var (view, window) = Show(editor);
        try
        {
            Assert.False(view.FindControl<Control>("AnimationBar")!.IsEffectivelyVisible);

            Click(window, view.FindControl<ToggleButton>("AnimationModeButton")!);

            Assert.Equal(TileSetEditorMode.Animation, editor.Mode);
            Assert.True(view.FindControl<Control>("AnimationBar")!.IsEffectivelyVisible);
            Assert.Contains("Click the tile to animate.", Texts(view));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Clicks_on_the_canvas_pick_the_tile_then_add_frames_to_the_bar()
    {
        await using var session = await OpenTownAsync();
        var editor = await session.EditTileSetAsync("Tiles/Town");
        var (view, window) = Show(editor);
        try
        {
            Click(window, view.FindControl<ToggleButton>("AnimationModeButton")!);

            ClickTile(view, window, 0, 0);
            Assert.Contains("Click the tiles of its frames, in order.", Texts(view));
            ClickTile(view, window, 1, 0);
            ClickTile(view, window, 2, 0);

            var list = view.FindControl<ListBox>("FrameList")!;
            Assert.Equal(2, list.GetVisualDescendants().OfType<ListBoxItem>().Count());
            Assert.Equal(2, list.GetVisualDescendants().OfType<PictureView>().Count(picture => picture.Picture is not null));
            Assert.Contains("0.2 s", Texts(list));
            Assert.Contains("2", Texts(list));
            Assert.DoesNotContain("Click the tiles of its frames, in order.", Texts(view));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task An_animated_tile_plays_on_the_canvas()
    {
        // Frames of 2 s, so that the canvas's own ticks, which follow the clock of the test, do not change the frame read first
        var tileSet = TestTown.TileSet();
        tileSet.Tiles.Add(new TileDefinition
        {
            Tile = TileId.Parse("1:0,0"),
            Animation = { new TileAnimationFrame { Tile = TileId.Parse("1:1,0"), Duration = 2 }, new TileAnimationFrame { Tile = TileId.Parse("1:2,0"), Duration = 2 } },
        });
        await using var session = await OpenTownAsync(tileSet);
        var editor = await session.EditTileSetAsync("Tiles/Town");
        var (view, window) = Show(editor);
        try
        {
            var canvas = view.FindControl<TileCanvas>("Canvas")!;
            var middle = At(view, window, 0, 0);
            Assert.True(canvas.IsTicking);
            Assert.Equal(TestTown.Flat(1, 0), CanvasProbe.Pixel(window, (int)middle.X, (int)middle.Y));

            for (var tick = 0; tick < 9; tick++)
            {
                canvas.Tick(TimeSpan.FromSeconds(0.25));
            }

            Assert.Equal(TestTown.Flat(2, 0), CanvasProbe.Pixel(window, (int)middle.X, (int)middle.Y));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task The_bar_removes_and_moves_the_selected_frame_and_the_canvas_has_the_focus_again()
    {
        await using var session = await OpenTownAsync(Animated());
        var editor = await session.EditTileSetAsync("Tiles/Town");
        var (view, window) = Show(editor);
        try
        {
            var canvas = view.FindControl<TileCanvas>("Canvas")!;
            Click(window, view.FindControl<ToggleButton>("AnimationModeButton")!);
            ClickTile(view, window, 0, 0);
            Assert.Equal(3, editor.Frames.Count);

            Click(window, view.FindControl<ListBox>("FrameList")!.GetVisualDescendants().OfType<ListBoxItem>().First());
            Assert.Same(editor.Frames[0], editor.SelectedFrame);
            Click(window, view.FindControl<Button>("MoveFrameRightButton")!);
            Assert.Equal(Tiles("1:2,0", "1:1,0", "1:3,0"), FrameTiles(session));
            Assert.True(canvas.IsFocused);

            Click(window, view.FindControl<Button>("RemoveFrameButton")!);
            Assert.Equal(Tiles("1:2,0", "1:3,0"), FrameTiles(session));
            Assert.True(canvas.IsFocused);

            Click(window, view.FindControl<Button>("ClearFramesButton")!);
            Assert.Empty(FrameTiles(session));
            Click(window, view.FindControl<Button>("DoneAnimationButton")!);
            Assert.Null(editor.AnimatedTile);
            Assert.True(canvas.IsFocused);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Delete_on_the_canvas_removes_the_selected_frame()
    {
        await using var session = await OpenTownAsync(Animated());
        var editor = await session.EditTileSetAsync("Tiles/Town");
        var (view, window) = Show(editor);
        try
        {
            Click(window, view.FindControl<ToggleButton>("AnimationModeButton")!);
            ClickTile(view, window, 0, 0);
            editor.SelectedFrame = editor.Frames[1];
            Assert.True(view.FindControl<TileCanvas>("Canvas")!.IsFocused);

            window.KeyPress(Key.Delete, RawInputModifiers.None, PhysicalKey.Delete, null);
            window.KeyRelease(Key.Delete, RawInputModifiers.None, PhysicalKey.Delete, null);

            Assert.Equal(Tiles("1:1,0", "1:3,0"), FrameTiles(session));
        }
        finally
        {
            window.Close();
        }
    }

    private static (TileSetEditorView View, Window Window) Show(TileSetEditorViewModel editor)
    {
        var view = new TileSetEditorView { DataContext = editor };
        return (view, CanvasProbe.Show(view, 800, 600));
    }

    // The middle of the tile (column, row) of the selected source, in the window
    private static Point At(TileSetEditorView view, Window window, int column, int row)
    {
        var canvas = view.FindControl<TileCanvas>("Canvas")!;
        var screen = canvas.View.ToScreen(new Vector2(column * 16 + 8, row * 16 + 8));
        return canvas.TranslatePoint(new Point(screen.X, screen.Y), window)!.Value;
    }

    private static void ClickTile(TileSetEditorView view, Window window, int column, int row)
    {
        var tile = At(view, window, column, row);
        window.MouseDown(tile, MouseButton.Left);
        window.MouseUp(tile, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    private static List<TileId> Tiles(params string[] tiles) => [.. tiles.Select(TileId.Parse)];

    private static List<TileId> FrameTiles(TesseraSession session)
        => [.. session.TileSetAt("Tiles/Town").Asset.GetDefinition(TileId.Parse("1:0,0"))?.Animation.Select(frame => frame.Tile) ?? []];

    // The town set, whose tile 1:0,0 has the frames 1:1,0, 1:2,0 and 1:3,0
    private static TileSetAsset Animated()
    {
        var tileSet = TestTown.TileSet();
        var definition = new TileDefinition { Tile = TileId.Parse("1:0,0") };
        foreach (var frame in new[] { "1:1,0", "1:2,0", "1:3,0" })
        {
            definition.Animation.Add(new TileAnimationFrame { Tile = TileId.Parse(frame), Duration = 0.2f });
        }
        tileSet.Tiles.Add(definition);
        return tileSet;
    }

    private static void Click(Window window, Control control)
    {
        var middle = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
        window.MouseDown(middle, MouseButton.Left);
        window.MouseUp(middle, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    // A control of a template, found by its name among the visible ones
    private static T Named<T>(Control root, string name) where T : Control
        => root.GetVisualDescendants().OfType<T>().Single(control => control.Name == name && control.IsEffectivelyVisible);

    private static List<string> Texts(Control root)
    {
        Dispatcher.UIThread.RunJobs();
        return [.. root.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible).Select(block => block.Text ?? "")];
    }

    private static async Task<TesseraSession> OpenTownAsync(TileSetAsset? tileSet = null)
    {
        var options = new StrideEditorTestSessionOptions { Dispatcher = DispatcherService.Create(), Plugins = [new TesseraEditorAvaloniaPlugin()] };
        var session = await TesseraSession.OpenAsync(options, ("Tiles/Town", tileSet ?? TestTown.TileSet()));
        session.WriteImage("Tiles/town.png", 64, 32, TestTown.Pixel);
        return session;
    }
}
