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
using ColorRise.Tessera.Editor.Palette;
using ColorRise.Tessera.Editor.Tests;
using ColorRise.Tessera.Editor.TileMapEditor;
using Stride.Assets.Editor.Avalonia;
using Stride.Assets.Editor.Testing;
using Stride.Core.Assets;
using Stride.Core.Presentation.Avalonia.Services;
using Stride.Core.Serialization;
using Int2 = Stride.Core.Mathematics.Int2;
using StrideColor = Stride.Core.Mathematics.Color;
using Vector2 = Stride.Core.Mathematics.Vector2;

namespace ColorRise.Tessera.Editor.Avalonia.Tests;

/// <summary>
/// The view of the map editor, in a window, as a person uses it.
/// </summary>
public sealed class TileMapEditorViewTests
{
    private const string Location = "Maps/Town";

    // Ground has a tile at each corner of the cells 0 to 6, so that the fit shows them all; Details is empty
    private static readonly string[][] TwoLayers = [TestTown.Rows((0, 0, "0,0"), (6, 0, "1,0"), (0, 6, "0,1"), (6, 6, "1,1")), ["."]];

    [AvaloniaFact]
    public async Task The_editor_opens_from_the_session_with_its_view()
    {
        await using var session = await OpenAsync(TwoLayers);
        var map = session.MapAt(Location);

        session.Session.EditorCollection.OpenAssetEditor(map);

        var editor = Assert.IsType<TileMapEditorViewModel>(Assert.Single(session.Session.EditorCollection.Editors));
        // The editor area of Game Studio makes the view of an editor with this template
        var area = new ContentControl { Content = editor, ContentTemplate = new EditorViewSelector { Session = session.Session } };
        var window = CanvasProbe.Show(area, 1280, 720);
        try
        {
            var view = Assert.Single(area.GetVisualDescendants().OfType<TileMapEditorView>());
            Assert.Same(editor, view.DataContext);
        }
        finally
        {
            window.Close();
            await session.Session.EditorCollection.CloseAssetEditorAsync(map, false);
        }
    }

