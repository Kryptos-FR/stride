using ColorRise.Tessera.Assets;
using ColorRise.Tessera.Editor.FlipbookEditor;
using Stride.Core.Assets.Editor.Testing;
using Stride.Core.IO;
using Stride.Core.Mathematics;
using Stride.Core.Presentation.Services;
using static ColorRise.Tessera.Editor.Tests.TestTown;

namespace ColorRise.Tessera.Editor.Tests;

/// <summary>
/// Mirror in the flipbook editor: the selected clip copied to the clip of the other side, each cell flipped.
/// </summary>
public sealed class FlipbookMirrorTests
{
    private const string Location = "Characters/Hero";
    private const string TownLocation = "Tiles/Town";

    [Theory]
    [InlineData(FlipbookDirection.None)]
    [InlineData(FlipbookDirection.Up)]
    [InlineData(FlipbookDirection.Down)]
    public async Task Mirror_is_off_for_a_direction_that_is_its_own_mirror(FlipbookDirection direction)
    {
        await using var book = await OpenAsync(asset => asset.Animations[0].Clips.Add(Clip(FlipbookDirection.Up, "1:3,0")));
        var editor = book.Editor;
        editor.SelectedItem = editor.Animations[0].Clips[1];
        Assert.True(editor.MirrorCommand.IsEnabled);
        editor.SelectedItem = editor.Animations.SelectMany(animation => animation.Clips).First(clip => clip.Direction == direction);

        Assert.False(editor.MirrorCommand.IsEnabled);
        editor.MirrorCommand.Execute();
        Assert.Empty(book.Session.Steps);
        Assert.Empty(book.Session.Dialogs.Messages);
    }

    [Fact]
    public async Task Mirror_makes_the_other_side_with_each_cell_flipped()
    {
        await using var book = await OpenAsync(asset =>
        {
            var walk = asset.Animations[0];
            walk.Clips.RemoveAt(1);
            var right = Clip(FlipbookDirection.Right);
            right.Frames.Add(Frame("1:0,0"));
            right.Frames.Add(Frame("1:1,0", TileFlip.Horizontal));
            right.Frames.Add(Frame("1:2,0", TileFlip.Vertical));
            right.Frames[1].Duration = 0.2f;
            walk.Clips.Add(right);
        });
        var editor = book.Editor;
        editor.SelectedItem = editor.Animations[0].Clips[1];
        Assert.True(editor.MirrorCommand.IsEnabled);

        editor.MirrorCommand.Execute();

        Assert.Empty(book.Session.Dialogs.Messages);
        Assert.Equal(["Mirror clip"], book.Session.Steps);
        var walk = Walk(book);
        Assert.Equal([FlipbookDirection.Down, FlipbookDirection.Right, FlipbookDirection.Left], walk.Clips.Select(clip => clip.Direction));
        var left = walk.Clips[2];
        Assert.Equal(["1:0,0", "1:1,0", "1:2,0"], left.Frames.Select(frame => frame.Cells.Single().Tile.ToString()));
        Assert.Equal([TileFlip.Horizontal, TileFlip.None, TileFlip.Horizontal | TileFlip.Vertical], left.Frames.Select(frame => frame.Cells.Single().Flip));
        Assert.Equal([0.1f, 0.2f, 0.1f], left.Frames.Select(frame => frame.Duration));
        Assert.All(left.Frames, frame => Assert.Equal(Int2.Zero, frame.Cells.Single().Offset));
        Assert.Same(editor.Animations[0].Clips[2], editor.SelectedItem);
        Assert.Same(left, editor.SelectedClip!.Clip);
    }

    [Fact]
    public async Task Mirror_moves_the_offsets_of_the_other_cells()
    {
        // The source 2 cuts the town in tiles of 8 by 16 pixels
        await using var book = await OpenAsync(
            asset =>
            {
                var frame = Frame("1:0,0");
                frame.Cells.Add(new FlipbookCell { Tile = TileId.Parse("2:0,0"), Offset = new Int2(2, 16) });
                // A tile the set lacks is as wide as the set's tiles
                frame.Cells.Add(new FlipbookCell { Tile = TileId.Parse("1:9,9"), Offset = new Int2(20, 0) });
                var right = Clip(FlipbookDirection.Right);
                right.Frames.Add(frame);
                asset.Animations[1].Clips.Add(right);
            },
            tileSet =>
            {
                var narrow = tileSet.AddSource<GridTileSetSource>("Narrow");
                narrow.Image = new UFile("town.png");
                narrow.TileSize = new Int2(8, 16);
            });
        var editor = book.Editor;
        editor.SelectedItem = editor.Animations[1].Clips[1];

        editor.MirrorCommand.Execute();

        var left = book.Session.FlipbookAt(Location).Asset.Animations[1].Clips[2];
        var cells = Assert.Single(left.Frames).Cells;
        Assert.Equal(["1:0,0", "2:0,0", "1:9,9"], cells.Select(cell => cell.Tile.ToString()));
        Assert.Equal([Int2.Zero, new Int2(6, 16), new Int2(-20, 0)], cells.Select(cell => cell.Offset));
        Assert.All(cells, cell => Assert.Equal(TileFlip.Horizontal, cell.Flip));
    }

