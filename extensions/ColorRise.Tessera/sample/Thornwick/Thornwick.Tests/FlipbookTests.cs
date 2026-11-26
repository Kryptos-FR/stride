using ColorRise.Tessera;
using ColorRise.Tessera.Assets;
using Stride.Core.Mathematics;

namespace Thornwick.Tests;

/// <summary>
/// The flipbooks of the hero and the villagers, made in Game Studio: each stands and walks the four ways.
/// </summary>
public sealed class FlipbookTests
{
    private static readonly FlipbookDirection[] Sides = [FlipbookDirection.Down, FlipbookDirection.Left, FlipbookDirection.Right, FlipbookDirection.Up];

    public static TheoryData<string> Flipbooks => new(SampleAssets.Flipbooks);

    [Theory]
    [MemberData(nameof(Flipbooks))]
    public void A_flipbook_has_nothing_to_tell(string url)
        => Assert.Empty(FlipbookAssetChecks.Of(SampleAssets.FlipbookAsset(url)));

    [Theory]
    [MemberData(nameof(Flipbooks))]
    public void A_figure_stands_and_walks_the_four_ways(string url)
    {
        var flipbook = SampleAssets.Flipbook(url);
        var tiles = Assert.IsType<TileSet>(flipbook.TileSet);

        Assert.Equal(new Vector2(0.5f, 0), flipbook.Pivot);
        var idle = Assert.Single(flipbook.Animations, animation => animation.Name == "idle");
        var walk = Assert.Single(flipbook.Animations, animation => animation.Name == "walk");
        Assert.Equal(FlipbookLoopMode.Loop, walk.LoopMode);
        foreach (var animation in new[] { idle, walk })
        {
            foreach (var side in Sides)
            {
                // Looked for in Clips: FindClip would fall back to another side
                var clip = Assert.Single(animation.Clips, clip => clip.Direction == side);
                Assert.True(animation != walk || clip.Frames.Count >= 2, $"{animation.Name}, {side}: {clip.Frames.Count} frame");
                foreach (var frame in clip.Frames)
                {
                    Assert.True(frame.Duration > 0, $"{animation.Name}, {side}: a frame lasts {frame.Duration} s");
                    Assert.NotEmpty(frame.Cells);
                    Assert.All(frame.Cells, cell => Assert.True(tiles.Tiles.ContainsKey(cell.Tile), $"{animation.Name}, {side}: no tile {cell.Tile}"));
                }
            }
        }
    }

    [Fact]
    public void The_four_figures_differ()
    {
        var standing = SampleAssets.Flipbooks
            .Select(url => SampleAssets.Flipbook(url).Animations.Single(animation => animation.Name == "idle"))
            .Select(idle => idle.Clips.Single(clip => clip.Direction == FlipbookDirection.Down).Frames[0].Cells[0].Tile)
            .ToHashSet();

        Assert.Equal(4, standing.Count);
    }
}
