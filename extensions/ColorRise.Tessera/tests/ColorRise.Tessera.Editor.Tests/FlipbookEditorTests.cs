using ColorRise.Tessera.Assets;
using ColorRise.Tessera.Editor.Canvas;
using ColorRise.Tessera.Editor.FlipbookEditor;
using Stride.Core.Assets;
using Stride.Core.Assets.Editor.Testing;
using Stride.Core.Mathematics;
using Stride.Core.Presentation.Quantum.ViewModels;
using Stride.Core.Presentation.Services;
using Stride.Core.Quantum;
using Stride.Core.Serialization;
using static ColorRise.Tessera.Editor.Tests.TestTown;

namespace ColorRise.Tessera.Editor.Tests;

/// <summary>
/// The flipbook editor without its pictures: the tree of animations and clips, the strip of frames, the palette that adds
/// frames, the property grid, and how they follow the asset.
/// </summary>
public sealed class FlipbookEditorTests
{
    private const string Location = "Characters/Hero";
    private const string TownLocation = "Tiles/Town";

    [Fact]
    public async Task The_editor_opens_with_the_tree_of_the_asset_and_the_first_clip_shown()
    {
        await using var book = await OpenAsync();
        var editor = book.Editor;

        Assert.Equal(["walk", "idle"], editor.Animations.Select(animation => animation.Name));
        Assert.Equal(["Down", "Left"], editor.Animations[0].Clips.Select(clip => clip.Label));
        Assert.Equal(["Any direction"], editor.Animations[1].Clips.Select(clip => clip.Label));
        Assert.Equal([3, 2], editor.Animations[0].Clips.Select(clip => clip.FrameCount));
        Assert.Same(editor.Animations[0].Clips[0], editor.SelectedItem);
        Assert.Same(editor.Animations[0], editor.SelectedAnimation);
        Assert.Same(editor.Animations[0].Clips[0], editor.SelectedClip);
        Assert.Equal(["1", "2", "3"], editor.Frames.Select(frame => frame.Label));
        Assert.Equal(Tiles("1:0,0", "1:1,0", "1:2,0"), editor.Frames.Select(frame => frame.Tile));
        Assert.Equal([0.1f, 0.1f, 0.1f], editor.Frames.Select(frame => frame.Duration));
        Assert.Empty(editor.SelectedFrames);
        Assert.NotNull(editor.Pictures);
        Assert.NotNull(editor.Palette);
        Assert.Empty(editor.Problems);
        Assert.Empty(book.Session.Steps);
    }

    [Fact]
    public async Task A_flipbook_without_a_tile_set_has_no_palette_until_one_is_picked()
    {
        await using var book = await OpenAsync(withTileSet: false);
        var editor = book.Editor;
        Assert.Null(editor.Pictures);
        Assert.Null(editor.Palette);
        Assert.Equal(["The flipbook has no tile set: it draws nothing."], editor.Problems);

        book.Session.Dialogs.PickAsset = _ => book.Session.TileSetAt(TownLocation);
        editor.PickTileSetCommand.Execute();

        Assert.NotNull(editor.Palette);
        Assert.Same(book.Session.TileSetAt(TownLocation), editor.Pictures!.TileSet);
        Assert.Equal(["Set tile set"], book.Session.Steps);
        Assert.Empty(editor.Problems);

        book.Session.UndoRedo.Undo();
        Assert.Null(editor.Pictures);
        Assert.Null(editor.Palette);
    }

    [Fact]
    public async Task Add_animation_gives_a_free_name_and_one_clip_for_any_direction()
    {
        await using var book = await OpenAsync();
        var editor = book.Editor;

        editor.AddAnimationCommand.Execute();

        Assert.Equal(["walk", "idle", "animation"], Names(book));
        var added = editor.Animations[2];
        Assert.Same(added, editor.SelectedItem);
        Assert.Same(added, editor.SelectedAnimation);
        Assert.Equal([FlipbookDirection.None], added.Clips.Select(clip => clip.Direction));
        Assert.Same(added.Clips[0], editor.SelectedClip);
        Assert.Empty(editor.Frames);

        editor.AddAnimationCommand.Execute();
        Assert.Equal(["walk", "idle", "animation", "animation 2"], Names(book));
        Assert.Equal(["Add animation", "Add animation"], book.Session.Steps);
    }

