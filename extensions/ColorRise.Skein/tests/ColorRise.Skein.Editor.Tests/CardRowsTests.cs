using ColorRise.Skein.Assets.Text;
using ColorRise.Skein.Editor.Graph;
using ColorRise.Skein.Editor.ViewModels;

namespace ColorRise.Skein.Editor.Tests;

/// <summary>
/// A card shows the statements of its node as rows: its lines and options are edited in place, and the jumps under an
/// option or a branch of a block leave from its row.
/// </summary>
public sealed class CardRowsTests
{
    private const string Act1 =
        "title: Start\nposition: 0,0\n---\n"
        + "Odile: The post is late. #line:post\n"
        + "-> Ask about the boat <<if $boat_seen>> #line:ask\n"
        + "    Fisher: Gone. #line:gone\n"
        + "    <<jump Pier>>\n"
        + "-> Leave\n"
        + "<<if $boat_seen>>\n"
        + "    <<detour Pier>>\n"
        + "<<else>>\n"
        + "    <<jump {$next}>>\n"
        + "<<endif>>\n"
        + "<<set $boat_seen to true>>\n"
        + "===\n"
        + "title: Pier\nposition: 340,0\n---\nFisher: Mind the nets.\n<<stop>>\n===\n"
        + "title: Shore\nposition: 680,0\n---\nThe tide is out.\n<<jump Pier>>\n===\n";

    private static async Task<(SkeinSession Session, YarnScriptEditorViewModel Editor)> Open(string text = Act1)
    {
        var session = await SkeinSession.OpenAsync(SkeinSession.Project("Dialogue/Story"), SkeinSession.Script("Dialogue/Act1", text));
        var editor = session.Edit("Dialogue/Act1");
        await session.Analysis.WhenIdle();
        return (session, editor);
    }

    [Fact]
    public async Task A_card_has_a_row_for_each_statement_of_its_node()
    {
        var (session, editor) = await Open();
        await using var _ = session;
        var start = editor.Cards[0];

        Assert.Equal(
            [
                (YarnStatementKind.Line, "Odile", "The post is late.", null, 0, false),
                (YarnStatementKind.Option, null, "Ask about the boat", "if $boat_seen", 0, true),
                (YarnStatementKind.Line, "Fisher", "Gone.", null, 1, false),
                (YarnStatementKind.Jump, null, "jump Pier", null, 1, false),
                (YarnStatementKind.Option, null, "Leave", null, 0, true),
                (YarnStatementKind.If, null, "if $boat_seen", null, 0, true),
                (YarnStatementKind.Detour, null, "detour Pier", null, 1, false),
                (YarnStatementKind.Else, null, "else", null, 0, true),
                (YarnStatementKind.Jump, null, "jump {$next}", null, 1, false),
                (YarnStatementKind.Command, null, "set $boat_seen to true", null, 0, false),
            ],
            start.Rows.Select(r => (r.Kind, r.Speaker, r.Display, r.Condition, r.Depth, r.HasConnector)));
        Assert.Equal([true, true, true, false, true, false, false, false, false, false], start.Rows.Select(r => r.IsEditable));
        Assert.True(start.AddRow.IsAdd);
        Assert.True(start.AddRow.IsEditable);

        // A row stays the same object while the text changes around it, and takes the statement of the new text
        var first = start.Rows[0];
        var script = session.ScriptAt("Dialogue/Act1");
        script.TextBuffer.Replace(script.TextBuffer.Text.IndexOf("late", StringComparison.Ordinal), 4, "early", "Typing");
        Assert.Same(first, start.Rows[0]);
        Assert.Equal("The post is early.", first.Display);

        // A card that folds shows its preview, and is shorter
        var unfolded = start.Height;
        editor.FoldCards = true;
        Assert.All(editor.Cards, c => Assert.True(c.IsFolded));
        Assert.True(start.Height < unfolded);
        start.IsFolded = false;
        Assert.Equal(unfolded, start.Height);
    }

