using ColorRise.Tessera.Assets;
using ColorRise.Tessera.Editor.Canvas;
using ColorRise.Tessera.Editor.Palette;
using ColorRise.Tessera.Editor.TileMapEditor;
using Stride.Core.Assets;
using Stride.Core.Assets.Editor.Testing;
using Stride.Core.Mathematics;
using Stride.Core.Quantum;
using static ColorRise.Tessera.Editor.Tests.TestTown;

namespace ColorRise.Tessera.Editor.Tests;

/// <summary>
/// The tools of the map editor, driven through its input as the canvas drives it.
/// </summary>
public sealed class TileMapToolsTests
{
    private const string Location = "Maps/Town";
    private const string OtherLocation = "Maps/Other";

    [Fact]
    public async Task A_click_stamps_in_one_step()
    {
        await using var map = await OpenAsync([["."]]);
        map.Editor.Stamp = TileStamp.Single(Cell(2, 1));

        Click(map.Editor, 1, 1);

        Assert.Equal(["Stamp"], map.Session.Steps);
        Assert.Equal([". . .", ". 2,1 .", ". . ."], Tokens(Saved(map, 0), 0, 0, 2, 2));
    }

    [Fact]
    public async Task A_drag_stamps_along_the_way_in_one_step()
    {
        await using var map = await OpenAsync([["."]]);
        map.Editor.Stamp = TileStamp.Single(Cell(1, 0));

        Move(map.Editor, 0, 0);
        Press(map.Editor, 0, 0);
        Move(map.Editor, 5, 0);
        Release(map.Editor, 5, 0);

        Assert.Equal(["Stamp"], map.Session.Steps);
        Assert.Equal(["1,0 1,0 1,0 1,0 1,0 1,0 ."], Tokens(Saved(map, 0), 0, 0, 6, 0));
    }

    [Fact]
    public async Task The_stroke_shows_before_the_release_and_undo_takes_all_of_it()
    {
        await using var map = await OpenAsync([["."]]);
        map.Editor.Stamp = TileStamp.Single(Cell(1, 0));

        Press(map.Editor, 0, 0);
        Move(map.Editor, 2, 0);

        Assert.Equal(["1,0 1,0 1,0"], Tokens(map.Editor.Document.GetLayer(0), 0, 0, 2, 0));
        Assert.Equal([". . ."], Tokens(Saved(map, 0), 0, 0, 2, 0));
        Assert.Empty(map.Session.Steps);

        Release(map.Editor, 2, 0);
        Assert.Equal(["1,0 1,0 1,0"], Tokens(Saved(map, 0), 0, 0, 2, 0));

        map.Session.UndoRedo.Undo();
        Assert.Equal([". . ."], Tokens(Saved(map, 0), 0, 0, 2, 0));
        Assert.Equal([". . ."], Tokens(map.Editor.Document.GetLayer(0), 0, 0, 2, 0));
    }

    [Fact]
    public async Task Shift_click_draws_a_line_from_the_last_cell()
    {
        await using var map = await OpenAsync([["."]]);
        map.Editor.Stamp = TileStamp.Single(Cell(1, 0));

        Click(map.Editor, 0, 0);
        Click(map.Editor, 3, 3, modifiers: CanvasModifiers.Shift);

        Assert.Equal(["Stamp", "Stamp"], map.Session.Steps);
        Assert.Equal([". . . 1,0", ". . 1,0 .", ". 1,0 . .", "1,0 . . ."], Tokens(Saved(map, 0), 0, 0, 3, 3));
    }

    [Fact]
    public async Task A_stamp_of_several_tiles_paints_its_block_and_skips_its_holes()
    {
        await using var map = await OpenAsync([["3,0 3,0 3,0 3,0", "3,0 3,0 3,0 3,0", "3,0 3,0 3,0 3,0"]]);
        // The bottom row 0,0 . 1,0, the top row 2,0 0,1 .
        map.Editor.Stamp = new TileStamp(new Int2(3, 2), [Cell(0, 0), TileCell.Empty, Cell(1, 0), Cell(2, 0), Cell(0, 1), TileCell.Empty]);

        // The cursor is on the middle cell of the bottom row: the stamp's bottom-left cell is (1, 1)
        Click(map.Editor, 2, 1);

        Assert.Equal(["Stamp"], map.Session.Steps);
        Assert.Equal(["3,0 2,0 0,1 3,0", "3,0 0,0 3,0 1,0", "3,0 3,0 3,0 3,0"], Tokens(Saved(map, 0), 0, 0, 3, 2));
    }