    [Fact]
    public async Task Add_direction_offers_only_the_directions_the_animation_lacks()
    {
        await using var book = await OpenAsync();
        var editor = book.Editor;

        Assert.Equal(
            [FlipbookDirection.None, FlipbookDirection.Right, FlipbookDirection.Up, FlipbookDirection.DownLeft, FlipbookDirection.DownRight, FlipbookDirection.UpLeft, FlipbookDirection.UpRight],
            editor.FreeDirections);
        Assert.False(editor.AddClipCommand.CanExecute(FlipbookDirection.Down));
        Assert.True(editor.AddClipCommand.CanExecute(FlipbookDirection.Up));

        editor.AddClipCommand.Execute(FlipbookDirection.Up);

        Assert.Equal(["Down", "Left", "Up"], editor.Animations[0].Clips.Select(clip => clip.Label));
        Assert.Same(editor.Animations[0].Clips[2], editor.SelectedItem);
        Assert.DoesNotContain(FlipbookDirection.Up, editor.FreeDirections);
        Assert.Equal(["Add clip"], book.Session.Steps);

        // A direction the animation has adds nothing
        editor.AddClipCommand.Execute(FlipbookDirection.Down);
        Assert.Equal(3, editor.Animations[0].Clips.Count);
        Assert.Single(book.Session.Steps);
    }

    [Fact]
    public async Task Selecting_an_animation_shows_its_first_clip()
    {
        await using var book = await OpenAsync();
        var editor = book.Editor;

        editor.SelectedItem = editor.Animations[1];

        Assert.Same(editor.Animations[1], editor.SelectedAnimation);
        Assert.Same(editor.Animations[1].Clips[0], editor.SelectedClip);
        Assert.Equal(Tiles("1:3,0"), editor.Frames.Select(frame => frame.Tile));
        Assert.Equal(Enum.GetValues<FlipbookDirection>().Where(direction => direction != FlipbookDirection.None), editor.FreeDirections);

        editor.SelectedItem = editor.Animations[0].Clips[1];
        Assert.Same(editor.Animations[0], editor.SelectedAnimation);
        Assert.Equal(Tiles("1:0,1", "1:1,1"), editor.Frames.Select(frame => frame.Tile));
    }

    // Studio check, step 3, line 5: the editor's texts said "None" where its tree says "Any direction"
    [Fact]
    public async Task The_question_names_the_clip_as_the_tree_does()
    {
        await using var book = await OpenAsync();
        var editor = book.Editor;
        editor.SelectedItem = editor.Animations[1].Clips[0];
        Assert.Equal("Any direction", editor.Animations[1].Clips[0].Label);

        book.Session.Dialogs.Answers.Enqueue(MessageBoxResult.No);
        editor.RemoveSelectedCommand.Execute();

        Assert.Equal("Remove the clip Any direction of 'idle' and its 1 frame?", Assert.Single(book.Session.Dialogs.Messages));
    }

    [Fact]
    public async Task Removing_a_clip_or_an_animation_with_frames_asks_first()
    {
        await using var book = await OpenAsync();
        var editor = book.Editor;
        editor.SelectedItem = editor.Animations[0].Clips[1];

        book.Session.Dialogs.Answers.Enqueue(MessageBoxResult.No);
        editor.RemoveSelectedCommand.Execute();
        Assert.Equal("Remove the clip Left of 'walk' and its 2 frames?", Assert.Single(book.Session.Dialogs.Messages));
        Assert.Equal(2, editor.Animations[0].Clips.Count);
        Assert.Empty(book.Session.Steps);

        book.Session.Dialogs.Answers.Enqueue(MessageBoxResult.Yes);
        editor.RemoveSelectedCommand.Execute();
        Assert.Equal(["Down"], editor.Animations[0].Clips.Select(clip => clip.Label));
        Assert.Same(editor.Animations[0].Clips[0], editor.SelectedItem);
        Assert.Equal(["Remove clip"], book.Session.Steps);

        editor.SelectedItem = editor.Animations[0];
        book.Session.Dialogs.Answers.Enqueue(MessageBoxResult.Yes);
        editor.RemoveSelectedCommand.Execute();
        Assert.Equal("Remove the animation 'walk' and its 3 frames?", book.Session.Dialogs.Messages[2]);
        Assert.Equal(["idle"], Names(book));
        Assert.Same(editor.Animations[0], editor.SelectedItem);
        Assert.Equal(["Remove clip", "Remove animation"], book.Session.Steps);
    }

