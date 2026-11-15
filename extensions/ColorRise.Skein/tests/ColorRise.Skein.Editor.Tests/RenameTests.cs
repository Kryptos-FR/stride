using ColorRise.Skein.Editor.Services;

namespace ColorRise.Skein.Editor.Tests;

/// <summary>
/// A node is renamed in every script of its projects, whether an editor has them open or not.
/// </summary>
public sealed class RenameTests
{
    private const string Act1 =
        "title: Start\n---\n<<declare $tide = 0>>\nOdile: To the pier.\n<<jump Pier>>\n===\n"
        + "title: Pier\nwhen: $tide > 2\n---\nFisher: Mind the nets.\n===\n"
        + "title: Pier\nwhen: always\n---\nFisher: Quiet today. // not like the Pier of old\n===\n";

    private const string Act2 = "title: Harbour\n---\n<<if visited(\"Pier\")>>\n    Back from the Pier.\n<<endif>>\n<<detour Pier>>\n===\n";

    private const string Far = "title: Far\n---\n<<jump Pier>>\n===\n";

    [Fact]
    public async Task Renaming_a_node_changes_what_names_it_in_the_scripts_of_its_project_as_one_step()
    {
        await using var session = await SkeinSession.OpenAsync(
            SkeinSession.Project("Dialogue/Story"), SkeinSession.Script("Dialogue/Act1", Act1), SkeinSession.Script("Dialogue/Act2", Act2), SkeinSession.Script("Elsewhere/Far", Far));
        var editor = session.Edit("Dialogue/Act1");
        var (act1, act2, far) = (session.ScriptAt("Dialogue/Act1"), session.ScriptAt("Dialogue/Act2"), session.ScriptAt("Elsewhere/Far"));
        await session.Analysis.WhenIdle();
        var cards = editor.Cards.ToList();

        // The second script has no editor open
        Assert.Single(session.Session.EditorCollection.Editors);
        Assert.True(editor.Rename(cards[2], "Quay"));

        // The two nodes of the group and the jump; the text of a line and a comment stay
        var renamed1 = Act1.Replace("<<jump Pier>>", "<<jump Quay>>").Replace("title: Pier", "title: Quay");
        var renamed2 = Act2.Replace("visited(\"Pier\")", "visited(\"Quay\")").Replace("<<detour Pier>>", "<<detour Quay>>");
        Assert.Equal(renamed1, act1.TextBuffer.Text);
        Assert.Equal(renamed2, act2.TextBuffer.Text);
        Assert.Contains("Back from the Pier.", renamed2);
        Assert.Contains("// not like the Pier of old", renamed1);

        // A script of no project is not one that can name the node
        Assert.Equal(Far, far.TextBuffer.Text);
        Assert.Equal(["Rename node 'Pier' to 'Quay'"], session.Steps);
        Assert.Equal((true, true, false), (act1.IsDirty, act2.IsDirty, far.IsDirty));

        // The cards are the ones of before, and the compiler finds every jump its node
        Assert.Equal(cards, editor.Cards);
        Assert.Equal(["Start", "Quay", "Quay"], editor.Cards.Select(c => c.Title));
        await session.Analysis.WhenIdle();
        Assert.Empty(editor.Diagnostics);
        Assert.Empty(editor.Stubs);
        Assert.Same(cards[1], Assert.Single(editor.Links).Target);

        session.UndoRedo.Undo();
        Assert.Equal(Act1, act1.TextBuffer.Text);
        Assert.Equal(Act2, act2.TextBuffer.Text);
        Assert.Equal((false, false), (act1.IsDirty, act2.IsDirty));
        Assert.Equal(cards, editor.Cards);

        session.UndoRedo.Redo();
        Assert.Equal(renamed1, act1.TextBuffer.Text);
        Assert.Equal(renamed2, act2.TextBuffer.Text);
        Assert.Single(session.Steps);
    }

    [Fact]
    public async Task A_script_of_two_projects_is_renamed_with_the_scripts_of_both()
    {
        await using var session = await SkeinSession.OpenAsync(
            SkeinSession.Project("Story"), SkeinSession.Project("Dialogue/Chapter"), SkeinSession.Script("Dialogue/Act1", Act1), SkeinSession.Script("Dialogue/Act2", Act2), SkeinSession.Script("Elsewhere/Far", Far));
        var (act1, act2, far) = (session.ScriptAt("Dialogue/Act1"), session.ScriptAt("Dialogue/Act2"), session.ScriptAt("Elsewhere/Far"));

        // 'Story' includes the three scripts, 'Dialogue/Chapter' the two of its folder
        Assert.Equal([act1, act2, far], YarnRenameService.ScriptsAround(act1));
        Assert.Equal([far, act1, act2], YarnRenameService.ScriptsAround(far));

        Assert.Equal("A node named 'Far' already exists.", YarnRenameService.Rename(act1, "Start", "Far"));
        Assert.Empty(session.Steps);

        Assert.Null(YarnRenameService.Rename(far, "Far", "Away"));
        Assert.Null(YarnRenameService.Rename(act1, "Pier", "Quay"));
        Assert.Equal(Far.Replace("title: Far", "title: Away").Replace("<<jump Pier>>", "<<jump Quay>>"), far.TextBuffer.Text);
        Assert.Contains("<<detour Quay>>", act2.TextBuffer.Text);
        Assert.Equal(["Rename node 'Far' to 'Away'", "Rename node 'Pier' to 'Quay'"], session.Steps);

        // The title a node has is no change
        Assert.Null(YarnRenameService.Rename(act1, "Quay", "Quay"));
        Assert.Equal(2, session.Steps.Count);
    }
}