    [Fact]
    public async Task A_right_drag_captures_a_stamp()
    {
        await using var map = await OpenAsync([["0,1 .", "0,0 1,0"]]);

        Drag(map.Editor, (1, 1), (0, 0), CanvasButton.Right);

        var stamp = new TileStamp(new Int2(2, 2), [Cell(0, 0), Cell(1, 0), Cell(0, 1), TileCell.Empty]);
        Assert.Equal(stamp, map.Editor.Stamp);
        Assert.Equal(TileToolKind.Stamp, map.Editor.Tool);
        Assert.Empty(map.Session.Steps);

        // Cells without a tile keep the stamp there was
        Drag(map.Editor, (5, 5), (6, 6), CanvasButton.Right);
        Assert.Equal(stamp, map.Editor.Stamp);
    }

    [Fact]
    public async Task The_ghost_follows_the_cursor_and_leaves_with_it()
    {
        await using var map = await OpenAsync([["."]]);
        map.Editor.Stamp = new TileStamp(new Int2(3, 2), [Cell(0, 0), Cell(1, 0), Cell(2, 0), Cell(0, 1), Cell(1, 1), Cell(2, 1)]);

        Move(map.Editor, 5, 5);

        Assert.Equal(new Int2(5, 5), map.Editor.HoverCell);
        var origin = map.Editor.Scene.CellBounds(new Int2(4, 5));
        var ghost = Assert.Single(Collect(map.Editor.Scene).Sprites, sprite => sprite.Opacity < 1);
        Assert.Equal(new RectangleF(origin.X, origin.Bottom - 32, 48, 32), ghost.Destination);

        map.Editor.Input.PointerLeft();

        Assert.Null(map.Editor.HoverCell);
        Assert.DoesNotContain(Collect(map.Editor.Scene).Sprites, sprite => sprite.Opacity < 1);
    }

    [Fact]
    public async Task The_rectangle_tool_fills_the_dragged_rectangle()
    {
        await using var map = await OpenAsync([["."]]);
        map.Editor.Tool = TileToolKind.Rectangle;
        map.Editor.Stamp = TileStamp.Single(Cell(1, 0));

        Move(map.Editor, 1, 1);
        Press(map.Editor, 1, 1);
        Move(map.Editor, 4, 3);
        Assert.Equal(12, Count(map.Editor.Document.GetLayer(0)));
        Move(map.Editor, 2, 2);
        Assert.Equal(4, Count(map.Editor.Document.GetLayer(0)));
        Release(map.Editor, 2, 2);

        Assert.Equal(["Rectangle"], map.Session.Steps);
        Assert.Equal([". . . . .", ". . . . .", ". 1,0 1,0 . .", ". 1,0 1,0 . .", ". . . . ."], Tokens(Saved(map, 0), 0, 0, 4, 4));
    }

    [Fact]
    public async Task The_fill_tool_fills_inside_the_frame()
    {
        await using var map = await OpenAsync([["."]], asset => asset.Size = new Int2(8, 8));
        map.Editor.Tool = TileToolKind.Fill;
        map.Editor.Stamp = TileStamp.Single(Cell(2, 0));

        Click(map.Editor, 3, 3);

        Assert.Equal(["Fill"], map.Session.Steps);
        var saved = Saved(map, 0);
        Assert.Equal(64, Count(saved));
        Assert.All(Tokens(saved, 0, 0, 7, 7), row => Assert.Equal(string.Join(' ', Enumerable.Repeat("2,0", 8)), row));
    }

    [Fact]
    public async Task The_erase_tool_erases_along_a_drag_and_a_rectangle_with_the_right_button()
    {
        var full = string.Join(' ', Enumerable.Repeat("1,0", 6));
        await using var map = await OpenAsync([[full, full, full, full]]);
        map.Editor.Tool = TileToolKind.Erase;

        Drag(map.Editor, (0, 0), (5, 0));
        Drag(map.Editor, (1, 1), (2, 3), CanvasButton.Right);

        Assert.Equal(["Erase", "Erase"], map.Session.Steps);
        Assert.Equal(
            ["1,0 . . 1,0 1,0 1,0", "1,0 . . 1,0 1,0 1,0", "1,0 . . 1,0 1,0 1,0", ". . . . . ."],
            Tokens(Saved(map, 0), 0, 0, 5, 3));
    }

