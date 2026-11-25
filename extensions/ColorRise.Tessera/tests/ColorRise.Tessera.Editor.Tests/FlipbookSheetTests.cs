using ColorRise.Tessera.Assets;
using ColorRise.Tessera.Editor.Canvas;
using ColorRise.Tessera.Editor.FlipbookEditor;
using Stride.Core.Assets.Editor.Testing;
using Stride.Core.Mathematics;
using static ColorRise.Tessera.Editor.Tests.TestTown;

namespace ColorRise.Tessera.Editor.Tests;

/// <summary>
/// Sheet in the flipbook editor: a block of the palette cut into one clip a row. TestTown's grid is four tiles wide; the block
/// of these tests is its three first columns over its two rows, or a taller image written by the test for more rows.
/// </summary>
public sealed class FlipbookSheetTests
{
    private const string Location = "Characters/Hero";
    private const string TownLocation = "Tiles/Town";

    [Fact]
    public async Task The_bar_opens_with_the_block_picked_and_the_name_of_the_selected_animation()
    {
        await using var book = await OpenAsync();
        var editor = book.Editor;
        Assert.True(editor.SheetCommand.IsEnabled);
        Assert.Null(editor.PendingSheet);

        // A pick of one tile is no block
        Click(editor, 3, 0);
        editor.SheetCommand.Execute();
        var sheet = editor.PendingSheet!;
        Assert.Null(sheet.Block);
        Assert.Equal((0, 0), (sheet.Columns, sheet.Rows));
        Assert.Empty(sheet.RowItems);
        Assert.Equal("walk", sheet.AnimationName);
        sheet.CancelCommand.Execute();

        editor.SelectedItem = editor.Animations[1];
        Drag(editor, (0, 0), (2, 1));
        Assert.Equal(["Add frames", "Add frames"], book.Session.Steps);
        editor.SheetCommand.Execute();

        sheet = editor.PendingSheet!;
        Assert.Same(editor.Palette!.Picked, sheet.Block);
        Assert.Equal((3, 2), (sheet.Columns, sheet.Rows));
        Assert.Equal([1, 2], sheet.RowItems.Select(row => row.Row));
        Assert.Equal([FlipbookDirection.Down, FlipbookDirection.Left], sheet.RowItems.Select(row => row.Direction));
        Assert.Equal("idle", sheet.AnimationName);
        Assert.Equal(SheetFrameOrder.InOrder, sheet.Order);
        Assert.Equal(0.15f, sheet.Duration);
        Assert.Null(sheet.Problem);
        Assert.Null(sheet.Note);
        Assert.True(sheet.ConfirmCommand.IsEnabled);
        Assert.Equal(["Add frames", "Add frames"], book.Session.Steps);
        sheet.CancelCommand.Execute();

        // Without a selected animation, the name is "walk"
        editor.SelectedItem = null;
        editor.SheetCommand.Execute();
        Assert.Equal("walk", editor.PendingSheet!.AnimationName);
    }

    [Fact]
    public async Task Sheet_is_off_without_a_palette()
    {
        await using var book = await OpenAsync(withTileSet: false);

        Assert.False(book.Editor.SheetCommand.IsEnabled);
    }

    [Fact]
    public async Task A_pick_while_the_bar_is_open_sets_the_block_and_adds_no_frame()
    {
        await using var book = await OpenAsync();
        var editor = book.Editor;
        editor.SheetCommand.Execute();
        var sheet = editor.PendingSheet!;
        Assert.Equal("Drag over the frames in the palette.", sheet.Problem);

        Drag(editor, (0, 0), (2, 1));

        Assert.Same(sheet, editor.PendingSheet);
        Assert.Equal((3, 2), (sheet.Columns, sheet.Rows));
        Assert.Null(sheet.Problem);
        Assert.Equal(3, editor.Frames.Count);
        Assert.Empty(book.Session.Steps);

        Click(editor, 3, 0);

        Assert.Equal((1, 1), (sheet.Columns, sheet.Rows));
        Assert.Equal([FlipbookDirection.None], sheet.RowItems.Select(row => row.Direction));
        Assert.Equal(3, editor.Frames.Count);
        Assert.Empty(book.Session.Steps);
    }

    [Fact]
    public async Task Four_rows_are_down_left_right_up_and_the_rows_after_are_skipped()
    {
        await using var book = await OpenAsync(height: 80);
        var editor = book.Editor;
        editor.SheetCommand.Execute();

        Drag(editor, (0, 0), (2, 4));

        var sheet = editor.PendingSheet!;
        Assert.Equal((3, 5), (sheet.Columns, sheet.Rows));
        Assert.Equal([1, 2, 3, 4, 5], sheet.RowItems.Select(row => row.Row));
        Assert.Equal(
            [FlipbookDirection.Down, FlipbookDirection.Left, FlipbookDirection.Right, FlipbookDirection.Up, null],
            sheet.RowItems.Select(row => row.Direction));
        Assert.Null(sheet.Problem);
    }

