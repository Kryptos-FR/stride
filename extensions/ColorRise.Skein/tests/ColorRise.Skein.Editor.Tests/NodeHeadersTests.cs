using ColorRise.Skein.Assets.Text;
using ColorRise.Skein.Editor.Graph;
using ColorRise.Skein.Editor.Nodes;
using ColorRise.Skein.Editor.Text;
using Stride.Core.Assets.Editor.Testing;
using Stride.Core.Presentation.Quantum.ViewModels;

namespace ColorRise.Skein.Editor.Tests;

/// <summary>
/// The property grid shows the headers of the nodes selected on the graph, and what it changes is a change of the text.
/// </summary>
public sealed class NodeHeadersTests
{
    private const string Act1 = "title: Arrival\ntags: act1 harbour\ncolor: blue\nquest: lamp\n---\nNarrator: Here.\n<<jump Pier>>\n===\ntitle: Pier\n---\nNarrator: The pier.\n===\n";
    private const string Act2 = "title: Lighthouse\n---\n<<jump Arrival>>\n===\n";

    private static async DispatcherTask<SkeinSession> OpenAsync()
        => await SkeinSession.OpenAsync(SkeinSession.Project("Dialogue/Story"), SkeinSession.Script("Dialogue/Act1", Act1), SkeinSession.Script("Dialogue/Act2", Act2));

    [Fact]
    public async Task Selecting_cards_shows_their_headers_in_the_property_grid()
    {
        await using var session = await OpenAsync();
        var editor = session.Edit("Dialogue/Act1");
        await session.Analysis.WhenIdle();
        var properties = session.Session.ActiveProperties;

        editor.Select("Arrival");
        var grid = await GridAsync(session);

        Assert.Equal("Yarn node", properties.TypeDescription);
        Assert.Equal("Arrival", properties.Name);
        Assert.Equal(["Title", "Tags", "Color", "Cluster", "When", "Others"], grid.RootNode.Children.Select(x => x.Name));
        Assert.Equal("Arrival", Child(grid, "Title").NodeValue);
        Assert.Equal(["act1", "harbour"], Child(grid, "Tags").Children.Select(x => x.NodeValue));
        Assert.Equal("blue", Child(grid, "Color").NodeValue);
        Assert.Null(Child(grid, "Cluster").NodeValue);
        Assert.Empty(Child(grid, "When").Children);
        var quest = Assert.Single(Child(grid, "Others").Children);
        Assert.Equal(["quest", "lamp"], quest.Children.Select(x => x.NodeValue));

        // Two cards: the grid shows both, and the values they share
        editor.SelectedCards.Add(editor.Cards[1]);
        grid = await GridAsync(session);
        Assert.Equal("2 Yarn nodes selected", properties.Name);
        Assert.Equal(2, editor.SelectedHeaders.Count);

        // No card: the grid goes back to the script
        editor.SelectedCards.Clear();
        await GridAsync(session);
        Assert.Empty(editor.SelectedHeaders);
        Assert.Equal("Act1", properties.Name);
        Assert.Equal([session.ScriptAt("Dialogue/Act1")], properties.Selection);
    }

    [Fact]
    public async Task A_change_in_the_grid_is_a_change_of_the_text_in_one_step()
    {
        await using var session = await OpenAsync();
        var editor = session.Edit("Dialogue/Act1");
        await session.Analysis.WhenIdle();
        var script = session.ScriptAt("Dialogue/Act1");
        editor.Select("Arrival");
        var grid = await GridAsync(session);

        Child(grid, "Cluster").NodeValue = "Shore";

        Assert.Equal(Act1.Replace("quest: lamp\n", "quest: lamp\ncluster: Shore\n"), script.TextBuffer.Text);
        Assert.Equal(["Update property Cluster"], session.Steps);
        Assert.Equal("Shore", editor.Cards[0].Cluster);
        // The grid keeps the object it changed: it says what the text says
        Assert.Same(grid, session.Session.ActiveProperties.ViewModel);

        // Clearing the value takes the header away
        Child(grid, "Cluster").NodeValue = "";
        Assert.Equal(Act1, script.TextBuffer.Text);

        Child(grid, "Tags").Children.First().NodeValue = "act2";
        Assert.Equal(Act1.Replace("tags: act1 harbour", "tags: act2 harbour"), script.TextBuffer.Text);

        Child(grid, "Others").Children.Single().Children.Last().NodeValue = "oil";
        Assert.Contains("quest: oil\n", script.TextBuffer.Text);
        Assert.Equal(4, session.Steps.Count);

        // Undo gives the text of before back, and the grid shows it
        session.UndoRedo.Undo();
        session.UndoRedo.Undo();
        Assert.Equal(Act1, script.TextBuffer.Text);
        grid = await GridAsync(session);
        Assert.Equal("act1", Child(grid, "Tags").Children.First().NodeValue);
        Assert.Equal("lamp", Child(grid, "Others").Children.Single().Children.Last().NodeValue);
    }