    [Fact]
    public async Task An_empty_clip_is_removed_without_a_question()
    {
        await using var book = await OpenAsync();
        var editor = book.Editor;
        editor.AddClipCommand.Execute(FlipbookDirection.Up);

        editor.RemoveSelectedCommand.Execute();

        Assert.Empty(book.Session.Dialogs.Messages);
        Assert.Equal(["Down", "Left"], editor.Animations[0].Clips.Select(clip => clip.Label));
        Assert.Same(editor.Animations[0].Clips[1], editor.SelectedItem);
        Assert.Equal(["Add clip", "Remove clip"], book.Session.Steps);
    }

    // Review Focus 5
    [Fact]
    public async Task A_rename_is_one_step_and_an_empty_or_taken_name_is_refused_with_a_message()
    {
        await using var book = await OpenAsync();
        var editor = book.Editor;
        var walk = editor.Animations[0];

        Assert.True(editor.Rename(walk, " run "));
        Assert.Equal("run", walk.Name);
        Assert.Equal(["run", "idle"], Names(book));
        Assert.Equal(["Rename animation"], book.Session.Steps);
        Assert.Null(editor.Message);

        Assert.False(editor.Rename(walk, "  "));
        Assert.Equal("An animation needs a name.", editor.Message);
        Assert.False(editor.Rename(walk, "idle"));
        Assert.Equal("Another animation is named 'idle'.", editor.Message);
        Assert.Equal("run", walk.Name);
        Assert.True(editor.Rename(walk, "run"));
        Assert.Single(book.Session.Steps);

        // A name typed in the tree asks the editor; a refused one goes back
        walk.Name = "stroll";
        Assert.Equal(["stroll", "idle"], Names(book));
        walk.Name = "idle";
        Assert.Equal("stroll", walk.Name);
        Assert.Equal(["Rename animation", "Rename animation"], book.Session.Steps);
    }

    // Review Focus 5
    [Fact]
    public async Task Two_clips_of_one_direction_made_in_the_grid_are_named_in_the_problems()
    {
        await using var book = await OpenAsync();
        var editor = book.Editor;
        editor.SelectedItem = editor.Animations[0].Clips[1];
        await GridAsync(editor, editor.Animations[0].Clips[1], [nameof(FlipbookClip.Direction)]);

        Child(editor, nameof(FlipbookClip.Direction)).NodeValue = FlipbookDirection.Down;

        await ExclusiveDispatcherService.WaitUntilAsync(
            () => editor.Problems.Contains("The animation 'walk' has 2 clips for Down: the first is played."), "the line of the two clips");
        Assert.Equal(["Down", "Down"], editor.Animations[0].Clips.Select(clip => clip.Label));
        Assert.Same(editor.Animations[0].Clips[1], editor.SelectedItem);
    }

    [Fact]
    public async Task A_click_in_the_palette_adds_a_frame_after_the_selected_frames()
    {
        await using var book = await OpenAsync();
        var editor = book.Editor;
        editor.SelectedFrames = [editor.Frames[0]];

        Click(editor.Palette!.Scene, TileMiddle(3, 0));

        Assert.Equal([0, 3, 1, 2], editor.Frames.Select(frame => frame.Tile!.Value.Column));
        Assert.Equal([editor.Frames[1]], editor.SelectedFrames);
        var cell = Assert.Single(Clip(book, 0, 0).Frames[1].Cells);
        Assert.Equal((TileId.Parse("1:3,0"), Int2.Zero, TileFlip.None), (cell.Tile, cell.Offset, cell.Flip));
        Assert.Equal(["Add frames"], book.Session.Steps);
        Assert.Null(editor.Message);
    }

    [Fact]
    public async Task A_drag_in_the_palette_adds_its_tiles_row_by_row_from_the_top()
    {
        await using var book = await OpenAsync();
        var editor = book.Editor;

        Drag(editor.Palette!.Scene, TileMiddle(0, 0), TileMiddle(2, 1));

        Assert.Equal(
            Tiles("1:0,0", "1:1,0", "1:2,0", "1:0,0", "1:1,0", "1:2,0", "1:0,1", "1:1,1", "1:2,1"),
            editor.Frames.Select(frame => frame.Tile));
        Assert.Equal(editor.Frames.Skip(3), editor.SelectedFrames);
        Assert.Equal(["Add frames"], book.Session.Steps);
    }

