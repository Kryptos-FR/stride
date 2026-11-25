using System.Text;
using ColorRise.Tessera.Assets;
using ColorRise.Tessera.Editor.FlipbookEditor;
using ColorRise.Tessera.Editor.ViewModels;
using Stride.Core.Assets;
using Stride.Core.Assets.Editor.Testing;
using static ColorRise.Tessera.Editor.Tests.TestTown;

namespace ColorRise.Tessera.Editor.Tests;

/// <summary>
/// The changes of a flipbook asset made through its graph: each one a step the history undoes and does again.
/// </summary>
public sealed class FlipbookGraphTests
{
    private const string Location = "Characters/Hero";

    [Fact]
    public async Task Each_change_is_undone_and_redone()
    {
        await using var session = await OpenAsync();
        var flipbook = session.FlipbookAt(Location);
        var asset = flipbook.Asset;
        var walk = asset.Animations[0];
        var (down, left) = (walk.Clips[0], walk.Clips[1]);
        var changes = new (string Name, Action Change)[]
        {
            ("Add animation", () => FlipbookGraph.AddAnimation(flipbook, "run")),
            ("Add clip", () => FlipbookGraph.AddClip(flipbook, walk, FlipbookDirection.Up)),
            ("Add frames", () => FlipbookGraph.AddFrames(flipbook, down, 1, [Frame("1:3,0"), Frame("1:3,1")])),
            ("Move frames", () => FlipbookGraph.MoveFrames(flipbook, down, [0, 2], 5)),
            ("Remove frames", () => FlipbookGraph.RemoveFrames(flipbook, down, [1, 0])),
            ("Replace frames", () => FlipbookGraph.ReplaceFrames(flipbook, left, [Frame("1:2,1", TileFlip.Horizontal)])),
            ("Rename animation", () => FlipbookGraph.RenameAnimation(flipbook, walk, "stroll")),
            ("Remove clip", () => FlipbookGraph.RemoveClip(flipbook, walk, left)),
            ("Remove animation", () => FlipbookGraph.RemoveAnimation(flipbook, asset.Animations[1])),
            ("Set tile set", () => FlipbookGraph.SetTileSet(flipbook, null)),
        };

        foreach (var (name, change) in changes)
        {
            var before = Yaml(asset);
            using (var transaction = session.UndoRedo.CreateTransaction())
            {
                change();
                session.UndoRedo.SetName(transaction, name);
            }
            var after = Yaml(asset);
            Assert.NotEqual(before, after);

            session.UndoRedo.Undo();
            Assert.Equal(before, Yaml(asset));
            session.UndoRedo.Redo();
            Assert.Equal(after, Yaml(asset));
        }
        Assert.Equal(changes.Select(change => change.Name), session.Steps);
        Assert.Null(asset.TileSet);
        Assert.Equal(["stroll", "run"], asset.Animations.Select(animation => animation.Name));
    }

    [Fact]
    public async Task Moved_frames_keep_their_order_and_land_before_the_target()
    {
        await using var session = await OpenAsync();
        var flipbook = session.FlipbookAt(Location);
        var clip = flipbook.Asset.Animations[0].Clips[0];
        Change(session, () => FlipbookGraph.ReplaceFrames(flipbook, clip, [.. "abcde".Select(Lettered)]));

        Change(session, () => FlipbookGraph.MoveFrames(flipbook, clip, [1, 3], 0));
        Assert.Equal("bdace", Letters(clip));

        Change(session, () => FlipbookGraph.ReplaceFrames(flipbook, clip, [.. "abcde".Select(Lettered)]));
        Change(session, () => FlipbookGraph.MoveFrames(flipbook, clip, [0, 1], 5));
        Assert.Equal("cdeab", Letters(clip));

        // The indices are taken in the order of the frames, whatever the order they are given in
        Change(session, () => FlipbookGraph.MoveFrames(flipbook, clip, [4, 2], 1));
        Assert.Equal("cebda", Letters(clip));
    }

    [Fact]
    public async Task Removed_frames_are_named_by_their_indices_in_any_order()
    {
        await using var session = await OpenAsync();
        var flipbook = session.FlipbookAt(Location);
        var clip = flipbook.Asset.Animations[0].Clips[0];
        Change(session, () => FlipbookGraph.ReplaceFrames(flipbook, clip, [.. "abcd".Select(Lettered)]));

        Change(session, () => FlipbookGraph.RemoveFrames(flipbook, clip, [3, 0]));

        Assert.Equal("bc", Letters(clip));
    }

    [Fact]
    public async Task An_animation_can_be_added_without_a_clip()
    {
        await using var session = await OpenAsync();
        var flipbook = session.FlipbookAt(Location);

        FlipbookAnimation? added = null;
        Change(session, () => added = FlipbookGraph.AddAnimation(flipbook, "jump", withClip: false));
        FlipbookAnimation? withClip = null;
        Change(session, () => withClip = FlipbookGraph.AddAnimation(flipbook, "fall"));

        Assert.Same(added, flipbook.Asset.Animations[2]);
        Assert.Equal("jump", added!.Name);
        Assert.Empty(added.Clips);
        Assert.Equal([FlipbookDirection.None], withClip!.Clips.Select(clip => clip.Direction));
        Assert.Empty(withClip.Clips[0].Frames);
    }

    private static async DispatcherTask<TesseraSession> OpenAsync()
    {
        var tileSet = TileSet();
        return await TesseraSession.OpenAsync([("Tiles/Town", tileSet), (Location, Flipbook(tileSet, "Tiles/Town"))]);
    }

    private static void Change(TesseraSession session, Action change)
    {
        using var transaction = session.UndoRedo.CreateTransaction();
        change();
        session.UndoRedo.SetName(transaction, "Change");
    }

    // A frame named by a letter: a, b, c... show the tiles 1:0,0, 1:1,0, 1:2,0...
    private static FlipbookFrame Lettered(char letter) => Frame($"1:{letter - 'a'},0");

    private static string Letters(FlipbookClip clip) => string.Concat(clip.Frames.Select(frame => (char)('a' + frame.Cells[0].Tile.Column)));

    private static string Yaml(FlipbookAsset asset)
    {
        using var stream = new MemoryStream();
        AssetFileSerializer.Save(stream, asset, null);
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
