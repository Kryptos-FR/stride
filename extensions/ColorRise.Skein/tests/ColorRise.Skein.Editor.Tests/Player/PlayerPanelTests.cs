using ColorRise.Skein.Assets;
using ColorRise.Skein.Editor.Player;
using ColorRise.Skein.Editor.Text;
using Stride.Core.Assets.Editor.Panels;
using Stride.Core.Assets.Editor.Testing;

namespace ColorRise.Skein.Editor.Tests.Player;

/// <summary>
/// The dialogue player and timeline panels: what they show of the run of the session, and what they ask of it.
/// </summary>
public sealed class PlayerPanelTests
{
    private const string Lamp = """
        title: Start
        ---
        <<declare $gold = 5 as number>>
        /// Whether the purse is heavy
        <<declare $rich = $gold > 10>>
        <<fade_in 1>>
        Odile: The tide is out. #line:tide
        <<if has_item("oil")>>
            Odile: Your lamp is full.
        <<endif>>
        -> Take the lamp #line:take
            <<set $lamp = true>>
            <<jump Pier>>
        -> Leave it <<if $gold > 100>> #line:leave
            Odile: As you like.
        ===
        title: Pier
        ---
        => Odile: Mind the oil.
        => Odile: Mind the wick. <<if $rich>>
        Narrator: She leaves.
        ===

        """;

    private static async DispatcherTask<SkeinSession> OpenAsync()
        => await SkeinSession.OpenAsync(SkeinSession.Project("Dialogue/Story"), SkeinSession.Script("Dialogue/Act1", Lamp));

    private static T Panel<T>(SkeinSession session, PanelDescriptor descriptor)
        where T : class
        => Assert.IsType<T>(descriptor.CreateViewModel(new PanelContext { Descriptor = descriptor, Session = session.Session, ServiceProvider = session.Session.ServiceProvider }));

    [Fact]
    public async Task A_variable_of_an_enum_shows_its_case_and_takes_one()
    {
        await using var session = await SkeinSession.OpenAsync(SkeinSession.Project("Dialogue/Story"), SkeinSession.Script("Dialogue/Act1", """
            title: Start
            ---
            <<enum Act>>
                <<case Arrival>>
                <<case Wreck>>
            <<endenum>>
            <<declare $act = Act.Arrival>>
            Odile: The tide is out.
            Odile: Good night.
            ===

            """));
        var panel = Panel<DialoguePlayerViewModel>(session, DialoguePlayerViewModel.Panel);
        panel.IsShown = true;
        await session.Analysis.WhenIdle();
        await panel.Start();

        var act = Assert.Single(panel.Variables);
        Assert.Equal(("$act", "Act", "Arrival", "Arrival, Wreck"), (act.Name, act.Type, act.Value, act.Cases));

        act.Value = "Wreck";
        Assert.Equal("", act.Error);
        Assert.Equal("$act = Act.Wreck", panel.Transcript[^1].Text);
        Assert.Equal("Wreck", panel.Variables[0].Value);

        // The value of a case is welcome too, and shows as the case
        act = panel.Variables[0];
        act.Value = "0";
        Assert.Equal("", act.Error);
        Assert.Equal("Arrival", panel.Variables[0].Value);

        act = panel.Variables[0];
        act.Value = "Dusk";
        Assert.StartsWith("$act is one of Arrival, Wreck (Act)", act.Error);
    }

