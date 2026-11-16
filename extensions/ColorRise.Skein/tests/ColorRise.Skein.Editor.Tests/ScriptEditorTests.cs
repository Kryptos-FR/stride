using ColorRise.Skein.Assets.Compilation;
using ColorRise.Skein.Assets.Text;
using ColorRise.Skein.Editor.Graph;
using ColorRise.Skein.Editor.ViewModels;

namespace ColorRise.Skein.Editor.Tests;

/// <summary>
/// The editor of a script shows the nodes of its text as cards, and what the graph does is a change of the text.
/// </summary>
public sealed class ScriptEditorTests
{
    private const string Act1 =
        "title: Start\nposition: 0,0\n---\nOdile: The lamp's out again.\n<<jump Pier>>\n===\n"
        + "title: Pier\nposition: 340,0\n---\nFisher: Mind the nets.\n-> Ask about the oil\n    Fisher: None left.\n-> Leave\n===\n"
        + "title: Lamp\ntags: quest light\nquest: keeper\n---\nKeeper: It wants oil.\n===\n";

    private const string Act2 = "title: Harbour\n---\nThe boats are in.\n===\n";

    [Fact]
    public async Task The_nodes_of_the_text_are_cards_and_the_jumps_are_links()
    {
        await using var session = await SkeinSession.OpenAsync(SkeinSession.Project("Dialogue/Story"), SkeinSession.Script("Dialogue/Act1", Act1));
        var editor = session.Edit("Dialogue/Act1");

        // The cards are there from the text, before the compiler said anything
        Assert.Equal(["Start", "Pier", "Lamp"], editor.Cards.Select(c => c.Title));
        Assert.Empty(editor.Links);
        Assert.Equal("Odile: The lamp's out again.", editor.Cards[0].Preview);

        await session.Analysis.WhenIdle();

        var (start, pier, lamp) = (editor.Cards[0], editor.Cards[1], editor.Cards[2]);
        var link = Assert.Single(editor.Links);
        Assert.Same(start, link.Source);
        Assert.Same(pier, link.Target);
        Assert.False(link.IsDetour);
        Assert.False(link.IsBroken);
        Assert.Equal(5, link.Line);
        Assert.Same(start.OutSlot, link.SourceSlot);
        Assert.Same(pier.InSlot, link.TargetSlot);
        Assert.Empty(editor.Stubs);
        Assert.Equal([start, pier, lamp], editor.Nodes);

        Assert.Equal(["Odile"], start.Speakers);
        Assert.Contains("The lamp's out again.", start.Preview);
        Assert.Equal(["Ask about the oil", "Leave"], pier.Options);
        Assert.Equal(["quest", "light"], lamp.Tags);
        Assert.Equal(-1, lamp.Complexity);
        Assert.Equal(1, lamp.GroupCount);
        Assert.False(lamp.IsNote);
        Assert.False(lamp.HasError);
        Assert.Empty(editor.Diagnostics);
        Assert.Null(editor.Notice);

        // A node with a place is there; one without is put under the ones that have theirs
        Assert.True(pier.HasPosition);
        Assert.Equal(new YarnGraphPoint(340, 0), pier.Position);
        Assert.False(lamp.HasPosition);
        Assert.Equal(new YarnGraphPoint(0, YarnGraphLayout.RowHeight), lamp.Position);
    }