    [Fact]
    public async Task A_new_frame_lasts_as_the_frame_before_it()
    {
        await using var book = await OpenAsync();
        var editor = book.Editor;
        editor.AddAnimationCommand.Execute();

        Click(editor.Palette!.Scene, TileMiddle(0, 0));
        Assert.Equal([0.1f], editor.Frames.Select(frame => frame.Duration));

        SetDuration(book, editor.SelectedFrames[0].Frame, 0.3f);
        Click(editor.Palette.Scene, TileMiddle(1, 0));

        Assert.Equal([0.3f, 0.3f], editor.Frames.Select(frame => frame.Duration));
    }

    [Fact]
    public async Task A_pick_without_a_clip_adds_nothing_and_says_why()
    {
        await using var book = await OpenAsync(asset => asset.Animations.Clear());
        var editor = book.Editor;
        Assert.Null(editor.SelectedItem);
        Assert.Null(editor.SelectedClip);

        Click(editor.Palette!.Scene, TileMiddle(0, 0));

        Assert.Equal("Select or add an animation first.", editor.Message);
        Assert.Empty(book.Session.Steps);
    }

    [Fact]
    public async Task Frames_are_removed_and_moved_as_a_block_in_one_step()
    {
        await using var book = await OpenAsync();
        var editor = book.Editor;
        Assert.False(editor.RemoveFramesCommand.IsEnabled);
        editor.SelectedFrames = [editor.Frames[0], editor.Frames[1]];
        Assert.True(editor.RemoveFramesCommand.IsEnabled);
        Assert.False(editor.MoveFramesLeftCommand.IsEnabled);
        Assert.True(editor.MoveFramesRightCommand.IsEnabled);

        editor.MoveFramesRightCommand.Execute();
        Assert.Equal([2, 0, 1], Columns(editor));
        Assert.Equal(Tiles("1:0,0", "1:1,0"), editor.SelectedFrames.Select(frame => frame.Tile));
        Assert.True(editor.MoveFramesLeftCommand.IsEnabled);
        Assert.False(editor.MoveFramesRightCommand.IsEnabled);

        // What a drag in the strip does: the block goes before the frame that is at the place now
        editor.MoveSelectedFrames(0);
        Assert.Equal([0, 1, 2], Columns(editor));
        Assert.Equal(Tiles("1:0,0", "1:1,0"), editor.SelectedFrames.Select(frame => frame.Tile));

        editor.RemoveFramesCommand.Execute();
        Assert.Equal([2], Columns(editor));
        Assert.Empty(editor.SelectedFrames);
        Assert.False(editor.RemoveFramesCommand.IsEnabled);
        Assert.Equal(["Move frames", "Move frames", "Remove frames"], book.Session.Steps);
    }

    [Fact]
    public async Task The_grid_shows_the_frames_then_the_clip_or_the_animation()
    {
        await using var book = await OpenAsync();
        var editor = book.Editor;
        await GridAsync(editor, editor.Animations[0].Clips[0], [nameof(FlipbookClip.Direction)]);

        editor.SelectedFrames = [editor.Frames[1]];
        await GridAsync(editor, editor.Frames[1], [nameof(FlipbookFrame.Duration), nameof(FlipbookFrame.Cells)]);
        var cells = Child(editor, nameof(FlipbookFrame.Cells));
        Assert.Equal([nameof(FlipbookCell.Offset), nameof(FlipbookCell.Flip)], cells.Children.Single().Children.Select(child => child.Name));

        editor.SelectedItem = editor.Animations[1];
        Assert.Empty(editor.SelectedFrames);
        await GridAsync(editor, editor.Animations[1], [nameof(FlipbookAnimation.LoopMode)]);

        editor.SelectedItem = null;
        await ExclusiveDispatcherService.WaitUntilAsync(() => editor.EditorProperties.ViewModel is null, "an empty property grid");
    }

