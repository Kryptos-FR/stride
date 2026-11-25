using ColorRise.Tessera.Assets;
using ColorRise.Tessera.Editor.Canvas;
using ColorRise.Tessera.Editor.FlipbookEditor;
using Stride.Core.Assets.Editor.Testing;
using Stride.Core.IO;
using Stride.Core.Mathematics;
using Stride.Core.Presentation.Services;
using Stride.Core.Serialization;
using static ColorRise.Tessera.Editor.Tests.TestTown;

namespace ColorRise.Tessera.Editor.Tests;

/// <summary>
/// The pictures of the frames in the flipbook editor, and the preview that plays the selected clip around the pivot.
/// </summary>
public sealed class FlipbookPreviewTests
{
    private const string Location = "Characters/Hero";
    private const string TownLocation = "Tiles/Town";
    private static readonly RectangleF PivotCross = new(-3, -3, 6, 6);

    [Fact]
    public async Task The_preview_shows_the_frame_of_its_time_around_the_pivot()
    {
        await using var book = await OpenAsync();
        var preview = book.Editor.Preview;
        Assert.True(book.Editor.IsPlaying);
        Assert.True(preview.IsRunning);
        Assert.Equal(Vector2.Zero, preview.GridSize);

        var frame = Collect(preview);

        var sprite = Assert.Single(frame.Sprites);
        Assert.Equal(new RectangleF(-8, -16, 16, 16), sprite.Destination);
        Assert.Equal(new Rectangle(0, 0, 16, 16), sprite.Source);
        Assert.Equal(Color.Red, sprite.Picture[2, 2]);
        Assert.Equal(Color.Yellow, sprite.Picture[13, 13]);
        var cross = Assert.Single(frame.Shapes);
        Assert.Equal((CanvasMark.Cross, PivotCross), (cross.Mark, cross.Bounds));
        Assert.Equal(0, preview.FrameIndex);
    }

    [Fact]
    public async Task The_bounds_cover_every_frame_of_the_clip()
    {
        // The second frame of Down has a second cell to the right and up
        await using var book = await OpenAsync(asset => asset.Animations[0].Clips[0].Frames[1].Cells.Add(new FlipbookCell { Tile = TileId.Parse("1:3,0"), Offset = new Int2(16, 8) }));

        Assert.Equal(new RectangleF(-8, -24, 32, 27), book.Editor.Preview.Bounds);

        book.Editor.SelectedItem = book.Editor.Animations[1];
        Assert.Equal(new RectangleF(-8, -16, 16, 19), book.Editor.Preview.Bounds);
    }

    [Fact]
    public async Task Advance_moves_the_frame_at_the_speed_and_asks_a_redraw_only_then()
    {
        await using var book = await OpenAsync();
        var (editor, preview) = (book.Editor, book.Editor.Preview);
        Assert.Equal([0.25f, 0.5f, 1, 2], editor.PreviewSpeeds);
        Assert.Equal(1, editor.PreviewSpeed);
        editor.PreviewSpeed = 2;
        var (changes, frames) = (0, 0);
        preview.Changed += (_, _) => changes++;
        preview.FrameIndexChanged += (_, _) => frames++;

        preview.Advance(0.03);
        Assert.Equal((0, 0, 0), (preview.FrameIndex, changes, frames));

        preview.Advance(0.03);
        Assert.Equal((1, 1, 1), (preview.FrameIndex, changes, frames));
        Assert.Equal(0.12, preview.Time, 6);
    }

    [Fact]
    public async Task A_once_animation_stops_on_its_last_frame_and_loop_starts_it_again()
    {
        await using var book = await OpenAsync(asset => asset.Animations[0].LoopMode = FlipbookLoopMode.Once);
        var (editor, preview) = (book.Editor, book.Editor.Preview);
        Assert.Equal(FlipbookLoopMode.Once, preview.LoopMode);

        preview.Advance(0.35);
        Assert.Equal(2, preview.FrameIndex);
        Assert.False(editor.IsPlaying);
        Assert.False(preview.IsRunning);
        preview.Advance(0.1);
        Assert.Equal(2, preview.FrameIndex);

        // Play again starts at the beginning
        editor.PlayPauseCommand.Execute();
        Assert.True(editor.IsPlaying);
        Assert.Equal(0, preview.FrameIndex);

        // Loop plays it again at its end
        editor.LoopPreview = true;
        preview.Advance(0.35);
        Assert.True(editor.IsPlaying);
        Assert.Equal(0, preview.FrameIndex);
    }