    [Fact]
    public async Task A_jump_out_of_the_script_leads_to_a_stub_and_a_jump_to_nowhere_is_broken()
    {
        const string text = "title: Start\nposition: 100,50\n---\n<<jump Harbour>>\n<<detour Nowhere>>\n<<jump Harbour>>\n===\n";
        await using var session = await SkeinSession.OpenAsync(SkeinSession.Project("Dialogue/Story"), SkeinSession.Script("Dialogue/Act1", text), SkeinSession.Script("Dialogue/Act2", Act2));
        var editor = session.Edit("Dialogue/Act1");
        await session.Analysis.WhenIdle();

        var start = Assert.Single(editor.Cards);
        Assert.Equal(["Harbour", "Nowhere"], editor.Stubs.Select(s => s.Title));
        var (harbour, nowhere) = (editor.Stubs[0], editor.Stubs[1]);
        Assert.Equal("Dialogue/Act2.yarn", harbour.FileName);
        Assert.Same(session.ScriptAt("Dialogue/Act2"), harbour.Script);
        Assert.False(harbour.IsBroken);
        Assert.Same(start, harbour.Source);
        Assert.Null(nowhere.FileName);
        Assert.Null(nowhere.Script);
        Assert.True(nowhere.IsBroken);
        Assert.Equal([start, harbour, nowhere], editor.Nodes);

        // The stubs sit on the right of their card, one under the other
        Assert.True(harbour.Position.X > start.Position.X);
        Assert.Equal(start.Position.Y, harbour.Position.Y);
        Assert.Equal(harbour.Position.X, nowhere.Position.X);
        Assert.True(nowhere.Position.Y > harbour.Position.Y);

        // Two jumps to one node are one link
        Assert.Equal(2, editor.Links.Count);
        Assert.Same(harbour, editor.Links[0].Target);
        Assert.False(editor.Links[0].IsBroken);
        Assert.False(editor.Links[0].IsDetour);
        Assert.Same(nowhere, editor.Links[1].Target);
        Assert.True(editor.Links[1].IsBroken);
        Assert.True(editor.Links[1].IsDetour);
        Assert.Same(nowhere.InSlot, editor.Links[1].TargetSlot);

        // Opening a stub opens the editor of its script, with the node selected
        editor.OpenTargetCommand.Execute(harbour);
        Assert.True(session.Session.EditorCollection.TryGetAssetEditor<YarnScriptEditorViewModel>(session.ScriptAt("Dialogue/Act2"), out var other));
        Assert.Equal(["Harbour"], other.SelectedCards.Cast<YarnNodeCardViewModel>().Select(c => c.Title));
    }

    [Fact]
    public async Task Adding_nodes_writes_them_at_the_end_of_the_text_one_step_each()
    {
        await using var session = await SkeinSession.OpenAsync(SkeinSession.Project("Dialogue/Story"), SkeinSession.Script("Dialogue/Act1", Act1), SkeinSession.Script("Dialogue/Act2", Act2));
        var editor = session.Edit("Dialogue/Act1");
        var script = session.ScriptAt("Dialogue/Act1");
        await session.Analysis.WhenIdle();
        var lamp = editor.Cards[2];

        editor.AddNodeCommand.Execute(new YarnGraphPoint(100, 200));

        Assert.Equal(Act1 + "title: Node\nposition: 100,200\n---\n===\n", script.TextBuffer.Text);
        Assert.Equal(["Add node"], session.Steps);
        Assert.True(script.IsDirty);
        Assert.Equal(["Start", "Pier", "Lamp", "Node"], editor.Cards.Select(c => c.Title));
        Assert.Equal(new YarnGraphPoint(100, 200), editor.Cards[3].Position);
        Assert.Equal([editor.Cards[3]], editor.SelectedCards);

        // A second node takes a title no node has, in the script or in the rest of the project
        editor.AddNodeCommand.Execute(new YarnGraphPoint(0, 0));
        Assert.Equal("Node2", editor.Cards[4].Title);

        editor.AddNoteCommand.Execute(new YarnGraphPoint(10, 20));
        Assert.EndsWith("title: Note\nposition: 10,20\nstyle: note\n---\n===\n", script.TextBuffer.Text);
        Assert.True(editor.Cards[5].IsNote);
        Assert.Equal(["Add node", "Add node", "Add note"], session.Steps);

        session.UndoRedo.Undo();
        session.UndoRedo.Undo();
        session.UndoRedo.Undo();
        Assert.Equal(Act1, script.TextBuffer.Text);
        Assert.Equal(["Start", "Pier", "Lamp"], editor.Cards.Select(c => c.Title));
        Assert.Same(lamp, editor.Cards[2]);
        Assert.Empty(editor.SelectedCards);

        // A node added to the group of a node makes the node a node group
        var place = lamp.Position;
        var member = editor.AddToGroup(lamp);

        var before = YarnDocument.Parse(Act1);
        var expected = YarnTextEdit.Apply(Act1,
        [
            .. YarnTextPatcher.SetHeaders(before, before.Nodes[2], "when", ["always"]),
            .. YarnTextPatcher.AddNode(before, "Lamp", place.X + 40, place.Y + 40, [("when", "always")]),
        ]);
        Assert.Equal(expected, script.TextBuffer.Text);
        Assert.Equal("Add to node group", session.Steps[^1]);
        Assert.NotNull(member);
        Assert.Same(member, editor.Cards[3]);
        Assert.Equal((2, 0, 2, 1), (lamp.GroupCount, lamp.GroupIndex, member.GroupCount, member.GroupIndex));
        Assert.Equal(["always"], lamp.When);
        Assert.Equal(["always"], member.When);

        // The compiler takes the two for a node group, and says the new one has nothing in it yet
        await session.Analysis.WhenIdle();
        Assert.Equal(["YS0033"], editor.Diagnostics.Select(d => d.Code));
        Assert.True(lamp.Complexity >= 0);
    }