    [Fact]
    public async Task The_player_shows_the_run_at_its_cursor_and_drives_it()
    {
        await using var session = await OpenAsync();
        var panel = Panel<DialoguePlayerViewModel>(session, DialoguePlayerViewModel.Panel);

        // Shown, the panel picks the only project and its first node
        panel.IsShown = true;
        await session.Analysis.WhenIdle();
        Assert.Equal(session.ProjectAt("Dialogue/Story"), panel.SelectedProject);
        Assert.Equal(["Pier", "Start"], panel.Nodes);
        Assert.Equal("Start", panel.SelectedNode);
        Assert.False(panel.IsPlaying);
        Assert.True(panel.StartCommand.IsEnabled);
        Assert.False(panel.ContinueCommand.IsEnabled);

        await panel.Start();

        Assert.True(panel.IsPlaying);
        Assert.Equal(
            [(PlayerStepKind.NodeEnter, null, "Start"), (PlayerStepKind.Command, null, "<<fade_in 1>>"), (PlayerStepKind.Line, "Odile", "The tide is out.")],
            panel.Transcript.Select(t => (t.Kind, t.Speaker, t.Text)));
        Assert.True(panel.ContinueCommand.IsEnabled);
        Assert.Empty(panel.Options);

        // The variables of the scripts with their values; a smart variable shows its expression and cannot be set
        Assert.Equal(["$gold", "$lamp", "$rich"], panel.Variables.Select(v => v.Name));
        var (gold, lamp, rich) = (panel.Variables[0], panel.Variables[1], panel.Variables[2]);
        Assert.Equal(("number", "5", false), (gold.Type, gold.Value, gold.IsSmart));
        Assert.Equal(("bool", "false"), (lamp.Type, lamp.Value));
        Assert.Equal((true, "$gold > 10", "Whether the purse is heavy"), (rich.IsSmart, rich.Value, rich.Description));

        // A function of the game is asked, with its type
        await panel.Continue();
        var question = panel.Question!;
        Assert.Equal(("has_item(\"oil\")", "bool", true), (question.Call, question.ReturnType, question.IsPending));
        Assert.False(panel.ContinueCommand.IsEnabled);
        Assert.Equal("has_item(\"oil\") gives true or false.", question.Give("maybe"));
        Assert.NotNull(panel.Question);
        Assert.Null(question.Give("true"));
        Assert.Null(panel.Question);
        Assert.Equal("Your lamp is full.", panel.Transcript[^1].Text);
        Assert.Equal((PlayerStepKind.Function, "has_item(\"oil\") → true"), (panel.Transcript[^2].Kind, panel.Transcript[^2].Text));
        var answer = Assert.Single(panel.Answers);
        Assert.Equal(("has_item(\"oil\")", "true", false), (answer.Call, answer.Answer, answer.IsPending));

        // The options at the cursor: one that cannot be chosen shows greyed
        await panel.Continue();
        Assert.Equal([("Take the lamp", true), ("Leave it", false)], panel.Options.Select(o => (o.Text, o.IsAvailable)));
        Assert.Equal([true, false], panel.Options.Select(o => o.ChooseCommand.IsEnabled));
        Assert.False(panel.ContinueCommand.IsEnabled);

        // A variable set in the panel is a step of the run; a text that is not of its type is not
        gold = panel.Variables[0];
        gold.Value = "plenty";
        Assert.Equal("$gold is a number.", gold.Error);
        gold.Value = "12";
        Assert.Equal("", gold.Error);
        Assert.Equal("$gold = 12", panel.Transcript[^1].Text);
        Assert.Equal("12", panel.Variables[0].Value);

        // The choice, then the pick of the line group with why the others were out
        panel.Options[0].ChooseCommand.Execute();
        Assert.Equal("true", panel.Variables[1].Value);
        var pick = Assert.Single(panel.Transcript, t => t.Kind == PlayerStepKind.Saliency);
        Assert.Equal(2, pick.Candidates.Count);
        Assert.Contains(pick.Candidates, c => c.IsChosen);
        Assert.Equal("> Take the lamp", panel.Transcript.Single(t => t.Kind == PlayerStepKind.Choose).Text);
        Assert.Equal((PlayerStepKind.NodeEnter, "Pier"), (panel.Transcript[^3].Kind, panel.Transcript[^3].Text));

        // The end of the dialogue
        await panel.Continue();
        await panel.Continue();
        Assert.Equal((PlayerStepKind.End, "The dialogue is over."), (panel.Transcript[^1].Kind, panel.Transcript[^1].Text));
        Assert.False(panel.ContinueCommand.IsEnabled);

        // Going back shows the transcript up to the cursor
        await session.Player.GoTo(3);
        Assert.Equal(3, panel.Transcript.Count);
        Assert.Equal("5", panel.Variables[0].Value);
        Assert.Empty(panel.Answers);

        // An answer changed from there plays on from there
        await session.Player.GoTo(session.Player.Steps.ToList().FindIndex(s => s.Kind == PlayerStepKind.Function));
        Assert.Null(panel.Answers[0].Give("false"));
        await panel.Continue();
        Assert.DoesNotContain(panel.Transcript, t => t.Text == "Your lamp is full.");

        panel.ResetCommand.Execute();
        Assert.False(panel.IsPlaying);
        Assert.Empty(panel.Transcript);
        Assert.Empty(panel.Variables);

        panel.Destroy();
    }