    [Fact]
    public async Task One_row_is_a_clip_for_any_direction()
    {
        await using var book = await OpenAsync();
        var editor = book.Editor;
        editor.SheetCommand.Execute();
        Drag(editor, (0, 0), (2, 0));
        var sheet = editor.PendingSheet!;
        Assert.Equal([FlipbookDirection.None], sheet.RowItems.Select(row => row.Direction));

        sheet.ConfirmCommand.Execute();

        var walk = Walk(book);
        Assert.Equal([FlipbookDirection.Down, FlipbookDirection.Left, FlipbookDirection.None], walk.Clips.Select(clip => clip.Direction));
        Assert.Equal(["1:0,0", "1:1,0", "1:2,0"], TilesOf(walk.Clips[2]));
        // The other clips stay
        Assert.Equal(["1:0,0", "1:1,0", "1:2,0"], TilesOf(walk.Clips[0]));
        Assert.Equal([0.1f, 0.1f, 0.1f], walk.Clips[0].Frames.Select(frame => frame.Duration));
    }

    [Fact]
    public async Task Make_writes_the_clips_in_one_step()
    {
        await using var book = await OpenAsync(height: 80);
        var editor = book.Editor;
        editor.SheetCommand.Execute();
        Drag(editor, (0, 0), (2, 3));

        editor.PendingSheet!.ConfirmCommand.Execute();

        Assert.Equal(["Sheet"], book.Session.Steps);
        var walk = Walk(book);
        Assert.Equal(
            [FlipbookDirection.Down, FlipbookDirection.Left, FlipbookDirection.Right, FlipbookDirection.Up],
            walk.Clips.Select(clip => clip.Direction));
        Assert.Equal(["1:0,0", "1:1,0", "1:2,0"], TilesOf(walk.Clips[0]));
        Assert.Equal(["1:0,1", "1:1,1", "1:2,1"], TilesOf(walk.Clips[1]));
        Assert.Equal(["1:0,2", "1:1,2", "1:2,2"], TilesOf(walk.Clips[2]));
        Assert.Equal(["1:0,3", "1:1,3", "1:2,3"], TilesOf(walk.Clips[3]));
        Assert.All(walk.Clips.SelectMany(clip => clip.Frames), frame =>
        {
            Assert.Equal(0.15f, frame.Duration);
            var cell = Assert.Single(frame.Cells);
            Assert.Equal((Int2.Zero, TileFlip.None), (cell.Offset, cell.Flip));
        });
        Assert.Null(editor.PendingSheet);
        Assert.Same(editor.Animations[0], editor.SelectedItem);
        Assert.Equal(["Down", "Left", "Right", "Up"], editor.Animations[0].Clips.Select(clip => clip.Label));

        // The palette adds frames again
        Click(editor, 3, 0);
        Assert.Equal(["Sheet", "Add frames"], book.Session.Steps);
    }

    [Fact]
    public async Task There_and_back_plays_1_2_3_2()
    {
        await using var book = await OpenAsync();
        var editor = book.Editor;
        editor.SheetCommand.Execute();
        // The tile 3,1 is transparent: the second row has two frames
        Drag(editor, (1, 0), (3, 1));
        var sheet = editor.PendingSheet!;
        sheet.Order = SheetFrameOrder.ThereAndBack;

        sheet.ConfirmCommand.Execute();

        var walk = Walk(book);
        Assert.Equal(["1:1,0", "1:2,0", "1:3,0", "1:2,0"], TilesOf(walk.Clips[0]));
        Assert.Equal(["1:1,1", "1:2,1"], TilesOf(walk.Clips[1]));
        // Each frame is one of its own
        Assert.NotSame(walk.Clips[0].Frames[1], walk.Clips[0].Frames[3]);
    }