    [Fact]
    public async Task A_duration_set_in_the_grid_shows_in_the_strip()
    {
        await using var book = await OpenAsync();
        var editor = book.Editor;
        var frame = editor.Frames[1];
        editor.SelectedFrames = [frame];
        await GridAsync(editor, frame, [nameof(FlipbookFrame.Duration), nameof(FlipbookFrame.Cells)]);

        Child(editor, nameof(FlipbookFrame.Duration)).NodeValue = 0.4f;

        await ExclusiveDispatcherService.WaitUntilAsync(() => editor.Frames[1].Duration == 0.4f, "the duration in the strip");
        Assert.Equal(0.4f, Clip(book, 0, 0).Frames[1].Duration);
        Assert.Same(frame, editor.Frames[1]);
        Assert.Equal([frame], editor.SelectedFrames);
    }

    // Review Focus 1
    [Fact]
    public async Task A_frame_whose_tile_is_gone_is_marked_named_and_kept()
    {
        await using var book = await OpenAsync(asset => asset.Animations[0].Clips[1].Frames[1].Cells[0].Tile = TileId.Parse("1:9,9"));
        var editor = book.Editor;

        editor.SelectedItem = editor.Animations[0].Clips[1];
        Assert.Equal([false, true], editor.Frames.Select(frame => frame.HasMissing));
        Assert.Equal(["The frame 2 of the clip Left of 'walk' uses the tile 1:9,9, which the tile set does not have."], editor.Problems);

        // Another change, then a save: the frame keeps its cell
        Assert.True(editor.Rename(editor.Animations[1], "rest"));
        Assert.True(await book.Session.Session.SaveSessionAsync());
        var saved = AssetFileSerializer.Load<FlipbookAsset>(book.Session.FileOf(Location, FlipbookAsset.FileExtension)).Asset;
        Assert.Equal(TileId.Parse("1:9,9"), saved.Animations[0].Clips[1].Frames[1].Cells.Single().Tile);
        Assert.Equal("rest", saved.Animations[1].Name);
    }

    // Review Focus 1
    [Fact]
    public async Task A_tile_set_that_cannot_be_found_is_named_and_the_editor_opens()
    {
        await using var book = await OpenAsync(asset => asset.TileSet = AttachedReferenceManager.CreateProxyObject<TileSet>(AssetId.New(), "Tiles/Gone"));
        var editor = book.Editor;

        Assert.Null(editor.Pictures);
        Assert.Null(editor.Palette);
        Assert.Equal(["The tile set of the flipbook cannot be found."], editor.Problems);
        Assert.Equal(["walk", "idle"], editor.Animations.Select(animation => animation.Name));
        Assert.Equal(3, editor.Frames.Count);
    }

    // Review Focus 3
    [Fact]
    public async Task An_undo_that_removes_the_selected_frames_empties_the_selection()
    {
        await using var book = await OpenAsync();
        var editor = book.Editor;
        Click(editor.Palette!.Scene, TileMiddle(3, 0));
        Assert.Equal(Tiles("1:3,0"), editor.SelectedFrames.Select(frame => frame.Tile));

        book.Session.UndoRedo.Undo();

        Assert.Equal(3, editor.Frames.Count);
        Assert.Empty(editor.SelectedFrames);
        Assert.False(editor.RemoveFramesCommand.IsEnabled);
    }

    // Review Focus 3
    [Fact]
    public async Task An_undo_that_removes_the_selected_animation_selects_the_first_clip_that_is_left()
    {
        await using var book = await OpenAsync();
        var editor = book.Editor;
        editor.AddAnimationCommand.Execute();

        book.Session.UndoRedo.Undo();

        Assert.Equal(["walk", "idle"], editor.Animations.Select(animation => animation.Name));
        Assert.Same(editor.Animations[0].Clips[0], editor.SelectedItem);
        Assert.Same(editor.Animations[0].Clips[0], editor.SelectedClip);
        Assert.Equal(3, editor.Frames.Count);
    }