    [Fact]
    public async Task What_the_graph_does_to_nodes_is_one_step_of_the_text_each()
    {
        await using var session = await SkeinSession.OpenAsync(SkeinSession.Project("Dialogue/Story"), SkeinSession.Script("Dialogue/Act1", Act1));
        var editor = session.Edit("Dialogue/Act1");
        var script = session.ScriptAt("Dialogue/Act1");
        await session.Analysis.WhenIdle();
        var (start, pier, lamp) = (editor.Cards[0], editor.Cards[1], editor.Cards[2]);
        var document = YarnDocument.Parse(Act1);

        string Patched(params IReadOnlyList<YarnTextEdit>[] edits) => YarnTextEdit.Apply(Act1, [.. edits.SelectMany(e => e).OrderBy(e => e.Offset)]);

        void UndoTo(string step)
        {
            Assert.Equal(step, session.Steps[^1]);
            var steps = session.Steps.Count;
            session.UndoRedo.Undo();
            Assert.Equal(Act1, script.TextBuffer.Text);
            Assert.Equal(["Start", "Pier", "Lamp"], editor.Cards.Select(c => c.Title));
            Assert.Same(pier, editor.Cards[1]);
            Assert.Equal(steps, session.Steps.Count);
        }

        // Moving two cards is one step that writes two places
        editor.MoveCommand.Execute(new List<YarnCardMove> { new(start, new YarnGraphPoint(-20, 15)), new(lamp, new YarnGraphPoint(700, 300)) });
        Assert.Equal(Patched(YarnTextPatcher.SetPosition(document, document.Nodes[0], -20, 15), YarnTextPatcher.SetPosition(document, document.Nodes[2], 700, 300)), script.TextBuffer.Text);
        Assert.Contains("position: -20,15", script.TextBuffer.Text);
        Assert.Contains("title: Lamp\nposition: 700,300", script.TextBuffer.Text);
        Assert.Equal(new YarnGraphPoint(700, 300), lamp.Position);
        Assert.True(lamp.HasPosition);
        UndoTo("Move nodes");
        Assert.False(lamp.HasPosition);

        // A link is a jump at the end of the node, and shows once the compiler read it
        editor.LinkCommand.Execute(new YarnLinkRequest(lamp, start));
        Assert.Equal(Patched(YarnTextPatcher.AppendJump(document, document.Nodes[2], "Start")), script.TextBuffer.Text);
        await session.Analysis.WhenIdle();
        Assert.Contains(editor.Links, l => l.Source == lamp && l.Target == start);
        UndoTo("Link nodes");
        await session.Analysis.WhenIdle();
        Assert.Single(editor.Links);

        // A copy takes a title of its own and a place aside, and is selected
        editor.SelectedCards.Add(pier);
        editor.DuplicateCommand.Execute(null);
        Assert.Equal(Patched(YarnTextPatcher.DuplicateNode(document, document.Nodes[1], "Pier_copy", (380, 40))), script.TextBuffer.Text);
        Assert.Equal(["Start", "Pier", "Pier_copy", "Lamp"], editor.Cards.Select(c => c.Title));
        Assert.Equal([editor.Cards[2]], editor.SelectedCards);
        UndoTo("Duplicate node");

        editor.SelectedCards.Clear();
        editor.SelectedCards.AddRange([start, lamp]);
        editor.SetColorCommand.Execute("red");
        Assert.Equal(Patched(YarnTextPatcher.SetHeader(document, document.Nodes[0], "color", "red"), YarnTextPatcher.SetHeader(document, document.Nodes[2], "color", "red")), script.TextBuffer.Text);
        Assert.Equal(["red", null, "red"], editor.Cards.Select(c => c.Color));
        editor.SetColorCommand.Execute(null);
        Assert.Equal(Act1, script.TextBuffer.Text);
        Assert.Equal(["Set color", "Set color"], session.Steps.TakeLast(2));

        // The selection is deleted together; the link from a deleted node goes with it at once
        editor.DeleteSelectionCommand.Execute(null);
        Assert.Equal(Patched(YarnTextPatcher.DeleteNode(document, document.Nodes[0]), YarnTextPatcher.DeleteNode(document, document.Nodes[2])), script.TextBuffer.Text);
        Assert.Equal([pier], editor.Cards);
        Assert.Empty(editor.Links);
        Assert.Empty(editor.SelectedCards);
        UndoTo("Delete nodes");

        // The nodes that come back are new cards, with the links of the text
        Assert.NotSame(start, editor.Cards[0]);
        // Laying out gives every node a place, along the jumps
        await session.Analysis.WhenIdle();
        editor.LayoutCommand.Execute(null);
        Assert.Equal(Patched(
            YarnTextPatcher.SetPosition(document, document.Nodes[0], 0, 0),
            YarnTextPatcher.SetPosition(document, document.Nodes[1], YarnGraphLayout.ColumnWidth, 0),
            YarnTextPatcher.SetPosition(document, document.Nodes[2], 0, YarnGraphLayout.RowHeight)), script.TextBuffer.Text);
        UndoTo("Lay out nodes");
    }