    [Fact]
    public async Task Make_replaces_the_clips_of_its_directions_and_keeps_the_others()
    {
        await using var book = await OpenAsync(asset => asset.Animations[0].Clips.Add(Clip(FlipbookDirection.UpLeft, "1:3,0")));
        var editor = book.Editor;
        var upLeft = Walk(book).Clips[2];
        editor.SheetCommand.Execute();
        Drag(editor, (0, 0), (2, 1));
        var sheet = editor.PendingSheet!;
        Assert.Equal("Replaces the clips Down, Left of 'walk'.", sheet.Note);
        sheet.RowItems[1].Direction = null;
        Assert.Equal("Replaces the clip Down of 'walk'.", sheet.Note);
        sheet.RowItems[1].Direction = FlipbookDirection.Left;

        sheet.ConfirmCommand.Execute();

        var walk = Walk(book);
        Assert.Equal([FlipbookDirection.Down, FlipbookDirection.Left, FlipbookDirection.UpLeft], walk.Clips.Select(clip => clip.Direction));
        Assert.Equal(["1:0,0", "1:1,0", "1:2,0"], TilesOf(walk.Clips[0]));
        Assert.Equal([0.15f, 0.15f, 0.15f], walk.Clips[0].Frames.Select(frame => frame.Duration));
        Assert.Equal(["1:0,1", "1:1,1", "1:2,1"], TilesOf(walk.Clips[1]));
        Assert.Same(upLeft, walk.Clips[2]);
        Assert.Equal(["1:3,0"], TilesOf(upLeft));
    }

    [Fact]
    public async Task A_new_name_makes_a_new_animation_with_only_the_rows_clips()
    {
        await using var book = await OpenAsync();
        var editor = book.Editor;
        editor.SheetCommand.Execute();
        Drag(editor, (0, 0), (2, 1));
        var sheet = editor.PendingSheet!;
        sheet.AnimationName = " run ";
        Assert.Null(sheet.Note);

        sheet.ConfirmCommand.Execute();

        var asset = book.Session.FlipbookAt(Location).Asset;
        Assert.Equal(["walk", "idle", "run"], asset.Animations.Select(animation => animation.Name));
        var run = asset.Animations[2];
        Assert.Equal([FlipbookDirection.Down, FlipbookDirection.Left], run.Clips.Select(clip => clip.Direction));
        Assert.Equal(["1:0,1", "1:1,1", "1:2,1"], TilesOf(run.Clips[1]));
        Assert.Equal([0.1f, 0.1f, 0.1f], Walk(book).Clips[0].Frames.Select(frame => frame.Duration));
        Assert.Same(editor.Animations[2], editor.SelectedItem);
        Assert.Equal(["Sheet"], book.Session.Steps);
    }

    [Fact]
    public async Task A_hole_in_a_row_is_skipped_and_a_row_of_holes_makes_no_clip()
    {
        // The second row of the image is transparent, and so is the tile 1,2
        await using var book = await OpenAsync(height: 48, pixel: (x, y) => y / 16 == 1 || (x / 16, y / 16) == (1, 2) ? Color.Transparent : Flat(x / 16, y / 16));
        var editor = book.Editor;
        editor.SheetCommand.Execute();
        Drag(editor, (0, 0), (2, 2));
        var sheet = editor.PendingSheet!;
        Assert.Equal((3, 3), (sheet.Columns, sheet.Rows));
        sheet.AnimationName = "run";

        sheet.ConfirmCommand.Execute();

        var run = book.Session.FlipbookAt(Location).Asset.Animations[2];
        Assert.Equal([FlipbookDirection.Down, FlipbookDirection.Right], run.Clips.Select(clip => clip.Direction));
        Assert.Equal(["1:0,0", "1:1,0", "1:2,0"], TilesOf(run.Clips[0]));
        Assert.Equal(["1:0,2", "1:2,2"], TilesOf(run.Clips[1]));
    }

    [Theory]
    [InlineData("no block")]
    [InlineData("no name")]
    [InlineData("duration 0")]
    [InlineData("all skipped")]
    [InlineData("two rows of one direction")]
    public async Task What_cannot_be_made_is_said_and_Make_is_off(string what)
    {
        await using var book = await OpenAsync();
        var editor = book.Editor;
        editor.SheetCommand.Execute();
        var sheet = editor.PendingSheet!;
        if (what != "no block")
        {
            Drag(editor, (0, 0), (2, 1));
        }

        var expected = what switch
        {
            "no block" => "Drag over the frames in the palette.",
            "no name" => Set(() => sheet.AnimationName = "  ", "The animation needs a name."),
            "duration 0" => Set(() => sheet.Duration = 0, "The duration must be above 0."),
            "all skipped" => Set(() => sheet.RowItems.ToList().ForEach(row => row.Direction = null), "Every row is skipped."),
            _ => Set(() => sheet.RowItems[0].Direction = FlipbookDirection.Left, "Two rows have the direction Left."),
        };

        Assert.Equal(expected, sheet.Problem);
        Assert.False(sheet.ConfirmCommand.IsEnabled);
        sheet.ConfirmCommand.Execute();
        Assert.Same(sheet, editor.PendingSheet);
        Assert.Empty(book.Session.Steps);
        Assert.Equal(["walk", "idle"], book.Session.FlipbookAt(Location).Asset.Animations.Select(animation => animation.Name));
    }