    [Fact]
    public async Task A_ping_pong_plays_there_and_back()
    {
        await using var book = await OpenAsync(asset => asset.Animations[0].LoopMode = FlipbookLoopMode.PingPong);
        var preview = book.Editor.Preview;
        var shown = new List<int> { preview.FrameIndex };

        for (var step = 0; step < 4; step++)
        {
            preview.Advance(0.1);
            shown.Add(preview.FrameIndex);
        }

        Assert.Equal([0, 1, 2, 1, 0], shown);
    }

    [Fact]
    public async Task One_selected_frame_pauses_the_preview_on_it()
    {
        await using var book = await OpenAsync();
        var (editor, preview) = (book.Editor, book.Editor.Preview);

        editor.SelectedFrames = [editor.Frames[2]];

        Assert.False(editor.IsPlaying);
        Assert.Equal(2, preview.FrameIndex);
        preview.Advance(1);
        Assert.Equal(2, preview.FrameIndex);

        // Two frames selected leave the preview as it is
        editor.IsPlaying = true;
        editor.SelectedFrames = [editor.Frames[0], editor.Frames[1]];
        Assert.True(editor.IsPlaying);
    }

    [Fact]
    public async Task The_strip_marks_the_frame_the_preview_shows()
    {
        await using var book = await OpenAsync();
        var editor = book.Editor;
        Assert.Equal([true, false, false], editor.Frames.Select(frame => frame.IsCurrent));

        editor.Preview.Advance(0.15);

        Assert.Equal([false, true, false], editor.Frames.Select(frame => frame.IsCurrent));
    }

    [Fact]
    public async Task The_frames_have_their_pictures()
    {
        await using var book = await OpenAsync();
        var frames = book.Editor.Frames;

        Assert.Equal(Color.Red, frames[0].Picture![2, 2]);
        Assert.Equal(Flat(1, 0), frames[1].Picture![8, 8]);
        Assert.Equal(Flat(2, 0), frames[2].Picture![8, 8]);
        // The pictures are made once, and kept while nothing changes
        Assert.Same(frames[1].Picture, Collect(book.Editor.Preview, at: 0.15).Sprites.Single().Picture);
    }

    [Fact]
    public async Task A_new_image_of_the_tile_set_makes_the_pictures_again()
    {
        await using var book = await OpenAsync();
        var before = book.Editor.Frames[1].Picture;

        var file = book.Session.WriteImage("Tiles/town.png", 64, 32, (_, _) => Color.White);
        // The image cache reads a file again when its time changed
        File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddMinutes(1));
        book.Editor.Pictures!.Refresh();