    [Fact]
    public async Task The_pick_tool_takes_the_cell_as_the_stamp_and_goes_back_to_the_stamp_tool()
    {
        await using var map = await OpenAsync([["2,1h ."]]);
        map.Editor.Tool = TileToolKind.Pick;

        Click(map.Editor, 1, 0);
        Assert.Null(map.Editor.Stamp);
        Assert.Equal(TileToolKind.Pick, map.Editor.Tool);

        Click(map.Editor, 0, 0);
        Assert.Equal(TileStamp.Single(Cell(2, 1, TileFlip.Horizontal)), map.Editor.Stamp);
        Assert.Equal(TileToolKind.Stamp, map.Editor.Tool);
        Assert.Empty(map.Session.Steps);
    }

    [Fact]
    public async Task X_Y_and_Z_flip_and_turn_the_stamp()
    {
        await using var map = await OpenAsync([["."]]);
        var stamp = new TileStamp(new Int2(2, 1), [Cell(0, 0), Cell(1, 0)]);
        map.Editor.Stamp = stamp;

        Assert.True(Key(map.Editor, CanvasKey.X));
        Assert.Equal(stamp.FlipX(), map.Editor.Stamp);
        Key(map.Editor, CanvasKey.X);
        Assert.True(Key(map.Editor, CanvasKey.Y));
        Assert.Equal(stamp.FlipY(), map.Editor.Stamp);
        Key(map.Editor, CanvasKey.Y);
        Assert.True(Key(map.Editor, CanvasKey.Z));
        Assert.Equal(stamp.RotateClockwise(), map.Editor.Stamp);
        Assert.True(Key(map.Editor, CanvasKey.Z, CanvasModifiers.Shift));
        Assert.Equal(stamp, map.Editor.Stamp);
    }

    [Fact]
    public async Task A_stamp_of_tiles_that_are_not_square_is_not_turned()
    {
        var tileSet = TileSet();
        tileSet.TileSize = new Int2(16, 32);
        await using var map = await OpenAsync(tileSet, Map(tileSet, "Tiles/Town", ["."]));
        var stamp = new TileStamp(new Int2(2, 1), [Cell(0, 0), Cell(1, 0)]);
        map.Editor.Stamp = stamp;

        Assert.True(Key(map.Editor, CanvasKey.Z));
        Assert.Equal(stamp, map.Editor.Stamp);
        Assert.Equal("Only a stamp of square tiles can be turned.", map.Editor.Message);
        map.Editor.RotateStamp(clockwise: false);
        Assert.Equal(stamp, map.Editor.Stamp);

        // A mirror keeps the shape of a tile
        Assert.True(Key(map.Editor, CanvasKey.X));
        Assert.Equal(stamp.FlipX(), map.Editor.Stamp);
    }

    [Fact]
    public async Task The_tool_keys_choose_the_tool()
    {
        await using var map = await OpenAsync([["."]]);

        foreach (var (key, tool) in new[] { (CanvasKey.P, TileToolKind.Rectangle), (CanvasKey.F, TileToolKind.Fill), (CanvasKey.E, TileToolKind.Erase), (CanvasKey.I, TileToolKind.Pick), (CanvasKey.R, TileToolKind.Select), (CanvasKey.B, TileToolKind.Stamp) })
        {
            Assert.True(Key(map.Editor, key));
            Assert.Equal(tool, map.Editor.Tool);
        }
        Assert.False(Key(map.Editor, CanvasKey.Enter));
        // With Control, a key is a shortcut of Game Studio, such as Control+Z
        Assert.False(Key(map.Editor, CanvasKey.F, CanvasModifiers.Control));
        Assert.Equal(TileToolKind.Stamp, map.Editor.Tool);
    }

    [Fact]
    public async Task Select_then_move_is_one_step()
    {
        await using var map = await OpenAsync([["0,1 1,1 . 3,0", "0,0 . . ."]]);
        map.Editor.Tool = TileToolKind.Select;

        Drag(map.Editor, (0, 0), (1, 1));
        Assert.Equal(new Rectangle(0, 0, 2, 2), map.Editor.SelectedCells);
        Assert.Empty(map.Session.Steps);

        // The empty cell (1, 0) of the selection lands on (3, 1), and leaves its tile there
        Drag(map.Editor, (1, 1), (3, 2));

        Assert.Equal(["Move"], map.Session.Steps);
        Assert.Equal(new Rectangle(2, 1, 2, 2), map.Editor.SelectedCells);
        Assert.Equal([". . 0,1 1,1", ". . 0,0 3,0", ". . . ."], Tokens(Saved(map, 0), 0, 0, 3, 2));
    }