    [Fact]
    public async Task A_title_changed_in_the_grid_renames_the_node_in_the_project()
    {
        await using var session = await OpenAsync();
        var editor = session.Edit("Dialogue/Act1");
        await session.Analysis.WhenIdle();
        var (act1, act2) = (session.ScriptAt("Dialogue/Act1"), session.ScriptAt("Dialogue/Act2"));
        editor.Select("Pier");
        var grid = await GridAsync(session);

        Child(grid, "Title").NodeValue = "Jetty";

        Assert.Equal(Act1.Replace("Pier", "Jetty"), act1.TextBuffer.Text);
        Assert.Equal(["Update property Title"], session.Steps);
        Assert.Equal("Jetty", session.Session.ActiveProperties.Name);

        editor.Select("Arrival");
        grid = await GridAsync(session);
        Child(grid, "Title").NodeValue = "Landing";
        Assert.Contains("<<jump Landing>>", act2.TextBuffer.Text);
        Assert.StartsWith("title: Landing\n", act1.TextBuffer.Text);

        // A title another node has is refused: no text changes, no step is made, and the grid shows the title again
        var before = act1.TextBuffer.Text;
        Child(grid, "Title").NodeValue = "Jetty";
        Assert.Equal(before, act1.TextBuffer.Text);
        Assert.Equal("A node named 'Jetty' already exists.", editor.Problem);
        Assert.Equal(["Update property Title", "Update property Title"], session.Steps);
        grid = await GridAsync(session);
        Assert.Equal("Landing", Child(grid, "Title").NodeValue);

        // A header that cannot be written is refused the same way
        Child(grid, "Others").Children.Single().Children.First().NodeValue = "title";
        Assert.Equal(before, act1.TextBuffer.Text);
        Assert.Equal("'title' is not a free header: it has a place of its own.", editor.Problem);
        grid = await GridAsync(session);
        Assert.Equal("quest", Child(grid, "Others").Children.Single().Children.First().NodeValue);
    }

    [Fact]
    public async Task A_rename_on_the_graph_shows_in_the_grid()
    {
        await using var session = await OpenAsync();
        var editor = session.Edit("Dialogue/Act1");
        await session.Analysis.WhenIdle();
        editor.Select("Arrival");
        await GridAsync(session);

        Assert.True(editor.Rename((YarnNodeCardViewModel)editor.SelectedCards[0], "Landing"));

        var grid = await GridAsync(session);
        Assert.Equal("Landing", session.Session.ActiveProperties.Name);
        Assert.Equal("Landing", Child(grid, "Title").NodeValue);

        editor.Select("Pier");
        await GridAsync(session);
        Assert.True(editor.Rename((YarnNodeCardViewModel)editor.SelectedCards[0], "Jetty"));
        grid = await GridAsync(session);
        Assert.Equal("Jetty", Child(grid, "Title").NodeValue);
    }

