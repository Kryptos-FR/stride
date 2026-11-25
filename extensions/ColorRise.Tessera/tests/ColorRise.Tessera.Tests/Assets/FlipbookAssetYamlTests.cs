using System.Runtime.CompilerServices;
using System.Text;
using ColorRise.Tessera.Assets;
using Stride.Core.Assets;
using Stride.Core.IO;
using Stride.Core.Mathematics;
using Stride.Core.Serialization;

namespace ColorRise.Tessera.Tests.Assets;

/// <summary>
/// The file of a flipbook asset, and the content it makes.
/// </summary>
public sealed class FlipbookAssetYamlTests
{
    private static readonly Guid HeroId = new("3c9e5b14-2f7a-4d61-b8e0-7a1d4c2e9f53");
    private static readonly Guid HeroTilesId = new("a41f7c2d-8e3b-4b95-9c06-1d5e8f2a7b34");

    [Fact]
    public void The_golden_file_loads_and_saves_to_the_same_bytes()
    {
        // Written at the first run, or on demand, and read by a person: it is what a diff of a flipbook shows
        var golden = Path.Combine(GoldenFolder(), "hero.sdflipbook");
        if (!File.Exists(golden) || Environment.GetEnvironmentVariable("TESSERA_WRITE_GOLDEN") == "1")
        {
            File.WriteAllText(golden, Write(Hero()));
        }

        // Ids aside: the golden file reads as Hero, and writes back to itself
        var text = File.ReadAllText(golden).ReplaceLineEndings("\n");
        var read = Read(text);
        Assert.Equal(Describe(Hero()), Describe(read));
        Assert.Equal(text, Write(read));
    }

    [Fact]
    public void A_cell_is_written_with_its_tile_as_text()
    {
        var yaml = Write(Hero());

        Assert.Contains("Tile: 1:0,0", yaml, StringComparison.Ordinal);
        Assert.Contains("Flip: Horizontal", yaml, StringComparison.Ordinal);
    }

    [Fact]
    public void A_new_flipbook_has_one_animation_idle_with_a_clip_None()
    {
        var asset = new FlipbookAssetFactory().New();

        var animation = Assert.Single(asset.Animations);
        Assert.Equal(FlipbookAssetFactory.FirstAnimationName, animation.Name);
        Assert.Equal("idle", animation.Name);
        Assert.Equal(FlipbookLoopMode.Loop, animation.LoopMode);
        var clip = Assert.Single(animation.Clips);
        Assert.Equal(FlipbookDirection.None, clip.Direction);
        Assert.Empty(clip.Frames);
        Assert.Null(asset.TileSet);
        Assert.Equal(new Vector2(0.5f, 0), asset.Pivot);
    }

    [Fact]
    public void The_content_has_the_values_of_the_asset_in_objects_of_its_own()
    {
        var asset = Hero();

        var flipbook = asset.ToFlipbook();

        Assert.Same(asset.TileSet, flipbook.TileSet);
        Assert.Equal(Describe(asset), Describe(flipbook.TileSet, flipbook.Pivot, flipbook.Animations));
        // An editor that changes the asset after a build changes nothing in the content it gave
        Assert.NotSame(asset.Animations[0], flipbook.Animations[0]);
        Assert.NotSame(asset.Animations[0].Clips[0], flipbook.Animations[0].Clips[0]);
        Assert.NotSame(asset.Animations[0].Clips[0].Frames[0], flipbook.Animations[0].Clips[0].Frames[0]);
        Assert.NotSame(asset.Animations[0].Clips[0].Frames[0].Cells[0], flipbook.Animations[0].Clips[0].Frames[0].Cells[0]);
    }

    [Fact]
    public void An_empty_item_a_merge_leaves_is_not_in_the_content()
    {
        // As a hand edit or a merge leaves "~" in a list
        var asset = Hero();
        asset.Animations.Insert(0, null!);
        asset.Animations[1].Clips.Add(null!);
        asset.Animations[1].Clips[0].Frames.Add(null!);
        asset.Animations[1].Clips[0].Frames[0].Cells.Add(null!);

        var flipbook = asset.ToFlipbook();

        Assert.Equal(Describe(Hero()), Describe(flipbook.TileSet, flipbook.Pivot, flipbook.Animations));
    }