        Assert.NotSame(before, book.Editor.Frames[1].Picture);
        Assert.Equal(Color.White, book.Editor.Frames[1].Picture![8, 8]);
        Assert.Equal(Color.White, Collect(book.Editor.Preview, at: 0.15).Sprites.Single().Picture[8, 8]);
    }

    // Review Focus 3
    [Fact]
    public async Task An_undo_that_removes_the_frame_shown_shows_the_first_that_is_left()
    {
        await using var book = await OpenAsync();
        var (editor, preview) = (book.Editor, book.Editor.Preview);
        Click(editor, 3, 0);
        editor.SelectedFrames = [];
        editor.IsPlaying = true;
        preview.Time = 0.35;
        Assert.Equal(3, preview.FrameIndex);

        book.Session.UndoRedo.Undo();

        Assert.Equal(3, editor.Frames.Count);
        Assert.Contains(preview.FrameIndex, (int[])[0, 2]);
        Assert.Single(Collect(preview).Sprites);
        Assert.True(editor.IsPlaying);
    }

    // Review Focus 3
    [Fact]
    public async Task A_removed_animation_under_the_preview_shows_the_next_clip_or_nothing()
    {
        await using var book = await OpenAsync();
        var (editor, preview) = (book.Editor, book.Editor.Preview);
        editor.SelectedItem = editor.Animations[1];
        Assert.Same(Clip(book, 1, 0), preview.Clip);

        book.Session.Dialogs.Answers.Enqueue(MessageBoxResult.Yes);
        editor.RemoveSelectedCommand.Execute();
        Assert.Same(Clip(book, 0, 0), preview.Clip);
        Assert.Equal(FlipbookLoopMode.Loop, preview.LoopMode);
        Assert.Single(Collect(preview).Sprites);

        book.Session.Dialogs.Answers.Enqueue(MessageBoxResult.Yes);
        editor.RemoveSelectedCommand.Execute();
        Assert.Null(preview.Clip);
        Assert.Equal(-1, preview.FrameIndex);
        Assert.False(preview.IsRunning);
        var frame = Collect(preview);
        Assert.Empty(frame.Sprites);
        Assert.Equal(PivotCross, Assert.Single(frame.Shapes).Bounds);
    }

    // Review Focus 4
    [Fact]
    public async Task The_preview_shows_a_flipped_tall_frame_as_the_game_draws_it()
    {
        // A tile of 16 by 24 pixels whose bottom-right pixel is red, the cell flipped left to right, the pivot (0.5, 0)
        var tileSet = new TileSetAsset { TileSize = new Int2(16, 24) };
        tileSet.AddSource<GridTileSetSource>("Tall").Image = new UFile("tall.png");
        var flipbook = new FlipbookAsset { TileSet = AttachedReferenceManager.CreateProxyObject<TileSet>(tileSet.Id, "Tiles/Tall") };
        var animation = new FlipbookAnimation { Name = "stand" };
        animation.Clips.Add(new FlipbookClip());
        animation.Clips[0].Frames.Add(Frame("1:0,0", TileFlip.Horizontal));
        flipbook.Animations.Add(animation);
        await using var session = await TesseraSession.OpenAsync([("Tiles/Tall", tileSet), (Location, flipbook)]);
        session.WriteImage("Tiles/tall.png", 16, 24, (x, y) => x == 15 && y == 23 ? Color.Red : Color.Blue);
        var editor = await session.EditFlipbookAsync(Location);

        var sprite = Assert.Single(Collect(editor.Preview).Sprites);

        Assert.Equal(new RectangleF(-8, -24, 16, 24), sprite.Destination);
        Assert.Equal(Color.Red, sprite.Picture[0, 23]);
        Assert.Equal(Color.Blue, sprite.Picture[15, 23]);
    }

    private static async DispatcherTask<OpenEditor> OpenAsync(Action<FlipbookAsset>? change = null)
    {
        var tileSet = TileSet();
        var flipbook = Flipbook(tileSet, TownLocation);
        change?.Invoke(flipbook);
        var session = await TesseraSession.OpenAsync([(TownLocation, tileSet), (Location, flipbook)]);
        session.WriteImage("Tiles/town.png", 64, 32, Pixel);
        return new OpenEditor(session, await session.EditFlipbookAsync(Location));
    }

    private static FlipbookClip Clip(OpenEditor book, int animation, int clip) => book.Session.FlipbookAt(Location).Asset.Animations[animation].Clips[clip];

    // A click on a tile of the palette
    private static void Click(FlipbookEditorViewModel editor, int column, int row)
    {
        var at = new Vector2(column * 16 + 8, row * 16 + 8);
        editor.Palette!.Scene.PointerPressed(at, CanvasButton.Left, CanvasModifiers.None);
        editor.Palette.Scene.PointerReleased(at, CanvasButton.Left, CanvasModifiers.None);
    }

    private static CanvasFrame Collect(FlipbookPreviewScene preview, double? at = null)
    {
        if (at is { } time)
        {
            preview.Time = time;
        }
        var frame = new CanvasFrame();
        preview.Collect(new RectangleF(-1000, -1000, 2000, 2000), 4, frame);
        return frame;
    }

    private sealed class OpenEditor(TesseraSession session, FlipbookEditorViewModel editor) : IAsyncDisposable
    {
        public TesseraSession Session { get; } = session;

        public FlipbookEditorViewModel Editor { get; } = editor;

        public ValueTask DisposeAsync() => Session.DisposeAsync();
    }
}