    [Fact]
    public async Task The_tree_follows_a_change_made_outside_the_editor()
    {
        await using var book = await OpenAsync();
        var editor = book.Editor;
        var flipbook = book.Session.FlipbookAt(Location);
        editor.SelectedItem = editor.Animations[0].Clips[1];
        var left = editor.SelectedItem;

        using (var transaction = book.Session.UndoRedo.CreateTransaction())
        {
            FlipbookGraph.AddAnimation(flipbook, "jump");
            FlipbookGraph.RenameAnimation(flipbook, flipbook.Asset.Animations[0], "stroll");
            FlipbookGraph.AddFrames(flipbook, flipbook.Asset.Animations[0].Clips[1], 0, [Frame("1:3,1")]);
            book.Session.UndoRedo.SetName(transaction, "Elsewhere");
        }

        Assert.Equal(["stroll", "idle", "jump"], editor.Animations.Select(animation => animation.Name));
        Assert.Equal(["Any direction"], editor.Animations[2].Clips.Select(clip => clip.Label));
        Assert.Same(left, editor.SelectedItem);
        Assert.Equal(3, editor.Animations[0].Clips[1].FrameCount);
        Assert.Equal(Tiles("1:3,1", "1:0,1", "1:1,1"), editor.Frames.Select(frame => frame.Tile));
        Assert.Equal(["1", "2", "3"], editor.Frames.Select(frame => frame.Label));
    }

    private static async DispatcherTask<OpenEditor> OpenAsync(Action<FlipbookAsset>? change = null, bool withTileSet = true)
    {
        var tileSet = TileSet();
        var flipbook = Flipbook(withTileSet ? tileSet : null, TownLocation);
        change?.Invoke(flipbook);
        var session = await TesseraSession.OpenAsync([(TownLocation, tileSet), (Location, flipbook)]);
        session.WriteImage("Tiles/town.png", 64, 32, Pixel);
        return new OpenEditor(session, await session.EditFlipbookAsync(Location));
    }

    private static List<string> Names(OpenEditor book) => [.. book.Session.FlipbookAt(Location).Asset.Animations.Select(animation => animation.Name)];

    private static FlipbookClip Clip(OpenEditor book, int animation, int clip) => book.Session.FlipbookAt(Location).Asset.Animations[animation].Clips[clip];

    private static List<TileId?> Tiles(params string[] tiles) => [.. tiles.Select(tile => (TileId?)TileId.Parse(tile))];

    private static List<int> Columns(FlipbookEditorViewModel editor) => [.. editor.Frames.Select(frame => frame.Tile!.Value.Column)];

    // The middle of a tile of the town's source in the palette, which shows the image at its size
    private static Vector2 TileMiddle(int column, int row) => new(column * 16 + 8, row * 16 + 8);

    private static void Click(ICanvasInput input, Vector2 at)
    {
        input.PointerPressed(at, CanvasButton.Left, CanvasModifiers.None);
        input.PointerReleased(at, CanvasButton.Left, CanvasModifiers.None);
    }

    private static void Drag(ICanvasInput input, Vector2 from, Vector2 to)
    {
        input.PointerPressed(from, CanvasButton.Left, CanvasModifiers.None);
        input.PointerMoved(to, CanvasModifiers.None);
        input.PointerReleased(to, CanvasButton.Left, CanvasModifiers.None);
    }

    private static void SetDuration(OpenEditor book, FlipbookFrame frame, float duration)
    {
        using var transaction = book.Session.UndoRedo.CreateTransaction();
        book.Session.Session.AssetNodeContainer.GetNode(frame)![nameof(FlipbookFrame.Duration)].Update(duration);
        book.Session.UndoRedo.SetName(transaction, "Duration");
    }

    // Waits for the grid to show one thing with the given members
    private static Task GridAsync(FlipbookEditorViewModel editor, object shown, string[] members)
        => ExclusiveDispatcherService.WaitUntilAsync(
            () => editor.EditorProperties.Selection?.SequenceEqual([shown]) == true
                  && editor.EditorProperties.ViewModel?.RootNode.Children.Select(child => child.Name).SequenceEqual(members) == true,
            $"the grid to show {string.Join(", ", members)}");

    private static NodeViewModel Child(FlipbookEditorViewModel editor, string member)
        => editor.EditorProperties.ViewModel!.RootNode.Children.Single(child => child.Name == member);

    private sealed class OpenEditor(TesseraSession session, FlipbookEditorViewModel editor) : IAsyncDisposable
    {
        public TesseraSession Session { get; } = session;

        public FlipbookEditorViewModel Editor { get; } = editor;

        public ValueTask DisposeAsync() => Session.DisposeAsync();
    }
}