    /// <summary>
    /// The hero of the golden file, whose tile set is at <c>Tiles/Hero</c>: <c>idle</c> (a loop, a clip None of two frames of
    /// 0.5), <c>walk</c> (a ping-pong, a clip Down of two frames of 0.1 and a clip Left of one frame, mirrored) and
    /// <c>attack</c> (once, a clip None of one frame of two cells, the second 16 pixels to the right).
    /// </summary>
    internal static FlipbookAsset Hero()
    {
        var asset = new FlipbookAsset
        {
            Id = (AssetId)HeroId,
            TileSet = AttachedReferenceManager.CreateProxyObject<TileSet>((AssetId)HeroTilesId, "Tiles/Hero"),
        };

        var idle = new FlipbookAnimation { Name = "idle" };
        idle.Clips.Add(Clip(FlipbookDirection.None, Frame(0.5f, Cell("1:0,0")), Frame(0.5f, Cell("1:1,0"))));
        var walk = new FlipbookAnimation { Name = "walk", LoopMode = FlipbookLoopMode.PingPong };
        walk.Clips.Add(Clip(FlipbookDirection.Down, Frame(0.1f, Cell("1:0,1")), Frame(0.1f, Cell("1:1,1"))));
        walk.Clips.Add(Clip(FlipbookDirection.Left, Frame(0.1f, Cell("1:0,2", flip: TileFlip.Horizontal))));
        var attack = new FlipbookAnimation { Name = "attack", LoopMode = FlipbookLoopMode.Once };
        attack.Clips.Add(Clip(FlipbookDirection.None, Frame(0.2f, Cell("1:0,3"), Cell("1:1,3", new Int2(16, 0)))));
        asset.Animations.AddRange([idle, walk, attack]);
        return asset;
    }

    internal static FlipbookClip Clip(FlipbookDirection direction, params FlipbookFrame[] frames)
    {
        var clip = new FlipbookClip { Direction = direction };
        clip.Frames.AddRange(frames);
        return clip;
    }

    internal static FlipbookFrame Frame(float duration, params FlipbookCell[] cells)
    {
        var frame = new FlipbookFrame { Duration = duration };
        frame.Cells.AddRange(cells);
        return frame;
    }

    internal static FlipbookCell Cell(string tile, Int2 offset = default, TileFlip flip = TileFlip.None)
        => new() { Tile = TileId.Parse(tile), Offset = offset, Flip = flip };

    // The values of a flipbook asset, ids aside
    internal static string Describe(FlipbookAsset asset) => Describe(asset.TileSet, asset.Pivot, asset.Animations);

    internal static string Describe(TileSet? tileSet, Vector2 pivot, IEnumerable<FlipbookAnimation> animations)
    {
        var text = new StringBuilder();
        var url = tileSet is null ? "none" : AttachedReferenceManager.GetUrl(tileSet) ?? "a tile set";
        text.Append($"{url} {pivot}\n");
        foreach (var animation in animations)
        {
            text.Append($"{animation.Name} {animation.LoopMode}\n");
            foreach (var clip in animation.Clips)
            {
                text.Append($"  {clip.Direction}\n");
                foreach (var frame in clip.Frames)
                {
                    text.Append($"    {frame.Duration}:");
                    foreach (var cell in frame.Cells)
                    {
                        text.Append($" {cell.Tile} {cell.Offset} {cell.Flip}");
                    }
                    text.Append('\n');
                }
            }
        }
        return text.ToString();
    }

    private static string Write(FlipbookAsset asset)
    {
        using var stream = new MemoryStream();
        AssetFileSerializer.Save(stream, asset, null);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static FlipbookAsset Read(string yaml)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(yaml));
        return (FlipbookAsset)AssetFileSerializer.Load<Asset>(stream, new UFile("Hero.sdflipbook")).Asset;
    }

    private static string GoldenFolder([CallerFilePath] string path = "") => Path.Combine(Path.GetDirectoryName(path)!, "Golden");
}