    [Fact]
    public async Task Copy_and_paste_make_the_selection_the_stamp()
    {
        await using var map = await OpenAsync([["0,1 .", "0,0 1,0"]]);
        map.Editor.Tool = TileToolKind.Select;
        Drag(map.Editor, (0, 0), (1, 1));

        map.Editor.CopyCommand.Execute();
        map.Editor.Tool = TileToolKind.Erase;
        map.Editor.PasteCommand.Execute();

        Assert.Equal(TileToolKind.Stamp, map.Editor.Tool);
        Assert.Equal(new TileStamp(new Int2(2, 2), [Cell(0, 0), Cell(1, 0), Cell(0, 1), TileCell.Empty]), map.Editor.Stamp);
        Click(map.Editor, 5, 5);
        Assert.Equal(["Stamp"], map.Session.Steps);
        Assert.Equal(["0,1 .", "0,0 1,0"], Tokens(Saved(map, 0), 5, 5, 6, 6));
    }

    [Fact]
    public async Task Cut_and_delete_clear_the_selection_in_one_step()
    {
        await using var map = await OpenAsync([["0,0 1,0 2,0"]]);
        map.Editor.Tool = TileToolKind.Select;

        Drag(map.Editor, (0, 0), (1, 0));
        map.Editor.CutCommand.Execute();

        Assert.Equal(["Cut"], map.Session.Steps);
        Assert.Equal([". . 2,0"], Tokens(Saved(map, 0), 0, 0, 2, 0));
        map.Editor.PasteCommand.Execute();
        Assert.Equal(new TileStamp(new Int2(2, 1), [Cell(0, 0), Cell(1, 0)]), map.Editor.Stamp);

        map.Editor.Tool = TileToolKind.Select;
        Drag(map.Editor, (2, 0), (2, 0));
        Assert.True(Key(map.Editor, CanvasKey.Delete));

        Assert.Equal(["Cut", "Delete"], map.Session.Steps);
        Assert.Equal([". . ."], Tokens(Saved(map, 0), 0, 0, 2, 0));
    }

    [Fact]
    public async Task Paste_in_another_map_uses_that_map_s_slot_for_the_tile_set()
    {
        var tileSet = TileSet();
        var other = Map(tileSet, "Tiles/Town", [["."]]);
        other.TileSets[0].Slot = 3;
        other.LastSlot = 3;
        await using var map = await OpenAsync(tileSet, Map(tileSet, "Tiles/Town", [["2,1v 1,0"]]), (OtherLocation, other));
        var second = await map.Session.EditMapAsync(OtherLocation);
        map.Editor.Tool = TileToolKind.Select;
        Drag(map.Editor, (0, 0), (1, 0));

        map.Editor.CopyCommand.Execute();
        second.PasteCommand.Execute();

        Assert.Null(second.Message);
        Assert.Equal(new TileStamp(new Int2(2, 1), [new TileCell(3, Cell(2, 1).Tile, TileFlip.Vertical), new TileCell(3, Cell(1, 0).Tile)]), second.Stamp);
    }

    [Fact]
    public async Task Paste_without_the_tile_set_says_which_one_is_missing()
    {
        var tileSet = TileSet();
        var other = Map(tileSet, "Tiles/Town", [["."]]);
        other.TileSets.Clear();
        other.LastSlot = 0;
        await using var map = await OpenAsync(tileSet, Map(tileSet, "Tiles/Town", [["2,1 1,0"]]), (OtherLocation, other));
        var second = await map.Session.EditMapAsync(OtherLocation);
        second.Tool = TileToolKind.Erase;
        map.Editor.Tool = TileToolKind.Select;
        Drag(map.Editor, (0, 0), (1, 0));

        map.Editor.CopyCommand.Execute();
        second.PasteCommand.Execute();

        Assert.Equal("This map does not have the tile set 'Town'. Add it, then paste again.", second.Message);
        Assert.Null(second.Stamp);
        Assert.Equal(TileToolKind.Erase, second.Tool);
    }