    [Fact]
    public async Task A_script_that_does_not_compile_keeps_its_cards_and_its_links()
    {
        await using var session = await SkeinSession.OpenAsync(SkeinSession.Project("Dialogue/Story"), SkeinSession.Script("Dialogue/Act1", Act1));
        var editor = session.Edit("Dialogue/Act1");
        var script = session.ScriptAt("Dialogue/Act1");
        await session.Analysis.WhenIdle();
        var cards = editor.Cards.ToList();
        var link = Assert.Single(editor.Links);

        // The body of the second node gets a command that never ends
        var at = Act1.IndexOf("Fisher: Mind", StringComparison.Ordinal);
        script.TextBuffer.Replace(at, 0, "<<if $oil ==\n", "Typing");
        await session.Analysis.WhenIdle();

        Assert.Equal(cards, editor.Cards);
        Assert.Same(link, Assert.Single(editor.Links));
        Assert.Contains(editor.Diagnostics, d => d.Severity == YarnSeverity.Error && d.File == "Dialogue/Act1.yarn");
        Assert.Equal([false, true, false], editor.Cards.Select(c => c.HasError));
        Assert.Equal(["Ask about the oil", "Leave"], cards[1].Options);

        // The graph still changes the text
        editor.Move([new YarnCardMove(cards[2], new YarnGraphPoint(5, 5))]);
        Assert.Contains("title: Lamp\nposition: 5,5\n", script.TextBuffer.Text);
        Assert.Equal(new YarnGraphPoint(5, 5), cards[2].Position);
        session.UndoRedo.Undo();

        session.UndoRedo.Undo();
        await session.Analysis.WhenIdle();

        Assert.Empty(editor.Diagnostics);
        Assert.All(editor.Cards, c => Assert.False(c.HasError));
        Assert.Equal(cards, editor.Cards);
    }