    [Fact]
    public async Task Cancel_changes_nothing_and_the_palette_adds_frames_again()
    {
        await using var book = await OpenAsync();
        var editor = book.Editor;
        editor.SheetCommand.Execute();
        Drag(editor, (0, 0), (2, 1));

        editor.PendingSheet!.CancelCommand.Execute();

        Assert.Null(editor.PendingSheet);
        Assert.Empty(book.Session.Steps);
        Assert.Equal(["1:0,0", "1:1,0", "1:2,0"], TilesOf(Walk(book).Clips[0]));

        Click(editor, 3, 0);
        Assert.Equal(["1:0,0", "1:1,0", "1:2,0", "1:3,0"], TilesOf(Walk(book).Clips[0]));
        Assert.Equal(["Add frames"], book.Session.Steps);
    }

    [Fact]
    public async Task An_undo_takes_the_whole_sheet_back()
    {
        await using var book = await OpenAsync(height: 80);
        var editor = book.Editor;
        var down = Walk(book).Clips[0].Frames.ToList();
        editor.SheetCommand.Execute();
        Drag(editor, (0, 0), (2, 3));
        editor.PendingSheet!.ConfirmCommand.Execute();

        book.Session.UndoRedo.Undo();

        var walk = Walk(book);
        Assert.Equal([FlipbookDirection.Down, FlipbookDirection.Left], walk.Clips.Select(clip => clip.Direction));
        Assert.Equal(down, walk.Clips[0].Frames);
        Assert.Equal(["Down", "Left"], editor.Animations[0].Clips.Select(clip => clip.Label));

        book.Session.UndoRedo.Redo();
        Assert.Equal(["Down", "Left", "Right", "Up"], editor.Animations[0].Clips.Select(clip => clip.Label));
        Assert.Equal(["1:0,3", "1:1,3", "1:2,3"], TilesOf(Walk(book).Clips[3]));
    }

    // An image of the town 64 pixels wide, as high as asked: TestTown's pixels, or flat tiles below its two rows
    private static async DispatcherTask<OpenEditor> OpenAsync(Action<FlipbookAsset>? change = null, bool withTileSet = true, int height = 32, Func<int, int, Color>? pixel = null)
    {
        var tileSet = TileSet();
        var flipbook = Flipbook(withTileSet ? tileSet : null, TownLocation);
        change?.Invoke(flipbook);
        var session = await TesseraSession.OpenAsync([(TownLocation, tileSet), (Location, flipbook)]);
        session.WriteImage("Tiles/town.png", 64, height, pixel ?? ((x, y) => y < 32 ? Pixel(x, y) : Flat(x / 16, y / 16)));
        return new OpenEditor(session, await session.EditFlipbookAsync(Location));
    }

    private static string Set(Action change, string problem)
    {
        change();
        return problem;
    }

    private static FlipbookAnimation Walk(OpenEditor book) => book.Session.FlipbookAt(Location).Asset.Animations[0];

    private static List<string> TilesOf(FlipbookClip clip) => [.. clip.Frames.Select(frame => frame.Cells.Single().Tile.ToString())];

    // A click on a tile of the palette, which shows the image at its size
    private static void Click(FlipbookEditorViewModel editor, int column, int row)
    {
        var at = Middle(column, row);
        editor.Palette!.Scene.PointerPressed(at, CanvasButton.Left, CanvasModifiers.None);
        editor.Palette.Scene.PointerReleased(at, CanvasButton.Left, CanvasModifiers.None);
    }

    // A drag in the palette from a tile to another
    private static void Drag(FlipbookEditorViewModel editor, (int Column, int Row) from, (int Column, int Row) to)
    {
        var scene = editor.Palette!.Scene;
        scene.PointerPressed(Middle(from.Column, from.Row), CanvasButton.Left, CanvasModifiers.None);
        scene.PointerMoved(Middle(to.Column, to.Row), CanvasModifiers.None);
        scene.PointerReleased(Middle(to.Column, to.Row), CanvasButton.Left, CanvasModifiers.None);
    }

    private static Vector2 Middle(int column, int row) => new(column * 16 + 8, row * 16 + 8);

    private sealed class OpenEditor(TesseraSession session, FlipbookEditorViewModel editor) : IAsyncDisposable
    {
        public TesseraSession Session { get; } = session;

        public FlipbookEditorViewModel Editor { get; } = editor;

        public ValueTask DisposeAsync() => Session.DisposeAsync();
    }
}