    [Fact]
    public async Task Escape_cancels_the_open_stroke_then_clears_the_selection()
    {
        await using var map = await OpenAsync([["."]]);
        map.Editor.Stamp = TileStamp.Single(Cell(1, 0));

        Press(map.Editor, 0, 0);
        Move(map.Editor, 2, 0);
        Assert.True(Key(map.Editor, CanvasKey.Escape));
        Assert.Equal(0, Count(map.Editor.Document.GetLayer(0)));
        Move(map.Editor, 3, 0);
        Release(map.Editor, 3, 0);
        Assert.Equal(0, Count(map.Editor.Document.GetLayer(0)));
        Assert.Empty(map.Session.Steps);

        map.Editor.Tool = TileToolKind.Select;
        Drag(map.Editor, (0, 0), (1, 1));
        Assert.NotNull(map.Editor.SelectedCells);
        Assert.True(Key(map.Editor, CanvasKey.Escape));
        Assert.Null(map.Editor.SelectedCells);
    }

    [Fact]
    public async Task A_second_button_during_a_stroke_and_a_release_without_a_press_do_nothing()
    {
        await using var map = await OpenAsync([["0,0 ."]]);
        var stamp = TileStamp.Single(Cell(1, 0));
        map.Editor.Stamp = stamp;

        Press(map.Editor, 2, 0);
        Move(map.Editor, 3, 0);
        Press(map.Editor, 0, 0, CanvasButton.Right);
        Release(map.Editor, 0, 0, CanvasButton.Right);
        Release(map.Editor, 3, 0);
        Release(map.Editor, 1, 0);

        Assert.Equal(stamp, map.Editor.Stamp);
        Assert.Equal(["Stamp"], map.Session.Steps);
        Assert.Equal(["0,0 . 1,0 1,0"], Tokens(Saved(map, 0), 0, 0, 3, 0));
    }

    [Fact]
    public async Task A_hidden_or_locked_layer_or_no_layer_takes_no_stroke_and_says_why()
    {
        await using var map = await OpenAsync([["."]]);

        Click(map.Editor, 0, 0);
        Assert.Equal("Pick a tile in the palette first.", map.Editor.Message);

        map.Editor.Stamp = TileStamp.Single(Cell(1, 0));
        SetVisible(map, 0, false);
        Click(map.Editor, 0, 0);
        Assert.Equal("The layer 'Layer0' is hidden.", map.Editor.Message);

        SetVisible(map, 0, true);
        map.Editor.Layers[0].IsLocked = true;
        Click(map.Editor, 0, 0);
        Assert.Equal("The layer 'Layer0' is locked.", map.Editor.Message);
        Assert.Equal(0, Count(Saved(map, 0)));

        map.Editor.Layers[0].IsLocked = false;
        Click(map.Editor, 0, 0);
        Assert.Null(map.Editor.Message);
        Assert.Equal(1, Count(Saved(map, 0)));

        RemoveLayer(map, 0);
        Assert.Equal(-1, map.Editor.CurrentLayer);
        Click(map.Editor, 0, 0);
        Assert.Equal("The map has no layer. Add one.", map.Editor.Message);
        Assert.Equal(["Hide", "Show", "Stamp", "Remove"], map.Session.Steps);
    }

    [Fact]
    public async Task The_tools_work_on_the_negative_side()
    {
        await using var map = await OpenAsync([["."]]);
        map.Editor.Stamp = TileStamp.Single(Cell(1, 0));

        Drag(map.Editor, (-3, -3), (2, -3));
        map.Editor.Tool = TileToolKind.Rectangle;
        map.Editor.Stamp = TileStamp.Single(Cell(2, 0));
        Drag(map.Editor, (-20, -20), (-17, -18));

        Assert.Equal(["Stamp", "Rectangle"], map.Session.Steps);
        var saved = Saved(map, 0);
        Assert.Equal(18, Count(saved));
        Assert.Equal([string.Join(' ', Enumerable.Repeat("1,0", 6))], Tokens(saved, -3, -3, 2, -3));
        Assert.All(Tokens(saved, -20, -20, -17, -18), row => Assert.Equal("2,0 2,0 2,0 2,0", row));
    }