    [Fact]
    public async Task The_player_says_why_a_dialogue_does_not_start_and_plays_what_it_is_given()
    {
        await using var session = await OpenAsync();
        var panel = Panel<DialoguePlayerViewModel>(session, DialoguePlayerViewModel.Panel);
        var project = session.ProjectAt("Dialogue/Story");
        var buffer = session.ScriptAt("Dialogue/Act1").TextBuffer;

        // "Play from here" of a script editor: the panel takes the project and the node
        await panel.Play(project, "Pier");
        Assert.Equal((project, "Pier"), (panel.SelectedProject, panel.SelectedNode));
        Assert.Equal("Pier", panel.Transcript[0].Text);
        Assert.Equal("", panel.Status);

        // An edit of the scripts shows, and a replay follows it
        buffer.Replace(buffer.Text.IndexOf("Mind the oil.", StringComparison.Ordinal), "Mind the oil.".Length, "Mind the oil, now.", YarnTextBuffer.TypingActionName);
        buffer.Replace(buffer.Text.IndexOf("=> Odile: Mind the wick. <<if $rich>>\n", StringComparison.Ordinal), "=> Odile: Mind the wick. <<if $rich>>\n".Length, "", YarnTextBuffer.TypingActionName);
        await session.Analysis.WhenIdle();
        Assert.True(panel.IsStale);
        Assert.Contains("changed", panel.Status);
        Assert.True(panel.ReplayCommand.IsEnabled);
        await panel.Replay();
        Assert.False(panel.IsStale);
        Assert.Contains(panel.Transcript, t => t.Text == "Mind the oil, now." && t.Changed);

        // Scripts with an error do not start: the panel says why
        buffer.Replace(buffer.Text.Length, 0, "title: Broken\n---\n<<if>>\n===\n", YarnTextBuffer.TypingActionName);
        await panel.Start();
        Assert.False(panel.IsPlaying);
        Assert.Contains("errors", panel.Status);

        // The language and the waits are the service's
        panel.HonourWait = true;
        Assert.True(session.Player.HonourWait);

        panel.Destroy();
    }

    [Fact]
    public async Task Play_from_here_plays_the_node_of_a_card_in_the_player()
    {
        await using var session = await SkeinSession.OpenAsync(
            SkeinSession.Project("Dialogue/Story"),
            SkeinSession.Script("Dialogue/Act1", Lamp + "title: Reminder\nstyle: note\n---\nAsk about the wick.\n===\n"),
            SkeinSession.Script("Elsewhere/Alone", "title: Alone\n---\nKye: Nobody.\n===\n"));
        var editor = session.Edit("Dialogue/Act1");
        await session.Analysis.WhenIdle();
        var player = session.Player;

        await editor.PlayFromHere(editor.Cards.Single(c => c.Title == "Pier"));

        Assert.True(player.IsPlaying);
        Assert.Equal((session.ProjectAt("Dialogue/Story"), "Pier"), (player.Project, player.StartNode));
        Assert.Null(editor.Problem);

        // Without a card, the selected node; a note is not played
        Assert.True(editor.Select("Start"));
        editor.PlayFromHereCommand.Execute(null);
        Assert.Equal("Start", player.StartNode);
        await editor.PlayFromHere(editor.Cards.Single(c => c.IsNote));
        Assert.Contains("note", editor.Problem);
        Assert.Equal("Start", player.StartNode);

        // A script in no project has nothing the player can play
        var alone = session.Edit("Elsewhere/Alone");
        await session.Analysis.WhenIdle();
        await alone.PlayFromHere(alone.Cards[0]);
        Assert.Contains("Yarn project", alone.Problem);
        Assert.Equal("Start", player.StartNode);
    }

