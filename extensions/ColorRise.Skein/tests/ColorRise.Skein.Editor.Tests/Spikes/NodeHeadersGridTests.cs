using ColorRise.Skein.Assets;
using ColorRise.Skein.Editor.Nodes;
using ColorRise.Skein.Editor.ViewModels;
using Stride.Assets.Editor.Testing;
using Stride.Core.Assets.Editor.Testing;
using Stride.Core.Presentation.Dirtiables;
using Stride.Core.Presentation.Quantum.ViewModels;
using Stride.Core.Presentation.Services;

namespace ColorRise.Skein.Editor.Tests.Spikes;

/// <summary>
/// The session's property grid shows and edits the headers of a Yarn node, which are no asset's own graph.
/// </summary>
public sealed class NodeHeadersGridTests
{
    private static StrideEditorTestSessionOptions Options => new() { Plugins = [new SkeinEditorPlugin()] };

    private static YarnNodeHeaders Arrival => new()
    {
        Title = "Arrival",
        Tags = { "act1", "harbour" },
        Color = "blue",
        Cluster = "Act 1",
        When = { "$tide == Tide.Low" },
        Others = { new YarnHeader { Key = "quest", Value = "lamp" } },
    };

    [Fact]
    public async Task The_grid_shows_the_headers_of_a_node()
    {
        await using var session = await StrideEditorTestSession.OpenAsync(Options, ("Dialogue/Hello", new YarnScriptAsset { Text = "title: Arrival\n---\n===\n" }));
        using var headers = new YarnNodeHeadersViewModel(session.Session, Arrival);

        var grid = await ShowAsync(session, headers);

        Assert.Equal(["Title", "Tags", "Color", "Cluster", "When", "Others"], grid.RootNode.Children.Select(x => x.Name));
        Assert.Equal("Arrival", Child(grid, "Title").NodeValue);
        Assert.Equal(2, Child(grid, "Tags").Children.Count);
        Assert.Equal("harbour", Child(grid, "Tags").Children.ElementAt(1).NodeValue);
        var quest = Assert.Single(Child(grid, "Others").Children);
        Assert.Equal(["Key", "Value"], quest.Children.Select(x => x.Name));
    }

    [Fact]
    public async Task A_change_in_the_grid_is_reported_once_inside_one_undo_step()
    {
        await using var session = await StrideEditorTestSession.OpenAsync(Options, ("Dialogue/Hello", new YarnScriptAsset { Text = "title: Arrival\n---\n===\n" }));
        var script = session.Asset<YarnScriptViewModel>("Dialogue/Hello");
        var undoRedo = session.Session.ServiceProvider.Get<IUndoRedoService>();
        using var headers = new YarnNodeHeadersViewModel(session.Session, Arrival);
        var reported = new List<string>();
        var undone = 0;
        headers.Changed += changed =>
        {
            reported.Add(changed.Title);
            // What the script editor does here: it rewrites the header in the script's text
            undoRedo.PushOperation(new AnonymousDirtyingOperation(script.Dirtiables, () => ++undone, () => { }));
        };
        var steps = new List<string?>();
        undoRedo.Done += (_, e) => steps.Add(undoRedo.GetName(e.Transaction));
        var grid = await ShowAsync(session, headers);

        Child(grid, "Title").NodeValue = "Landing";

        Assert.Equal(["Landing"], reported);
        Assert.Equal("Landing", headers.Headers.Title);
        Assert.Equal(["Update property Title"], steps);
        Assert.True(script.IsDirty);
        undoRedo.Undo();
        Assert.Equal(1, undone);
        Assert.False(script.IsDirty);
    }

    [Fact]
    public async Task Adding_and_removing_a_tag_in_the_grid_is_reported()
    {
        await using var session = await StrideEditorTestSession.OpenAsync(Options, ("Dialogue/Hello", new YarnScriptAsset { Text = "title: Arrival\n---\n===\n" }));
        using var headers = new YarnNodeHeadersViewModel(session.Session, Arrival);
        var reported = new List<string>();
        headers.Changed += changed => reported.Add(string.Join(' ', changed.Tags));
        var grid = await ShowAsync(session, headers);

        Child(grid, "Tags").Children.First().NodeValue = "act2";

        Assert.Equal(["act2 harbour"], reported);
    }

    private static async Task<GraphViewModel> ShowAsync(EditorTestSession session, YarnNodeHeadersViewModel headers)
    {
        var properties = session.Session.AssetViewProperties;
        properties.UpdateTypeAndName([headers], _ => "Yarn node", x => x.Headers.Title, "Yarn nodes");
        var grid = await properties.GenerateSelectionPropertiesAsync([headers]);
        Assert.NotNull(grid);
        Assert.True(properties.CanDisplayProperties, properties.FallbackMessage);
        Assert.Equal("Arrival", properties.Name);
        return grid;
    }

    private static NodeViewModel Child(GraphViewModel grid, string name) => grid.RootNode.Children.Single(x => x.Name == name);
}