    [Fact]
    public async Task A_fill_with_nothing_to_stop_it_stays_in_its_bounds()
    {
        // A ring of tiles from (0, 0) to (9, 9) with a gap at (9, 5)
        var ring = Enumerable.Range(0, 10).SelectMany(x => Enumerable.Range(0, 10).Select(y => (X: x, Y: y)))
            .Where(cell => (cell.X is 0 or 9 || cell.Y is 0 or 9) && cell != (9, 5))
            .Select(cell => (cell.X, cell.Y, "0,0"))
            .ToArray();
        await using var map = await OpenAsync([["."], Rows(ring)]);
        map.Editor.Tool = TileToolKind.Fill;
        map.Editor.Stamp = TileStamp.Single(Cell(2, 0));

        // No frame and no tile: the chunk of the click
        map.Editor.CurrentLayer = 0;
        Click(map.Editor, 40, 40);
        var first = Saved(map, 0);
        Assert.Equal(256, Count(first));
        Assert.Equal(Enumerable.Repeat(string.Join(' ', Enumerable.Repeat("2,0", 16)), 16), Tokens(first, 32, 32, 47, 47));

        // Out through the gap, but not beyond the used cells of the layer
        map.Editor.CurrentLayer = 1;
        Click(map.Editor, 5, 5);
        var second = Saved(map, 1);
        Assert.Equal(100, Count(second));
        Assert.All(Tokens(second, 0, 0, 9, 9), row => Assert.DoesNotContain(".", row.Split(' ')));
        Assert.Equal(["Fill", "Fill"], map.Session.Steps);
    }

    [Fact]
    public async Task An_undo_in_the_middle_of_a_drag_ends_the_stroke()
    {
        await using var map = await OpenAsync([["."]]);
        map.Editor.Stamp = TileStamp.Single(Cell(1, 0));
        Click(map.Editor, 0, 0);

        Press(map.Editor, 2, 0);
        Move(map.Editor, 3, 0);
        map.Session.UndoRedo.Undo();
        Move(map.Editor, 4, 0);
        Release(map.Editor, 4, 0);

        Assert.Equal(["Stamp"], map.Session.Steps);
        Assert.Equal(0, Count(map.Editor.Document.GetLayer(0)));
        Assert.Equal(0, Count(Saved(map, 0)));
    }

    [Fact]
    public async Task A_layer_removed_from_outside_during_a_drag_ends_the_stroke()
    {
        await using var map = await OpenAsync([["."], ["."]]);
        map.Editor.Stamp = TileStamp.Single(Cell(1, 0));
        Assert.Equal(1, map.Editor.CurrentLayer);

        Press(map.Editor, 0, 0);
        Move(map.Editor, 2, 0);
        RemoveLayer(map, 1);
        Assert.Equal(0, map.Editor.CurrentLayer);
        Move(map.Editor, 3, 0);
        Release(map.Editor, 3, 0);

        Assert.Equal(["Remove"], map.Session.Steps);
        Assert.Equal(0, Count(Saved(map, 0)));
        Assert.Equal(0, Count(map.Editor.Document.GetLayer(0)));
    }

    [Fact]
    public async Task The_current_layer_stays_on_its_layer_when_another_is_removed()
    {
        await using var map = await OpenAsync([["."], ["."]]);

        RemoveLayer(map, 0);

        Assert.Equal(0, map.Editor.CurrentLayer);
        Assert.Equal("Layer1", map.Editor.Document.GetLayer(map.Editor.CurrentLayer).Name);
    }

    private static async DispatcherTask<OpenEditor> OpenAsync(string[][] layers, Action<TileMapAsset>? change = null)
    {
        var tileSet = TileSet();
        var asset = Map(tileSet, "Tiles/Town", layers);
        change?.Invoke(asset);
        return await OpenAsync(tileSet, asset);
    }

    private static async DispatcherTask<OpenEditor> OpenAsync(TileSetAsset tileSet, TileMapAsset asset, params (string Location, Asset Asset)[] others)
    {
        var session = await TesseraSession.OpenAsync([("Tiles/Town", tileSet), (Location, asset), .. others]);
        session.WriteImage("Tiles/town.png", 64, 32, Pixel);
        return new OpenEditor(session, await session.EditMapAsync(Location));
    }

    private static Vector2 Middle(TileMapEditorViewModel editor, int x, int y) => editor.Scene.CellBounds(new Int2(x, y)).Center;