    [Fact]
    public async Task The_timeline_shows_the_steps_and_what_went_on_across_them()
    {
        await using var session = await OpenAsync();
        var timeline = Panel<DialogueTimelineViewModel>(session, DialogueTimelineViewModel.Panel);
        var player = session.Player;
        Assert.Equal("No dialogue is playing.", timeline.Status);
        Assert.False(timeline.BackCommand.IsEnabled);

        await player.Start(session.ProjectAt("Dialogue/Story"), "Start");
        await player.Continue();
        await player.Answer("has_item", false);
        await player.SetVariable("$gold", 12);
        await player.Choose(0);
        await player.Continue();
        await player.Continue();

        var kinds = player.Steps.Select(s => s.Kind).ToList();
        Assert.Equal(kinds.Count, timeline.Steps.Count);
        Assert.Equal(player.Cursor, timeline.Cursor);
        Assert.Equal($"Step {kinds.Count - 1} of {kinds.Count - 1}.", timeline.Status);
        Assert.Equal("▶", timeline.Steps[0].Glyph);
        Assert.All(timeline.Steps, s => Assert.False(s.IsFuture));
        Assert.Contains("Odile: The tide is out.", timeline.Steps[kinds.IndexOf(PlayerStepKind.Line)].Tip);

        // The lanes: the nodes the run was in, what was said, the choices, what the game did, the variables that changed
        Assert.Equal(["Nodes", "Lines", "Choices", "Game", "Variables"], timeline.Lanes.Select(l => l.Title));
        var nodes = timeline.Lanes[0].Items;
        Assert.Equal(["Start", "Pier"], nodes.Where(i => !i.IsMark).Select(i => i.Label));
        Assert.Equal(kinds.IndexOf(PlayerStepKind.NodeEnter), nodes[0].Start);
        Assert.Equal(kinds.LastIndexOf(PlayerStepKind.NodeEnter) - 1, nodes[0].End);
        Assert.Contains(nodes, i => i.IsMark && i.Kind == TimelineItemKind.Pick);
        Assert.Equal(["Odile", "Odile", "Narrator"], timeline.Lanes[1].Items.Select(i => i.Label));
        Assert.Equal(["Take the lamp"], timeline.Lanes[2].Items.Select(i => i.Label));
        Assert.Equal(["fade_in", "has_item", "$gold"], timeline.Lanes[3].Items.Select(i => i.Label));
        Assert.Equal(["$gold", "$lamp"], timeline.Lanes[4].Items.Select(i => i.Label));
        Assert.Contains("5 → 12", timeline.Lanes[4].Items[0].Tip);

        // Going back and forth
        timeline.GoToStepCommand.Execute(2);
        Assert.Equal((2, 2), (player.Cursor, timeline.Cursor));
        Assert.True(timeline.Steps[3].IsFuture);
        Assert.False(timeline.Steps[2].IsFuture);
        Assert.Equal($"Step 2 of {kinds.Count - 1}.", timeline.Status);
        Assert.NotEmpty(timeline.Details);
        timeline.ForwardCommand.Execute();
        Assert.Equal(3, player.Cursor);
        timeline.BackCommand.Execute();
        timeline.BackCommand.Execute();
        timeline.BackCommand.Execute();
        Assert.Equal(0, player.Cursor);
        Assert.False(timeline.BackCommand.IsEnabled);

        // An edit, then the replay from the timeline
        Assert.False(timeline.ReplayCommand.IsEnabled);
        var buffer = session.ScriptAt("Dialogue/Act1").TextBuffer;
        buffer.Replace(buffer.Text.IndexOf("-> Take the lamp #line:take", StringComparison.Ordinal), "-> Take the lamp #line:take".Length, "-> Take it #line:taken", YarnTextBuffer.TypingActionName);
        await session.Analysis.WhenIdle();
        Assert.True(timeline.ReplayCommand.IsEnabled);
        Assert.Contains("changed since", timeline.Status);
        await timeline.Replay();
        Assert.Contains("'Take the lamp'", timeline.Status);
        Assert.False(timeline.ReplayCommand.IsEnabled);

        player.Reset();
        Assert.Empty(timeline.Steps);
        Assert.Empty(timeline.Lanes);

        timeline.Destroy();
    }
}
