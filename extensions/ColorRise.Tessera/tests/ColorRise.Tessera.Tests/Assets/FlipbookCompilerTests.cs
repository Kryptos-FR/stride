using ColorRise.Tessera.Assets;
using Stride.Core.Assets;
using Stride.Core.BuildEngine;
using Stride.Core.Mathematics;
using Stride.Core.Serialization;
using Stride.Core.Serialization.Contents;
using static ColorRise.Tessera.Tests.Assets.FlipbookAssetYamlTests;

namespace ColorRise.Tessera.Tests.Assets;

/// <summary>
/// The build of a game compiles a flipbook asset to a <see cref="Flipbook"/> at the asset's URL, with its tile set.
/// </summary>
[Collection(BuildCollection.Name)]
public sealed class FlipbookCompilerTests
{
    [Fact]
    public void A_flipbook_builds_to_content_that_names_its_tile_set()
    {
        using var build = new AssetBuild();
        // The tile set is not a root: the flipbook brings it in the build
        var tiles = build.Add("Tiles/Town", TileSetCompilerTests.RedAndBlue(build), root: false);
        var asset = Hero(tiles);
        asset.Pivot = new Vector2(0.25f, 0.5f);
        build.Add("Flipbooks/Hero", asset);

        var result = build.Build();

        Assert.True(result.Succeeded, string.Join("\n", result.Errors));
        Assert.DoesNotContain(result.Warnings, warning => warning.Contains("Flipbooks/Hero", StringComparison.Ordinal));
        // The tile set comes with the flipbook; its textures, which need a graphics device, do not
        var flipbook = build.Content.Load<Flipbook>("Flipbooks/Hero",
            new ContentManagerLoaderSettings { ContentFilter = ContentManagerLoaderSettings.NewContentFilterByType(typeof(TileSet)) });
        Assert.Equal(Values(asset), Values(flipbook));
        Assert.NotNull(flipbook.TileSet);
        Assert.Equal("Tiles/Town", build.UrlOf(flipbook.TileSet));
        Assert.Equal([TileId.FromGrid(1, 0, 0), TileId.FromGrid(1, 1, 0)], flipbook.TileSet.Tiles.Keys.Order());
    }

    [Fact]
    public void The_warnings_do_not_fail_the_build()
    {
        using var build = new AssetBuild();
        var asset = Hero(build.Add("Tiles/Town", TileSetCompilerTests.RedAndBlue(build), root: false));
        var walk = asset.Animations.Single(animation => animation.Name == "walk");
        walk.Clips.Add(Clip(FlipbookDirection.Left, Frame(0.1f, Cell("1:1,0"))));
        walk.Clips[0].Frames.Clear();
        build.Add("Flipbooks/Hero", asset);

        var result = build.Build();

        Assert.True(result.Succeeded, string.Join("\n", result.Errors));
        Assert.Contains("Flipbooks/Hero: The animation 'walk' has 2 clips for Left: the first is played.", result.Warnings);
        Assert.Contains("Flipbooks/Hero: The clip Down of 'walk' has no frame.", result.Warnings);
        Assert.Equal(Values(asset), Values(build.LoadAlone<Flipbook>("Flipbooks/Hero")));
    }

    // Review Focus 1
    [Fact]
    public void A_flipbook_without_a_tile_set_builds_with_a_warning()
    {
        using var build = new AssetBuild();
        var asset = FlipbookAssetYamlTests.Hero();
        asset.TileSet = null;
        build.Add("Flipbooks/Hero", asset);

        var result = build.Build();

        Assert.True(result.Succeeded, string.Join("\n", result.Errors));
        Assert.Equal(["Flipbooks/Hero: The flipbook has no tile set: it draws nothing."], result.Warnings);
        var flipbook = build.LoadAlone<Flipbook>("Flipbooks/Hero");
        Assert.Null(flipbook.TileSet);
        Assert.Equal(Values(asset), Values(flipbook));
    }

    [Fact]
    public void A_cell_whose_tile_the_set_does_not_have_is_kept()
    {
        using var build = new AssetBuild();
        var asset = Hero(build.Add("Tiles/Town", TileSetCompilerTests.RedAndBlue(build), root: false));
        asset.Animations[0].Clips[0].Frames[0].Cells[0].Tile = TileId.Parse("1:9,9");
        build.Add("Flipbooks/Hero", asset);

        var result = build.Build();

        Assert.True(result.Succeeded, string.Join("\n", result.Errors));
        Assert.DoesNotContain(result.Warnings, warning => warning.Contains("Flipbooks/Hero", StringComparison.Ordinal));
        var cell = Assert.Single(build.LoadAlone<Flipbook>("Flipbooks/Hero").Animations[0].Clips[0].Frames[0].Cells);
        Assert.Equal(TileId.Parse("1:9,9"), cell.Tile);
    }

    [Fact]
    public void A_second_build_runs_no_command()
    {
        using var build = new AssetBuild();
        build.Add("Flipbooks/Hero", Hero(build.Add("Tiles/Town", TileSetCompilerTests.RedAndBlue(build), root: false)));
        Assert.Contains(build.Build().Commands, command => command.Url == "Flipbooks/Hero" && command.Status == ResultStatus.Successful);

        var second = build.Build();

        Assert.True(second.Succeeded, string.Join("\n", second.Errors));
        Assert.NotEmpty(second.Commands);
        Assert.All(second.Commands, command => Assert.Equal(ResultStatus.NotTriggeredWasSuccessful, command.Status));
    }

    [Fact]
    public void A_change_of_a_frame_builds_the_flipbook_again()
    {
        using var build = new AssetBuild();
        var asset = Hero(build.Add("Tiles/Town", TileSetCompilerTests.RedAndBlue(build), root: false));
        build.Add("Flipbooks/Hero", asset);
        var first = build.Build();
        Assert.True(first.Succeeded, string.Join("\n", first.Errors));
        Assert.Contains(first.Commands, command => command.Url == "Tiles/Town" && command.Status == ResultStatus.Successful);

        asset.Animations[0].Clips[0].Frames[1].Duration = 0.75f;
        var changed = build.Build();

        Assert.True(changed.Succeeded, string.Join("\n", changed.Errors));
        Assert.Equal(ResultStatus.Successful, changed.Commands.Single(command => command.Url == "Flipbooks/Hero").Status);
        Assert.Equal(ResultStatus.NotTriggeredWasSuccessful, changed.Commands.Single(command => command.Url == "Tiles/Town").Status);
        Assert.Equal(0.75f, build.LoadAlone<Flipbook>("Flipbooks/Hero").Animations[0].Clips[0].Frames[1].Duration);
    }

    // The hero of the golden file, a new asset whose tile set is the one of an item of the build
    private static FlipbookAsset Hero(AssetItem tileSet)
    {
        var asset = FlipbookAssetYamlTests.Hero();
        asset.Id = AssetId.New();
        asset.TileSet = AttachedReferenceManager.CreateProxyObject<TileSet>(tileSet.Id, tileSet.Location);
        return asset;
    }

    // The values of a flipbook, its tile set aside
    private static string Values(FlipbookAsset asset) => Describe(null, asset.Pivot, asset.Animations);

    private static string Values(Flipbook flipbook) => Describe(null, flipbook.Pivot, flipbook.Animations);
}