    [Fact]
    public async Task A_card_stays_the_same_object_while_its_node_is_typed()
    {
        await using var session = await SkeinSession.OpenAsync(SkeinSession.Project("Dialogue/Story"), SkeinSession.Script("Dialogue/Act1", Act1));
        var editor = session.Edit("Dialogue/Act1");
        var script = session.ScriptAt("Dialogue/Act1");
        await session.Analysis.WhenIdle();
        var cards = editor.Cards.ToList();
        editor.SelectedCards.Add(cards[1]);
        var changes = new List<string?>();
        cards[1].PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        // The title of the second node, typed one letter after the other
        var at = Act1.IndexOf("Pier\nposition", StringComparison.Ordinal) + "Pier".Length;
        script.TextBuffer.Replace(at, 0, "_", "Typing");
        script.TextBuffer.Replace(at + 1, 0, "2", "Typing");

        Assert.Equal(cards, editor.Cards);
        Assert.Equal("Pier_2", cards[1].Title);
        Assert.Equal([cards[1]], editor.SelectedCards);
        Assert.Equal(["Title", "Title"], changes);
        Assert.Equal(["Fisher"], cards[1].Speakers);

        // The link to the node stays on its card until the compiler reads the text, which no longer has a node 'Pier'
        Assert.Same(cards[1], Assert.Single(editor.Links).Target);
        await session.Analysis.WhenIdle();
        var stub = Assert.Single(editor.Stubs);
        Assert.Equal("Pier", stub.Title);
        Assert.True(stub.IsBroken);
        Assert.Same(stub, Assert.Single(editor.Links).Target);

        // A new node between two others does not take the card of one of them
        script.TextBuffer.Replace(0, 0, "title: First\n---\n===\n", "Paste");
        Assert.Equal(["First", "Start", "Pier_2", "Lamp"], editor.Cards.Select(c => c.Title));
        Assert.Equal(cards, editor.Cards.Skip(1));
    }

    [Fact]
    public async Task A_detour_is_a_link_from_the_detour_slot_of_its_card()
    {
        await using var session = await SkeinSession.OpenAsync(SkeinSession.Project("Dialogue/Story"), SkeinSession.Script("Dialogue/Act1", Act1));
        var editor = session.Edit("Dialogue/Act1");
        var script = session.ScriptAt("Dialogue/Act1");
        await session.Analysis.WhenIdle();
        var (start, lamp) = (editor.Cards[0], editor.Cards[2]);

        editor.LinkCommand.Execute(new YarnLinkRequest(lamp, start, IsDetour: true));

        Assert.Equal(Act1[..^4] + "<<detour Start>>\n===\n", script.TextBuffer.Text);
        Assert.Equal("Link nodes", session.Steps[^1]);
        await session.Analysis.WhenIdle();
        var detour = Assert.Single(editor.Links, l => l.Source == lamp);
        Assert.True(detour.IsDetour);
        Assert.Same(start, detour.Target);
        Assert.Same(lamp.DetourSlot, detour.SourceSlot);
        Assert.NotSame(lamp.OutSlot, lamp.DetourSlot);
        Assert.Same(start.InSlot, detour.TargetSlot);
    }

    [Fact]
    public async Task Moving_a_card_writes_the_place_of_the_cards_that_had_none()
    {
        await using var session = await SkeinSession.OpenAsync(SkeinSession.Project("Dialogue/Story"), SkeinSession.Script("Dialogue/Act1", Act1));
        var editor = session.Edit("Dialogue/Act1");
        var script = session.ScriptAt("Dialogue/Act1");
        await session.Analysis.WhenIdle();
        var (start, lamp) = (editor.Cards[0], editor.Cards[2]);
        var shown = lamp.Position;

        // The place of 'Lamp' is worked out from the cards that have one: it is written with the move, so that
        // 'Lamp' stays where the user sees it
        editor.Move([new YarnCardMove(start, new YarnGraphPoint(0, 900))]);

        Assert.Contains("title: Start\nposition: 0,900\n", script.TextBuffer.Text);
        Assert.Contains($"title: Lamp\nposition: {shown.X},{shown.Y}\n", script.TextBuffer.Text);
        Assert.Equal(shown, lamp.Position);
        Assert.True(lamp.HasPosition);
        Assert.Equal(["Move node"], session.Steps);

        session.UndoRedo.Undo();
        Assert.Equal(Act1, script.TextBuffer.Text);

        // A move of no card of the script writes nothing
        editor.Move([]);
        Assert.Equal(Act1, script.TextBuffer.Text);
        Assert.Equal(["Move node"], session.Steps);
    }