    [AvaloniaFact]
    public async Task The_view_draws_the_map()
    {
        await using var session = await OpenAsync(TwoLayers);
        var editor = await session.EditMapAsync(Location);
        var (view, window) = Show(editor);
        try
        {
            var cell = At(view, window, 6, 0);
            Assert.Equal(TestTown.Flat(1, 0), CanvasProbe.Pixel(window, (int)cell.X, (int)cell.Y));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task A_click_with_the_stamp_tool_paints_in_one_step()
    {
        await using var session = await OpenAsync(TwoLayers);
        var editor = await session.EditMapAsync(Location);
        var (view, window) = Show(editor);
        try
        {
            Click(window, InPalette(view, window, 2, 1));
            Click(window, At(view, window, 3, 3));
            // The pointer leaves the canvas, and the ghost with it
            window.MouseMove(new Point(1, 1));

            Assert.Equal(TestTown.Cell(2, 1), editor.Document.GetLayer(1).GetCell(new Int2(3, 3)));
            Assert.Equal(["Stamp"], session.Steps);
            var cell = At(view, window, 3, 3);
            Assert.Equal(TestTown.Flat(2, 1), CanvasProbe.Pixel(window, (int)cell.X, (int)cell.Y));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task A_drag_in_the_palette_makes_a_stamp_of_several_tiles()
    {
        await using var session = await OpenAsync(TwoLayers);
        var editor = await session.EditMapAsync(Location);
        var (view, window) = Show(editor);
        try
        {
            var empty = At(view, window, 4, 3);
            var before = CanvasProbe.Pixel(window, (int)empty.X, (int)empty.Y);

            window.MouseDown(InPalette(view, window, 0, 0), MouseButton.Left);
            window.MouseMove(InPalette(view, window, 1, 0));
            window.MouseUp(InPalette(view, window, 1, 0), MouseButton.Left);
            window.MouseMove(At(view, window, 3, 3));

            Assert.Equal(new Int2(2, 1), editor.Stamp!.Size);
            // The ghost covers the cell on the right of the cursor too
            Assert.NotEqual(before, CanvasProbe.Pixel(window, (int)empty.X, (int)empty.Y));
            Assert.Empty(session.Steps);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task The_keys_reach_the_tools_when_the_canvas_has_the_focus()
    {
        await using var session = await OpenAsync(TwoLayers);
        var editor = await session.EditMapAsync(Location);
        var (view, window) = Show(editor);
        try
        {
            Click(window, At(view, window, 3, 3));
            var stamp = new TileStamp(new Int2(2, 1), [TestTown.Cell(0, 0), TestTown.Cell(1, 0)]);
            editor.Stamp = stamp;

            PressKey(window, Key.E, PhysicalKey.E, "e");
            Assert.Equal(TileToolKind.Erase, editor.Tool);
            PressKey(window, Key.F, PhysicalKey.F, "f");
            Assert.Equal(TileToolKind.Fill, editor.Tool);
            PressKey(window, Key.X, PhysicalKey.X, "x");
            Assert.Equal(stamp.FlipX(), editor.Stamp);
            PressKey(window, Key.Z, PhysicalKey.Z, "Z", RawInputModifiers.Shift);
            Assert.Equal(stamp.FlipX().RotateCounterClockwise(), editor.Stamp);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task The_keys_do_not_reach_the_tools_while_a_name_is_typed()
    {
        await using var session = await OpenAsync(TwoLayers);
        var editor = await session.EditMapAsync(Location);
        var (view, window) = Show(editor);
        try
        {
            Click(window, At(view, window, 3, 3));
            DoubleClick(window, Named<TextBlock>(view, "LayerName", editor.Layers[0]));
            Assert.True(editor.Layers[0].IsEditing);

            foreach (var (key, physical, text) in new[] { (Key.B, PhysicalKey.B, "B"), (Key.E, PhysicalKey.E, "e"), (Key.E, PhysicalKey.E, "e"), (Key.F, PhysicalKey.F, "f") })
            {
                window.KeyPress(key, RawInputModifiers.None, physical, text);
                window.KeyTextInput(text);
                window.KeyRelease(key, RawInputModifiers.None, physical, text);
            }
            Assert.Equal(TileToolKind.Stamp, editor.Tool);
            PressKey(window, Key.Enter, PhysicalKey.Enter, "\r");

            Assert.Equal("Beef", session.MapAt(Location).Asset.Layers[1].Name);
            Assert.Equal(["Rename layer"], session.Steps);
            Assert.Equal(TileToolKind.Stamp, editor.Tool);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task The_eye_hides_the_layer_and_the_lock_stops_the_stamp()
    {
        await using var session = await OpenAsync(TwoLayers);
        var editor = await session.EditMapAsync(Location);
        var (view, window) = Show(editor);
        var ground = editor.Layers[1];
        try
        {
            Click(window, Named<ToggleButton>(view, "EyeButton", ground));
            Assert.False(session.MapAt(Location).Asset.Layers[0].Visible);
            Assert.Equal(["Layer visibility"], session.Steps);
            Click(window, Named<ToggleButton>(view, "EyeButton", ground));
            Assert.True(session.MapAt(Location).Asset.Layers[0].Visible);

            editor.SelectedLayer = ground;
            editor.Stamp = TileStamp.Single(TestTown.Cell(2, 1));
            Click(window, Named<ToggleButton>(view, "LockButton", ground));
            Click(window, At(view, window, 3, 3));

            Assert.True(ground.IsLocked);
            Assert.True(editor.Document.GetLayer(0).GetCell(new Int2(3, 3)).IsEmpty);
            Assert.Contains("The layer 'Layer0' is locked.", Texts(view));
            Assert.Contains("Layer0 (locked)", Texts(view));
        }
        finally
        {
            ground.IsLocked = false;
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task The_keys_reach_the_tools_after_a_click_on_the_tool_bar()
    {
        await using var session = await OpenAsync(TwoLayers);
        var editor = await session.EditMapAsync(Location);
        var (view, window) = Show(editor);
        try
        {
            Click(window, view.FindControl<ToggleButton>("FillToolButton")!);
            PressKey(window, Key.B, PhysicalKey.B, "b");
            Assert.Equal(TileToolKind.Stamp, editor.Tool);

            Click(window, view.FindControl<ToggleButton>("GridButton")!);
            PressKey(window, Key.E, PhysicalKey.E, "e");
            Assert.Equal(TileToolKind.Erase, editor.Tool);
            Assert.True(view.FindControl<TileCanvas>("Canvas")!.IsFocused);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task The_tool_buttons_show_the_current_tool()
    {
        await using var session = await OpenAsync(TwoLayers);
        var editor = await session.EditMapAsync(Location);
        var (view, window) = Show(editor);
        try
        {
            Click(window, view.FindControl<ToggleButton>("FillToolButton")!);

            Assert.Equal(TileToolKind.Fill, editor.Tool);
            Assert.Equal(["FillToolButton"], ToolButtons(view).Where(button => button.IsChecked == true).Select(button => button.Name));
            Assert.Equal("Stamp (B)", ToolTip.GetTip(view.FindControl<ToggleButton>("StampToolButton")!));

            Click(window, At(view, window, 3, 3));
            editor.Tool = TileToolKind.Fill;
            PressKey(window, Key.B, PhysicalKey.B, "b");
            Assert.Equal(["StampToolButton"], ToolButtons(view).Where(button => button.IsChecked == true).Select(button => button.Name));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task The_status_line_follows_the_pointer()
    {
        await using var session = await OpenAsync(TwoLayers);
        var editor = await session.EditMapAsync(Location);
        var (view, window) = Show(editor);
        try
        {
            editor.View.Zoom = 1;
            window.MouseMove(At(view, window, 3, 3));

            var texts = Texts(view);
            Assert.Contains("cell 3, 3", texts);
            Assert.Contains("100%", texts);
            Assert.Contains("Details", texts);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task A_missing_tile_set_shows_in_place_of_the_palette()
    {
        await using var session = await OpenAsync(TwoLayers, asset =>
        {
            asset.TileSets.Add(new TileSetSlot { Slot = 2, TileSet = AttachedReferenceManager.CreateProxyObject<TileSet>(AssetId.New(), "Tiles/Gone") });
            asset.LastSlot = 2;
        });
        var editor = await session.EditMapAsync(Location);
        var (view, window) = Show(editor);
        try
        {
            Assert.Single(view.GetVisualDescendants().OfType<TilePalette>(), palette => palette.IsEffectivelyVisible);

            editor.SelectedTileSet = editor.TileSets[1];

            var texts = Texts(view);
            Assert.Contains("This tile set is missing.", texts);
            Assert.Contains("missing (slot 2)", texts);
            Assert.DoesNotContain(view.GetVisualDescendants().OfType<TilePalette>(), palette => palette.IsEffectivelyVisible);
        }
        finally
        {
            window.Close();
        }
    }

    // The editor's view in a window of 1280 by 720
    private static (TileMapEditorView View, Window Window) Show(TileMapEditorViewModel editor)
    {
        var view = new TileMapEditorView { DataContext = editor };
        var window = CanvasProbe.Show(view, 1280, 720);
        Dispatcher.UIThread.RunJobs();
        return (view, window);
    }

    // The middle of a cell of the current layer, in the window
    private static Point At(TileMapEditorView view, Window window, int x, int y)
    {
        var canvas = view.FindControl<TileCanvas>("Canvas")!;
        var editor = (TileMapEditorViewModel)view.DataContext!;
        var screen = canvas.View.ToScreen(editor.Scene.CellBounds(new Int2(x, y)).Center);
        return canvas.TranslatePoint(new Point(screen.X, screen.Y), window)!.Value;
    }

    // The middle of the tile (column, row) of the palette's source, in the window
    private static Point InPalette(TileMapEditorView view, Window window, int column, int row)
    {
        var palette = Assert.Single(view.GetVisualDescendants().OfType<TilePalette>(), control => control.IsEffectivelyVisible);
        var canvas = palette.GetVisualDescendants().OfType<TileCanvas>().Single();
        var screen = canvas.View.ToScreen(new Vector2(column * 16 + 8, row * 16 + 8));
        return canvas.TranslatePoint(new Point(screen.X, screen.Y), window)!.Value;
    }

    private static void Click(Window window, Point point)
    {
        window.MouseMove(point);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Click(Window window, Control control) => Click(window, Middle(window, control));

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

    private static void PressKey(Window window, Key key, PhysicalKey physical, string text, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPress(key, modifiers, physical, text);
        window.KeyRelease(key, modifiers, physical, text);
        Dispatcher.UIThread.RunJobs();
    }

    // A control of the row of a layer, found by its name
    private static T Named<T>(Control root, string name, TileLayerItemViewModel layer) where T : Control
        => root.GetVisualDescendants().OfType<T>().Single(control => control.Name == name && control.DataContext == layer && control.IsEffectivelyVisible);

    private static IEnumerable<ToggleButton> ToolButtons(TileMapEditorView view)
        => view.GetVisualDescendants().OfType<ToggleButton>().Where(button => button.Name?.EndsWith("ToolButton", StringComparison.Ordinal) == true);

    private static List<string> Texts(Control root)
    {
        Dispatcher.UIThread.RunJobs();
        return [.. root.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible).Select(block => block.Text ?? "")];
    }

    private static async Task<TesseraSession> OpenAsync(string[][] layers, Action<TileMapAsset>? change = null)
    {
        var options = new StrideEditorTestSessionOptions { Dispatcher = DispatcherService.Create(), Plugins = [new TesseraEditorAvaloniaPlugin()] };
        var tileSet = TestTown.TileSet();
        var asset = TestTown.Map(tileSet, "Tiles/Town", layers);
        asset.Layers[1].Name = "Details";
        change?.Invoke(asset);
        var session = await TesseraSession.OpenAsync(options, ("Tiles/Town", tileSet), (Location, asset));
        session.WriteImage("Tiles/town.png", 64, 32, TestTown.Pixel);
        return session;
    }
}