    [Fact]
    public async Task Mirror_asks_before_it_replaces_a_clip_with_frames()
    {
        await using var book = await OpenAsync(asset => asset.Animations[0].Clips.Add(Clip(FlipbookDirection.Right, "1:3,0", "1:3,0")));
        var editor = book.Editor;
        var left = Walk(book).Clips[1];
        var frames = left.Frames.ToList();
        editor.SelectedItem = editor.Animations[0].Clips[2];

        book.Session.Dialogs.Answers.Enqueue(MessageBoxResult.No);
        editor.MirrorCommand.Execute();

        Assert.Equal("Replace the clip Left of 'walk'?", Assert.Single(book.Session.Dialogs.Messages));
        Assert.Equal(frames, left.Frames);
        Assert.Empty(book.Session.Steps);
        Assert.Same(editor.Animations[0].Clips[2], editor.SelectedItem);

        book.Session.Dialogs.Answers.Enqueue(MessageBoxResult.Yes);
        editor.MirrorCommand.Execute();

        Assert.Equal(2, book.Session.Dialogs.Messages.Count);
        Assert.Same(left, Walk(book).Clips[1]);
        Assert.Equal(["1:3,0", "1:3,0"], left.Frames.Select(frame => frame.Cells.Single().Tile.ToString()));
        Assert.All(left.Frames, frame => Assert.Equal(TileFlip.Horizontal, frame.Cells.Single().Flip));
        Assert.Equal(["Mirror clip"], book.Session.Steps);
        Assert.Same(editor.Animations[0].Clips[1], editor.SelectedItem);

        // One undo gives the clip its frames back
        book.Session.UndoRedo.Undo();
        Assert.Equal(frames, left.Frames);
    }

    [Fact]
    public async Task The_mirrored_frames_are_copies()
    {
        await using var book = await OpenAsync();
        var editor = book.Editor;
        editor.SelectedItem = editor.Animations[0].Clips[1];

        editor.MirrorCommand.Execute();

        var walk = Walk(book);
        var (left, right) = (walk.Clips[1], walk.Clips[2]);
        Assert.Equal(FlipbookDirection.Right, right.Direction);
        Assert.All(right.Frames, frame => Assert.DoesNotContain(frame, left.Frames));
        Assert.NotSame(left.Frames[0].Cells[0], right.Frames[0].Cells[0]);

        using (var transaction = book.Session.UndoRedo.CreateTransaction())
        {
            book.Session.Session.AssetNodeContainer.GetNode(right.Frames[0])![nameof(FlipbookFrame.Duration)].Update(0.5f);
            book.Session.UndoRedo.SetName(transaction, "Duration");
        }

        Assert.Equal(0.5f, right.Frames[0].Duration);
        Assert.Equal(0.1f, left.Frames[0].Duration);
        Assert.Equal(TileFlip.None, left.Frames[0].Cells[0].Flip);
    }

    [Fact]
    public async Task A_diagonal_mirrors_to_the_other_diagonal()
    {
        await using var book = await OpenAsync(asset => asset.Animations[0].Clips.Add(Clip(FlipbookDirection.UpRight, "1:2,1")));
        var editor = book.Editor;
        editor.SelectedItem = editor.Animations[0].Clips[2];

        editor.MirrorCommand.Execute();

        var upLeft = Walk(book).Clips[3];
        Assert.Equal(FlipbookDirection.UpLeft, upLeft.Direction);
        Assert.Equal(TileFlip.Horizontal, upLeft.Frames.Single().Cells.Single().Flip);
        Assert.Equal("Up left", ((FlipbookClipItemViewModel)editor.SelectedItem!).Label);
    }

    private static async DispatcherTask<OpenEditor> OpenAsync(Action<FlipbookAsset>? change = null, Action<TileSetAsset>? changeTileSet = null)
    {
        var tileSet = TileSet();
        changeTileSet?.Invoke(tileSet);
        var flipbook = Flipbook(tileSet, TownLocation);
        change?.Invoke(flipbook);
        var session = await TesseraSession.OpenAsync([(TownLocation, tileSet), (Location, flipbook)]);
        session.WriteImage("Tiles/town.png", 64, 32, Pixel);
        return new OpenEditor(session, await session.EditFlipbookAsync(Location));
    }

    private static FlipbookAnimation Walk(OpenEditor book) => book.Session.FlipbookAt(Location).Asset.Animations[0];

    private sealed class OpenEditor(TesseraSession session, FlipbookEditorViewModel editor) : IAsyncDisposable
    {
        public TesseraSession Session { get; } = session;

        public FlipbookEditorViewModel Editor { get; } = editor;

        public ValueTask DisposeAsync() => Session.DisposeAsync();
    }
}