    [Fact]
    public async Task The_messages_of_the_compiler_lead_to_their_place_in_the_text()
    {
        const string broken = "title: Harbour\n---\n<<jump Nowhere>>\nThe boats are in.\n===\n";
        await using var session = await SkeinSession.OpenAsync(SkeinSession.Project("Dialogue/Story"), SkeinSession.Script("Dialogue/Act1", Act1), SkeinSession.Script("Dialogue/Act2", broken));
        var editor = session.Edit("Dialogue/Act1");
        var script = session.ScriptAt("Dialogue/Act1");
        Assert.False(editor.IsAnalysisCurrent);
        await session.Analysis.WhenIdle();

        // The messages of the project are all there; the ones of the script are apart
        Assert.True(editor.IsAnalysisCurrent);
        var message = Assert.Single(editor.Diagnostics);
        Assert.Equal("Dialogue/Act2.yarn", message.File);
        Assert.Empty(editor.ScriptDiagnostics);

        // A message of another script opens the editor of that script, which is asked for the place
        await editor.GoTo(message);
        Assert.Null(editor.TakeRequestedPlace());
        Assert.True(session.Session.EditorCollection.TryGetAssetEditor<YarnScriptEditorViewModel>(session.ScriptAt("Dialogue/Act2"), out var other));
        Assert.Equal(new YarnTextPlace(message.Line, Math.Max(1, message.Column)), other.TakeRequestedPlace());
        Assert.Null(other.TakeRequestedPlace());
        Assert.Equal(3, message.Line);

        // A message of the script itself asks its own text pane
        var at = Act1.IndexOf("Fisher: Mind", StringComparison.Ordinal);
        script.TextBuffer.Replace(at, 0, "<<if $oil ==\n", "Typing");
        Assert.False(editor.IsAnalysisCurrent);
        await session.Analysis.WhenIdle();
        Assert.True(editor.IsAnalysisCurrent);
        Assert.NotEmpty(editor.ScriptDiagnostics);
        Assert.All(editor.ScriptDiagnostics, d => Assert.Equal("Dialogue/Act1.yarn", d.File));
        Assert.Equal(editor.ScriptDiagnostics, editor.Diagnostics.Take(editor.ScriptDiagnostics.Count));

        var asked = 0;
        editor.PlaceRequested += () => asked++;
        var mine = editor.ScriptDiagnostics[0];
        await editor.GoTo(mine);
        Assert.Equal(1, asked);
        Assert.Equal(new YarnTextPlace(mine.Line, Math.Max(1, mine.Column)), editor.TakeRequestedPlace());
    }

    [Fact]
    public async Task A_title_leads_to_its_node_in_the_script_or_in_another_of_the_project()
    {
        await using var session = await SkeinSession.OpenAsync(SkeinSession.Project("Dialogue/Story"), SkeinSession.Script("Dialogue/Act1", Act1), SkeinSession.Script("Dialogue/Act2", Act2));
        var editor = session.Edit("Dialogue/Act1");
        await session.Analysis.WhenIdle();

        Assert.True(await editor.GoToNode("Lamp"));
        Assert.Equal([editor.Cards[2]], editor.SelectedCards);

        Assert.True(await editor.GoToNode("Harbour"));
        Assert.True(session.Session.EditorCollection.TryGetAssetEditor<YarnScriptEditorViewModel>(session.ScriptAt("Dialogue/Act2"), out var other));
        Assert.Equal(["Harbour"], other.SelectedCards.Cast<YarnNodeCardViewModel>().Select(c => c.Title));
        Assert.Equal([editor.Cards[2]], editor.SelectedCards);

        Assert.False(await editor.GoToNode("Nowhere"));

        // A place of the text is in the node of a card, from the first header to the end of the node
        Assert.Same(editor.Cards[0], editor.CardAt(0));
        Assert.Same(editor.Cards[1], editor.CardAt(Act1.IndexOf("Fisher", StringComparison.Ordinal)));
        Assert.Same(editor.Cards[2], editor.CardAt(Act1.IndexOf("title: Lamp", StringComparison.Ordinal)));
        Assert.Null(editor.CardAt(Act1.Length + 5));
    }

    [Fact]
    public async Task A_script_in_no_project_or_in_several_says_so()
    {
        await using var session = await SkeinSession.OpenAsync(
            SkeinSession.Project("Dialogue/Story"), SkeinSession.Project("Dialogue/Other"), SkeinSession.Script("Dialogue/Act1", Act1), SkeinSession.Script("Alone/Lost", Act1));
        var lost = session.Edit("Alone/Lost");
        var act1 = session.Edit("Dialogue/Act1");
        await session.Analysis.WhenIdle();

        // A script no project includes is compiled alone
        Assert.Equal("This script is in no Yarn project.", lost.Notice);
        Assert.Single(lost.Links);
        Assert.Empty(lost.Diagnostics);

        // A script of two projects is checked with the first, and each of the two has its nodes: nothing is reported twice
        Assert.Equal("This script is in 2 Yarn projects: it is checked with 'Dialogue/Other'.", act1.Notice);
        Assert.Single(act1.Links);
        Assert.Empty(act1.Diagnostics);
    }