    [Fact]
    public async Task The_grid_follows_the_text_while_it_shows_the_nodes()
    {
        await using var session = await OpenAsync();
        var editor = session.Edit("Dialogue/Act1");
        await session.Analysis.WhenIdle();
        var script = session.ScriptAt("Dialogue/Act1");
        var properties = session.Session.ActiveProperties;
        editor.Select("Arrival");
        var grid = await GridAsync(session);

        // Typing in a header
        script.TextBuffer.Replace(Act1.IndexOf("blue", StringComparison.Ordinal), 4, "red", YarnTextBuffer.TypingActionName);
        grid = await GridAsync(session);
        Assert.Equal("red", Child(grid, "Color").NodeValue);

        // Typing in the body leaves the grid as it is
        script.TextBuffer.Replace(script.TextBuffer.Text.IndexOf("Here", StringComparison.Ordinal), 4, "There", YarnTextBuffer.TypingActionName);
        Assert.Same(grid, properties.ViewModel);

        // The user selects the script in the asset view: the grid is not the nodes' any more, and the text does not take it back
        await properties.GenerateSelectionPropertiesAsync([script]);
        script.TextBuffer.Replace(script.TextBuffer.Text.IndexOf("red", StringComparison.Ordinal), 3, "green", YarnTextBuffer.TypingActionName);
        Assert.Equal([script], properties.Selection);

        // Coming back to the graph does
        editor.ShowNodeProperties();
        grid = await GridAsync(session);
        Assert.Equal("green", Child(grid, "Color").NodeValue);

        // The editor closes: the grid shows nothing of it
        Assert.True(await session.Session.EditorCollection.CloseAssetEditorAsync(script, save: false));
        Assert.Empty(properties.Selection ?? []);
    }

    [Fact]
    public void Headers_are_read_from_the_text_and_written_back()
    {
        const string text = "title: Gull\nposition: 10,20\ntags: bird  shore\nwhen: $day > 1\nwhen: once\nstyle: note\ncolor:\n---\nKraa.\n===\n";
        var document = YarnDocument.Parse(text);
        var node = document.Nodes[0];

        var headers = YarnNodeHeadersText.Read(node);

        Assert.Equal("Gull", headers.Title);
        Assert.Equal(["bird", "shore"], headers.Tags);
        Assert.Null(headers.Color);
        Assert.Null(headers.Cluster);
        Assert.Equal(["$day > 1", "once"], headers.When);
        // The place is the graph's, not a free header
        Assert.Equal([("style", "note")], headers.Others.Select(o => (o.Key, o.Value)));
        Assert.True(YarnNodeHeadersText.Matches(headers, node));
        Assert.Empty(YarnNodeHeadersText.Write(document, node, headers));

        // What the grid holds before it can be written is no difference
        headers.Tags.Add("");
        headers.When.Add(" ");
        headers.Others.Add(new YarnHeader());
        Assert.True(YarnNodeHeadersText.Matches(headers, node));
        Assert.Empty(YarnNodeHeadersText.Write(document, node, headers));

        headers.Tags.RemoveAt(0);
        headers.When[0] = "$day > 2";
        headers.When.RemoveAt(1);
        headers.Cluster = "Sky";
        headers.Others.Clear();
        headers.Others.Add(new YarnHeader { Key = "quest", Value = "gulls" });
        Assert.False(YarnNodeHeadersText.Matches(headers, node));

        var written = YarnTextEdit.Apply(text, YarnNodeHeadersText.Write(document, node, headers));
        Assert.Equal("title: Gull\nposition: 10,20\ntags: shore\nwhen: $day > 2\ncolor:\ncluster: Sky\nquest: gulls\n---\nKraa.\n===\n", written);
        Assert.True(YarnNodeHeadersText.Matches(headers, YarnDocument.Parse(written).Nodes[0]));

        headers.Others[0].Key = "position";
        Assert.Throws<ArgumentException>(() => YarnNodeHeadersText.Write(document, node, headers));
    }

    private static async Task<GraphViewModel> GridAsync(SkeinSession session)
    {
        var properties = session.Session.ActiveProperties;
        await properties.RefreshSelectedPropertiesAsync();
        Assert.True(properties.CanDisplayProperties, properties.FallbackMessage);
        return properties.ViewModel!;
    }

    private static NodeViewModel Child(GraphViewModel grid, string name) => grid.RootNode.Children.Single(x => x.Name == name);
}