    [Fact]
    public async Task A_line_or_an_option_is_edited_on_its_card_as_one_step_and_keeps_its_id()
    {
        var (session, editor) = await Open();
        await using var _ = session;
        var script = session.ScriptAt("Dialogue/Act1");
        var (start, pier) = (editor.Cards[0], editor.Cards[1]);

        // The box has the line as written; another box that is open closes without writing
        Assert.True(editor.BeginEdit(pier.Rows[0]));
        Assert.True(editor.BeginEdit(start.Rows[0]));
        Assert.False(pier.Rows[0].IsEditing);
        Assert.True(start.Rows[0].IsEditing);
        Assert.Equal("Odile: The post is late.", start.Rows[0].EditText);

        start.Rows[0].EditText = "Odile: The post is late, #again.";
        Assert.True(editor.CommitEdit(start.Rows[0]));
        Assert.False(start.Rows[0].IsEditing);
        Assert.Equal(Act1.Replace("Odile: The post is late. #line:post", "Odile: The post is late, \\#again. #line:post"), script.TextBuffer.Text);
        Assert.Equal(["Edit line"], session.Steps);
        Assert.Equal("The post is late, \\#again.", start.Rows[0].Display);

        // An option keeps its condition and its id
        editor.BeginEdit(start.Rows[1]);
        start.Rows[1].EditText = "Ask about the boat again";
        editor.CommitEdit(start.Rows[1]);
        Assert.Contains("-> Ask about the boat again <<if $boat_seen>> #line:ask\n", script.TextBuffer.Text);
        Assert.Equal("Edit option", session.Steps[^1]);

        // The same text, or a box closed with Esc, writes nothing
        editor.BeginEdit(start.Rows[2]);
        Assert.False(editor.CommitEdit(start.Rows[2]));
        editor.BeginEdit(start.Rows[2]);
        start.Rows[2].EditText = "Fisher: Something else.";
        editor.CancelEdit(start.Rows[2]);
        Assert.False(editor.CommitEdit(start.Rows[2]));
        Assert.Equal(2, session.Steps.Count);

        // A line left empty goes; an option left empty stays, and the editor says why
        editor.BeginEdit(start.Rows[2]);
        start.Rows[2].EditText = " ";
        Assert.True(editor.CommitEdit(start.Rows[2]));
        Assert.DoesNotContain("Fisher: Gone.", script.TextBuffer.Text);
        Assert.Equal("Delete line", session.Steps[^1]);
        editor.BeginEdit(start.Rows[3]);
        start.Rows[3].EditText = "";
        Assert.False(editor.CommitEdit(start.Rows[3]));
        Assert.Contains("needs a text", editor.Problem);

        // A text that would be something else in Yarn is refused
        editor.BeginEdit(start.Rows[0]);
        start.Rows[0].EditText = "-> Not a line";
        Assert.False(editor.CommitEdit(start.Rows[0]));
        Assert.Contains("'->'", editor.Problem);

        // A command does not edit on the card: it shows in the text
        Assert.False(editor.BeginEdit(start.Rows[^1]));
        YarnTextPlace? place = null;
        editor.PlaceRequested += () => place = editor.TakeRequestedPlace();
        editor.ShowInText(start.Rows[^1]);
        Assert.Equal([start], editor.SelectedCards);
        Assert.Equal(new YarnTextPlace(13, 3), place);

        // Each edit is one undo step
        session.UndoRedo.Undo();
        session.UndoRedo.Undo();
        session.UndoRedo.Undo();
        Assert.Equal(Act1, script.TextBuffer.Text);
    }

    [Fact]
    public async Task The_row_that_adds_a_line_starts_with_the_speaker_of_the_last_line()
    {
        var (session, editor) = await Open();
        await using var _ = session;
        var script = session.ScriptAt("Dialogue/Act1");
        var (start, shore) = (editor.Cards[0], editor.Cards[2]);

        Assert.True(editor.BeginEdit(start.AddRow));
        Assert.Equal("Fisher: ", start.AddRow.EditText);

        // The speaker alone is no line
        Assert.False(editor.CommitEdit(start.AddRow));
        Assert.Empty(session.Steps);

        editor.BeginEdit(start.AddRow);
        start.AddRow.EditText = "Fisher: Mind the boat.";
        Assert.True(editor.CommitEdit(start.AddRow));
        Assert.Contains("<<set $boat_seen to true>>\nFisher: Mind the boat.\n===\n", script.TextBuffer.Text);
        Assert.Equal(["Add line"], session.Steps);
        Assert.Equal("Mind the boat.", start.Rows[^1].Display);

        // A node that ends with a jump has the line before it; a node without a speaker starts the box empty
        editor.BeginEdit(shore.AddRow);
        Assert.Equal("", shore.AddRow.EditText);
        shore.AddRow.EditText = "The gulls are back.";
        editor.CommitEdit(shore.AddRow);
        Assert.Contains("The tide is out.\nThe gulls are back.\n<<jump Pier>>\n", script.TextBuffer.Text);
    }

    [Fact]
    public async Task A_jump_under_an_option_or_a_branch_leaves_from_its_row()
    {
        var (session, editor) = await Open();
        await using var _ = session;
        var (start, pier, shore) = (editor.Cards[0], editor.Cards[1], editor.Cards[2]);

        var links = editor.Links.Where(l => l.Source == start).ToList();
        Assert.Equal(3, links.Count);

        // The jump under the option leaves from the option
        var ask = links.Single(l => !l.IsDetour && l.Target == pier);
        Assert.Same(start.Rows[1], ask.Row);
        Assert.Same(start.Rows[1].OutSlot, ask.SourceSlot);
        Assert.Same(start.Rows[1], ask.SourceSlot.Row);

        // The detour under the if leaves from the if
        var detour = links.Single(l => l.IsDetour);
        Assert.Same(start.Rows[5], detour.Row);

        // A jump to an expression leads to a stub that says it, from the else
        var computed = links.Single(l => l.Target is YarnStubCardViewModel);
        var stub = (YarnStubCardViewModel)computed.Target;
        Assert.True(stub.IsComputed);
        Assert.False(stub.IsBroken);
        Assert.Equal("{$next}", stub.Title);
        Assert.Same(start.Rows[7], computed.Row);

        // A jump at the top of a node leaves from the card
        var plain = Assert.Single(editor.Links, l => l.Source == shore);
        Assert.Null(plain.Row);
        Assert.Same(shore.OutSlot, plain.SourceSlot);

        // Where the dialogue ends
        Assert.True(pier.IsEnd);
        Assert.False(start.IsEnd);
        Assert.False(shore.IsEnd);
    }

    [Fact]
    public async Task A_link_dragged_from_a_row_adds_a_jump_under_it()
    {
        var (session, editor) = await Open();
        await using var _ = session;
        var script = session.ScriptAt("Dialogue/Act1");
        var (start, shore) = (editor.Cards[0], editor.Cards[2]);

        editor.LinkCommand.Execute(new YarnLinkRequest(start, shore, Row: start.Rows[4]));

        Assert.Contains("-> Leave\n    <<jump Shore>>\n<<if $boat_seen>>", script.TextBuffer.Text);
        Assert.Equal(["Link nodes"], session.Steps);

        await session.Analysis.WhenIdle();
        var link = Assert.Single(editor.Links, l => l.Source == start && l.Target == shore);
        Assert.Same(start.Rows[4], link.Row);
    }
}