    [Fact]
    public async Task A_title_a_node_cannot_have_is_refused_with_the_reason()
    {
        await using var session = await SkeinSession.OpenAsync(SkeinSession.Project("Dialogue/Story"), SkeinSession.Script("Dialogue/Act1", Act1), SkeinSession.Script("Dialogue/Act2", Act2));
        var editor = session.Edit("Dialogue/Act1");
        var script = session.ScriptAt("Dialogue/Act1");
        var start = editor.Cards[0];

        Assert.False(editor.Rename(start, "Pier"));
        Assert.Equal("A node named 'Pier' already exists.", editor.Problem);

        // Also a title of another script of the project
        editor.RenameCommand.Execute(new YarnRenameRequest(start, "Harbour"));
        Assert.Equal("A node named 'Harbour' already exists.", editor.Problem);

        Assert.False(editor.Rename(start, "Not a title"));
        Assert.Equal("'Not a title' cannot be a title: a title is one word of letters, digits and '_' that does not start with a digit.", editor.Problem);

        Assert.False(editor.Rename(start, "  "));
        Assert.Equal("A node needs a title.", editor.Problem);

        Assert.Equal(Act1, script.TextBuffer.Text);
        Assert.Empty(session.Steps);
        Assert.False(script.IsDirty);

        // What is done next takes the reason away
        Assert.True(editor.Rename(start, " Dawn "));
        Assert.Null(editor.Problem);
        Assert.Equal("Dawn", start.Title);
    }

    [Fact]
    public async Task Cards_are_grouped_by_a_header_and_searched()
    {
        await using var session = await SkeinSession.OpenAsync(SkeinSession.Project("Dialogue/Story"), SkeinSession.Script("Dialogue/Act1", Act1));
        var editor = session.Edit("Dialogue/Act1");
        var script = session.ScriptAt("Dialogue/Act1");
        await session.Analysis.WhenIdle();

        Assert.Equal(["tags", "quest"], editor.GroupableHeaders);
        Assert.All(editor.Cards, c => Assert.Null(c.GroupValue));

        editor.GroupBy = "quest";
        Assert.Equal([null, null, "keeper"], editor.Cards.Select(c => c.GroupValue));

        // A header typed in the text is there to group by, and its value is on the card
        var at = Act1.IndexOf("position: 340,0", StringComparison.Ordinal);
        script.TextBuffer.Replace(at, 0, "quest: nets\nact: 1\n", "Typing");
        Assert.Equal(["quest", "act", "tags"], editor.GroupableHeaders);
        Assert.Equal([null, "nets", "keeper"], editor.Cards.Select(c => c.GroupValue));

        editor.GroupBy = null;
        Assert.All(editor.Cards, c => Assert.Null(c.GroupValue));

        // The search is in titles, tags, speakers and text, whatever the case
        Assert.All(editor.Cards, c => Assert.False(c.IsFilteredOut));
        editor.Search = "lamp";
        Assert.Equal([false, true, false], editor.Cards.Select(c => c.IsFilteredOut));
        editor.Search = "LIGHT";
        Assert.Equal([true, true, false], editor.Cards.Select(c => c.IsFilteredOut));
        editor.Search = "fisher";
        Assert.Equal([true, false, true], editor.Cards.Select(c => c.IsFilteredOut));
        editor.Search = "the nets";
        Assert.Equal([true, false, true], editor.Cards.Select(c => c.IsFilteredOut));

        // A node typed while a search is on is searched too
        script.TextBuffer.Replace(script.TextBuffer.Text.Length, 0, "title: Nets\n---\n===\n", "Typing");
        Assert.Equal([true, false, true, true], editor.Cards.Select(c => c.IsFilteredOut));

        editor.Search = "";
        Assert.All(editor.Cards, c => Assert.False(c.IsFilteredOut));
    }
}
