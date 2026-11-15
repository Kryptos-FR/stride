using ColorRise.Skein.Assets;
using Stride.Assets.Editor.Testing;
using Stride.Core.Assets.Presentation.ViewModels;
using Stride.Core.Presentation.Quantum.ViewModels;

namespace ColorRise.Skein.Editor.Tests.Assets;

/// <summary>
/// A Yarn project is an asset of the session as any other: the property grid shows and edits it.
/// </summary>
public sealed class YarnProjectAssetTests
{
    private static StrideEditorTestSessionOptions Options => new() { Plugins = [new SkeinEditorPlugin()] };

    [Fact]
    public async Task The_grid_shows_a_project_and_a_change_is_saved_in_its_file()
    {
        await using var session = await StrideEditorTestSession.OpenAsync(
            Options,
            ("Dialogue/Story", new YarnProjectAssetFactory().New()),
            ("Dialogue/Hello", new YarnScriptAsset { Text = "title: Start\n---\n===\n" }));
        var project = session.Asset<AssetViewModel>("Dialogue/Story");
        var path = Path.Combine(session.Directory, session.PackageName, "Assets", "Dialogue", "Story.sdyarn");
        Assert.True(File.Exists(path));

        var properties = session.Session.AssetViewProperties;
        var grid = await properties.GenerateSelectionPropertiesAsync([project]);

        Assert.NotNull(grid);
        Assert.True(properties.CanDisplayProperties, properties.FallbackMessage);
        Assert.Equal(
            ["SourceFiles", "ExcludedFiles", "BaseLanguage", "Localisations", "LineIdStyle", "Saliency", "DiagnosticSeverities", "WriteYarnProjectFile"],
            grid.RootNode.Children.Select(x => x.Name));
        Assert.Equal("**/*.yarn", Assert.Single(Child(grid, "SourceFiles").Children).NodeValue);

        Child(grid, "BaseLanguage").NodeValue = "fr";

        Assert.True(project.IsDirty);
        Assert.Equal("fr", ((YarnProjectAsset)project.Asset).BaseLanguage);
        var log = session.Session.SaveSessionWithoutUI();
        Assert.False(log.HasErrors, string.Join(Environment.NewLine, log.Messages));
        Assert.Contains("BaseLanguage: fr", File.ReadAllText(path));
        Assert.Equal([path], YarnProjectFiles.ProjectsOf(session.Asset<AssetViewModel>("Dialogue/Hello").AssetItem).Select(p => p.FullPath.ToOSPath()));
    }

    private static NodeViewModel Child(GraphViewModel grid, string name) => grid.RootNode.Children.Single(x => x.Name == name);
}