    private static void Press(TileMapEditorViewModel editor, int x, int y, CanvasButton button = CanvasButton.Left, CanvasModifiers modifiers = CanvasModifiers.None)
        => editor.Input.PointerPressed(Middle(editor, x, y), button, modifiers);

    private static void Move(TileMapEditorViewModel editor, int x, int y) => editor.Input.PointerMoved(Middle(editor, x, y), CanvasModifiers.None);

    private static void Release(TileMapEditorViewModel editor, int x, int y, CanvasButton button = CanvasButton.Left)
        => editor.Input.PointerReleased(Middle(editor, x, y), button, CanvasModifiers.None);

    private static void Click(TileMapEditorViewModel editor, int x, int y, CanvasButton button = CanvasButton.Left, CanvasModifiers modifiers = CanvasModifiers.None)
    {
        Move(editor, x, y);
        Press(editor, x, y, button, modifiers);
        Release(editor, x, y, button);
    }

    private static void Drag(TileMapEditorViewModel editor, (int X, int Y) from, (int X, int Y) to, CanvasButton button = CanvasButton.Left)
    {
        Move(editor, from.X, from.Y);
        Press(editor, from.X, from.Y, button);
        Move(editor, to.X, to.Y);
        Release(editor, to.X, to.Y, button);
    }

    private static bool Key(TileMapEditorViewModel editor, CanvasKey key, CanvasModifiers modifiers = CanvasModifiers.None) => editor.Input.KeyPressed(key, modifiers);

    private static CanvasFrame Collect(TileMapScene scene)
    {
        var frame = new CanvasFrame();
        scene.Collect(new RectangleF(-10000, -10000, 20000, 20000), 1, frame);
        return frame;
    }

    // The cells of a layer as the tokens of TestTown.Map, the top row first; a slot other than 1 is written before the tile
    private static string[] Tokens(TileLayer layer, int x0, int y0, int x1, int y1)
    {
        var rows = new List<string>();
        for (var y = y1; y >= y0; y--)
        {
            rows.Add(string.Join(' ', Enumerable.Range(x0, x1 - x0 + 1).Select(x => Token(layer.GetCell(new Int2(x, y))))));
        }
        return [.. rows];
    }

    private static string Token(TileCell cell)
    {
        if (cell.IsEmpty)
        {
            return ".";
        }
        var flips = (cell.Flip.HasFlag(TileFlip.Horizontal) ? "h" : "") + (cell.Flip.HasFlag(TileFlip.Vertical) ? "v" : "") + (cell.Flip.HasFlag(TileFlip.Diagonal) ? "d" : "");
        return $"{(cell.Slot == 1 ? "" : $"{cell.Slot}/")}{cell.Tile.Column},{cell.Tile.Row}{flips}";
    }

    private static int Count(TileLayer layer)
    {
        var count = 0;
        foreach (var chunk in layer.Chunks)
        {
            foreach (var cell in chunk.Cells)
            {
                count += cell.IsEmpty ? 0 : 1;
            }
        }
        return count;
    }

    // A layer as the asset has it, read as the build reads it
    private static TileLayer Saved(OpenEditor map, int layer) => map.Session.MapAt(Location).Asset.ToTileMap(_ => null).Layers[layer];

    private static IObjectNode LayersNode(OpenEditor map) => map.Session.MapAt(Location).PropertyGraph.RootNode[nameof(TileMapAsset.Layers)].Target!;

    private static void SetVisible(OpenEditor map, int layer, bool visible)
    {
        using var transaction = map.Session.UndoRedo.CreateTransaction();
        LayersNode(map).IndexedTarget(new NodeIndex(layer))![nameof(TileLayerAsset.Visible)].Update(visible);
        map.Session.UndoRedo.SetName(transaction, visible ? "Show" : "Hide");
    }

    private static void RemoveLayer(OpenEditor map, int layer)
    {
        using var transaction = map.Session.UndoRedo.CreateTransaction();
        LayersNode(map).Remove(map.Session.MapAt(Location).Asset.Layers[layer], new NodeIndex(layer));
        map.Session.UndoRedo.SetName(transaction, "Remove");
    }

    private sealed class OpenEditor(TesseraSession session, TileMapEditorViewModel editor) : IAsyncDisposable
    {
        public TesseraSession Session { get; } = session;

        public TileMapEditorViewModel Editor { get; } = editor;

        public ValueTask DisposeAsync() => Session.DisposeAsync();
    }
}
