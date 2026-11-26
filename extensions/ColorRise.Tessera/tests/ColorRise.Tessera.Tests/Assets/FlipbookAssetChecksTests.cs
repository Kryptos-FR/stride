using ColorRise.Tessera.Assets;
using Stride.Core.Assets;
using Stride.Core.Serialization;
using static ColorRise.Tessera.Tests.Assets.FlipbookAssetYamlTests;

namespace ColorRise.Tessera.Tests.Assets;

/// <summary>
/// What is wrong in a flipbook that still loads, as the property grid, a merge or a hand edit can leave it: the build warns
/// about it, and the editor shows it.
/// </summary>
public sealed class FlipbookAssetChecksTests
{
    [Fact]
    public void A_flipbook_that_is_right_has_no_line()
    {
        Assert.Empty(FlipbookAssetChecks.Of(Walker()));
        Assert.Empty(FlipbookAssetChecks.Of(Hero()));
    }

    [Theory]
    [InlineData("no tile set", "The flipbook has no tile set: it draws nothing.")]
    [InlineData("no name", "An animation has no name: game code cannot play it.")]
    [InlineData("a name twice", "The name 'walk' is given to 2 animations: the first is played.")]
    [InlineData("no clip", "The animation 'walk' has no clip.")]
    [InlineData("two clips", "The animation 'walk' has 2 clips for Left: the first is played.")]
    [InlineData("no frame", "The clip Left of 'walk' has no frame.")]
    [InlineData("no cell", "The frame 3 of the clip Left of 'walk' has no cell.")]
    [InlineData("no frame for any direction", "The clip Any direction of 'walk' has no frame.")]
    [InlineData("two clips for a diagonal", "The animation 'walk' has 2 clips for Up left: the first is played.")]
    public void Each_thing_wrong_has_its_line(string wrong, string line)
    {
        var asset = Walker();
        var walk = asset.Animations[0];
        switch (wrong)
        {
            case "no tile set":
                asset.TileSet = null;
                break;
            case "no name":
                asset.Animations.Add(Animation(""));
                break;
            case "a name twice":
                asset.Animations.Add(Animation("walk"));
                break;
            case "no clip":
                walk.Clips.Clear();
                break;
            case "two clips":
                walk.Clips.Add(Clip(FlipbookDirection.Left, Frame(0.1f, Cell("1:0,1"))));
                break;
            case "no frame":
                walk.Clips[1].Frames.Clear();
                break;
            case "no cell":
                walk.Clips[1].Frames[2].Cells.Clear();
                break;
            case "no frame for any direction":
                walk.Clips.Add(Clip(FlipbookDirection.None));
                break;
            case "two clips for a diagonal":
                walk.Clips.Add(Clip(FlipbookDirection.UpLeft, Frame(0.1f, Cell("1:0,1"))));
                walk.Clips.Add(Clip(FlipbookDirection.UpLeft, Frame(0.1f, Cell("1:0,1"))));
                break;
        }

        Assert.Equal([line], FlipbookAssetChecks.Of(asset));
    }

    [Fact]
    public void An_animation_without_a_name_is_told_once_and_its_clips_are_not_checked()
    {
        // Its lines could not name it; once it has a name, they come
        var asset = Walker();
        asset.Animations.Add(new FlipbookAnimation());
        asset.Animations.Add(new FlipbookAnimation());

        Assert.Equal(["An animation has no name: game code cannot play it."], FlipbookAssetChecks.Of(asset));
    }

    [Fact]
    public void Only_what_is_played_is_checked_further()
    {
        // The second walk and the second clip Left are never played: what is wrong in them is not told
        var asset = Walker();
        asset.Animations.Add(new FlipbookAnimation { Name = "walk" });
        asset.Animations[0].Clips.Add(Clip(FlipbookDirection.Left));

        Assert.Equal(
            [
                "The name 'walk' is given to 2 animations: the first is played.",
                "The animation 'walk' has 2 clips for Left: the first is played.",
            ],
            FlipbookAssetChecks.Of(asset));
    }

    // Step 3's minor m6
    [Fact]
    public void A_clip_whose_frames_all_last_nothing_is_told()
    {
        var asset = Walker();
        var left = asset.Animations[0].Clips[1];
        left.Frames.Clear();
        left.Frames.Add(Frame(0, Cell("1:0,1")));
        left.Frames.Add(Frame(-1, Cell("1:1,1")));

        Assert.Equal(["No frame of the clip Left of 'walk' lasts above 0 s: it shows nothing that moves."], FlipbookAssetChecks.Of(asset));

        left.Frames.Add(Frame(0.1f, Cell("1:2,1")));
        Assert.Empty(FlipbookAssetChecks.Of(asset));
    }

    [Fact]
    public void Empty_items_are_not_lines()
    {
        // As a merge leaves "~" in a list: the content leaves them out
        var asset = Walker();
        asset.Animations.Add(null!);
        asset.Animations[0].Clips.Add(null!);
        asset.Animations[0].Clips[0].Frames.Add(null!);
        asset.Animations[0].Clips[0].Frames[0].Cells.Add(null!);

        Assert.Empty(FlipbookAssetChecks.Of(asset));
    }

    // A flipbook with a tile set and one animation, "walk", whose clips Down and Left have three frames of one cell
    private static FlipbookAsset Walker()
    {
        var asset = new FlipbookAsset { TileSet = AttachedReferenceManager.CreateProxyObject<TileSet>(AssetId.New(), "Tiles/Hero") };
        var walk = new FlipbookAnimation { Name = "walk" };
        foreach (var direction in new[] { FlipbookDirection.Down, FlipbookDirection.Left })
        {
            walk.Clips.Add(Clip(direction, Frame(0.1f, Cell("1:0,0")), Frame(0.1f, Cell("1:1,0")), Frame(0.1f, Cell("1:2,0"))));
        }
        asset.Animations.Add(walk);
        return asset;
    }

    private static FlipbookAnimation Animation(string name)
    {
        var animation = new FlipbookAnimation { Name = name };
        animation.Clips.Add(Clip(FlipbookDirection.None, Frame(0.1f, Cell("1:0,0"))));
        return animation;
    }
}
