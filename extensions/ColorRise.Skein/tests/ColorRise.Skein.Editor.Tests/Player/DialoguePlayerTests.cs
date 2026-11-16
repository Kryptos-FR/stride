using ColorRise.Skein.Assets;
using ColorRise.Skein.Assets.Compilation;
using ColorRise.Skein.Editor.Player;
using ColorRise.Skein.Editor.Text;
using Stride.Core.Assets.Editor.Testing;

namespace ColorRise.Skein.Editor.Tests.Player;

/// <summary>
/// The dialogue played in the session: a recorded run of the scripts as they are in memory, with a cursor that goes back
/// and forth, the functions of the game answered by hand, and a replay that follows the edits.
/// </summary>
public sealed class DialoguePlayerTests
{
    private const string Lamp = """
        title: Start
        ---
        <<declare $gold = 5 as number>>
        Odile: The tide is out. #line:tide
        <<set $gold = $gold + 1>>
        Odile: Take this.
        -> Take the lamp #line:take
            <<set $lamp = true>>
            Odile: Mind the oil.
        -> Leave it #line:leave
            Odile: As you like.
        Narrator: She leaves.
        ===

        """;

    private static readonly PlayerStepKind[] WholeRun =
    [
        PlayerStepKind.Start, PlayerStepKind.NodeEnter, PlayerStepKind.Line, PlayerStepKind.Line, PlayerStepKind.Options,
        PlayerStepKind.Choose, PlayerStepKind.Line, PlayerStepKind.Line, PlayerStepKind.End,
    ];

    private static async DispatcherTask<SkeinSession> OpenAsync(string script, params string[] languages)
    {
        var project = new YarnProjectAssetFactory().New();
        project.Localisations.AddRange(languages.Select(l => new YarnLocalisation { Language = l }));
        return await SkeinSession.OpenAsync(("Dialogue/Story", project), SkeinSession.Script("Dialogue/Act1", script));
    }

    // Plays the lamp story to its end, taking the lamp
    private static async Task<DialoguePlayerService> PlayLampAsync(SkeinSession session)
    {
        var player = session.Player;
        await player.Start(session.ProjectAt("Dialogue/Story"), "Start");
        await player.Continue();
        await player.Continue();
        await player.Choose(0);
        await player.Continue();
        await player.Continue();
        return player;
    }

    private static void Edit(SkeinSession session, string old, string text)
    {
        var buffer = session.ScriptAt("Dialogue/Act1").TextBuffer;
        buffer.Replace(buffer.Text.IndexOf(old, StringComparison.Ordinal), old.Length, text, YarnTextBuffer.TypingActionName);
    }

    [Fact]
    public async Task A_run_records_its_steps_in_order_with_the_variables_after_each()
    {
        await using var session = await OpenAsync(Lamp);
        var player = session.Player;
        var changes = 0;
        player.Changed += () => changes++;

        await player.Start(session.ProjectAt("Dialogue/Story"), "Start");

        Assert.True(player.IsPlaying);
        Assert.True(changes > 0);
        Assert.Equal([PlayerStepKind.Start, PlayerStepKind.NodeEnter, PlayerStepKind.Line], player.Steps.Select(s => s.Kind));
        Assert.Equal(2, player.Cursor);
        Assert.Equal(("Odile: The tide is out.", "Odile", "Start", "Dialogue/Act1.yarn", 4), (player.Steps[2].Text, player.Steps[2].Speaker, player.Steps[2].Node, player.Steps[2].File, player.Steps[2].Line));
        Assert.Equal(5f, player.Steps[2].Variables["$gold"]);
        Assert.Equal(("Start", 4), (player.CurrentNode, player.CurrentLine));
        Assert.Equal(["Start"], player.NodePath);
        Assert.True(player.CanContinue);

        await player.Continue();
        Assert.Equal("Odile: Take this.", player.CurrentStep!.Text);
        Assert.Equal(6f, player.CurrentStep.Variables["$gold"]);
        Assert.Equal(false, player.CurrentStep.Variables["$lamp"]);

        // The options stop the run until one is chosen; the choice is a step too
        await player.Continue();
        Assert.Equal(PlayerStepKind.Options, player.CurrentStep.Kind);
        Assert.Equal([(0, "Take the lamp", true), (1, "Leave it", true)], player.Options.Select(o => (o.Index, o.Text, o.IsAvailable)));
        Assert.False(player.CanContinue);
        Assert.Equal(7, player.CurrentLine);
        await player.Continue();
        Assert.Equal(4, player.Cursor);

        await player.Choose(0);
        Assert.Equal([PlayerStepKind.Choose, PlayerStepKind.Line], player.Steps.Skip(5).Select(s => s.Kind));
        Assert.Equal(("Take the lamp", "Odile: Mind the oil."), (player.Steps[5].Text, player.Steps[6].Text));
        Assert.Equal(true, player.CurrentStep.Variables["$lamp"]);
        Assert.Empty(player.Options);

        await player.Continue();
        await player.Continue();
        Assert.Equal(WholeRun, player.Steps.Select(s => s.Kind));
        Assert.Equal(8, player.Cursor);
        Assert.False(player.CanContinue);

        // Nothing is left to play: the run stays where it is
        await player.Continue();
        Assert.Equal(9, player.Steps.Count);

        player.Reset();
        Assert.False(player.IsPlaying);
        Assert.Empty(player.Steps);
        Assert.Equal(-1, player.Cursor);
        Assert.Null(player.CurrentNode);
    }

    [Fact]
    public async Task Going_back_gives_the_state_of_a_step_and_keeps_the_later_steps_until_a_new_input()
    {
        await using var session = await OpenAsync(Lamp);
        var player = await PlayLampAsync(session);

        await player.GoTo(2);

        Assert.Equal(2, player.Cursor);
        Assert.Equal("Odile: The tide is out.", player.CurrentStep!.Text);
        Assert.Equal(5f, player.CurrentStep.Variables["$gold"]);
        Assert.Equal(4, player.CurrentLine);
        Assert.Equal(WholeRun, player.Steps.Select(s => s.Kind));

        // Going on by the same inputs walks the recorded steps
        await player.Continue();
        Assert.Equal((3, 9), (player.Cursor, player.Steps.Count));
        await player.Continue();
        Assert.Equal(PlayerStepKind.Options, player.CurrentStep.Kind);
        Assert.Equal(2, player.Options.Count);
        await player.Choose(0);
        Assert.Equal((6, 9), (player.Cursor, player.Steps.Count));
        Assert.Equal("Odile: Mind the oil.", player.CurrentStep.Text);

        // Another choice drops what followed and plays from there
        await player.GoTo(4);
        await player.Choose(1);
        Assert.Equal(WholeRun.Take(7), player.Steps.Select(s => s.Kind));
        Assert.Equal(("Leave it", "Odile: As you like."), (player.Steps[5].Text, player.Steps[6].Text));
        Assert.Equal(6, player.Cursor);
        Assert.Equal(false, player.CurrentStep.Variables["$lamp"]);
        Assert.Equal(6f, player.CurrentStep.Variables["$gold"]);

        // A step that does not exist is the nearest one
        await player.GoTo(40);
        Assert.Equal(6, player.Cursor);
        await player.GoTo(-3);
        Assert.Equal(0, player.Cursor);
    }

    [Fact]
    public async Task A_function_of_the_game_stops_the_run_with_a_question_and_its_answer_is_remembered()
    {
        await using var session = await OpenAsync("""
            title: Start
            ---
            Kye: Evening.
            <<if has_item("oil")>>
                Kye: The lamp is full.
            <<else>>
                Kye: The lamp is dry.
            <<endif>>
            <<if has_item("oil")>>
                Kye: Twice.
            <<endif>>
            Kye: You owe {debt("Odile") + 1} coins.
            ===

            """);
        var player = session.Player;
        await player.Start(session.ProjectAt("Dialogue/Story"), "Start");

        await player.Continue();

        // The run waits at the line it was on, with the question
        Assert.Equal(("has_item", "has_item(\"oil\")", "bool"), (player.Question!.Function, player.Question.Call, player.Question.ReturnType));
        Assert.Equal((3, 2), (player.Steps.Count, player.Cursor));
        Assert.False(player.CanContinue);
        await player.Continue();
        Assert.Equal(3, player.Steps.Count);

        await player.Answer("has_item", true);

        Assert.Null(player.Question);
        Assert.Equal([PlayerStepKind.Function, PlayerStepKind.Line], player.Steps.Skip(3).Select(s => s.Kind));
        Assert.Equal(("has_item(\"oil\") → true", "Kye: The lamp is full."), (player.Steps[3].Text, player.Steps[4].Text));
        Assert.Equal([("has_item(\"oil\")", (object)true)], player.Answers.Select(a => (a.Call, a.Value)));

        // The same call is not asked again; another function is
        await player.Continue();
        Assert.Equal("Kye: Twice.", player.CurrentStep!.Text);
        await player.Continue();
        Assert.Equal(("debt", "number"), (player.Question!.Function, player.Question.ReturnType));

        // An answer of another type is refused, and the question stays
        await Assert.ThrowsAsync<ArgumentException>(() => player.Answer("debt", "a lot"));
        Assert.NotNull(player.Question);
        await player.Answer("debt", 2);
        Assert.Equal("Kye: You owe 3 coins.", player.CurrentStep!.Text);

        // Going back and on again asks nothing
        await player.GoTo(2);
        await player.Continue();
        Assert.Null(player.Question);
        Assert.Equal("Kye: The lamp is full.", player.CurrentStep.Text);

        // An answer changed at a step holds from that step on
        await player.GoTo(2);
        await player.ChangeAnswer("has_item(\"oil\")", false);
        await player.Continue();
        Assert.Equal("Kye: The lamp is dry.", player.CurrentStep.Text);
    }

    [Fact]
    public async Task A_function_in_a_condition_of_a_node_group_or_at_the_start_is_asked_too()
    {
        await using var session = await OpenAsync("""
            title: Start
            ---
            <<if tide() > 2>>
                Kye: High water.
            <<endif>>
            <<detour Shop>>
            Kye: Back outside.
            ===
            title: Shop
            when: has_item("oil")
            ---
            Kye: Oil, I see.
            ===
            title: Shop
            when: always
            ---
            Kye: Nothing to trade.
            ===

            """);
        var player = session.Player;

        // The first input of the run asks: no step yet
        await player.Start(session.ProjectAt("Dialogue/Story"), "Start");
        Assert.True(player.IsPlaying);
        Assert.Equal(("tide()", "number"), (player.Question!.Call, player.Question.ReturnType));
        Assert.Equal((0, -1), (player.Steps.Count, player.Cursor));

        await player.Answer("tide", 3);
        Assert.Equal([PlayerStepKind.Function, PlayerStepKind.Start, PlayerStepKind.NodeEnter, PlayerStepKind.Line], player.Steps.Select(s => s.Kind));
        Assert.Equal("Kye: High water.", player.CurrentStep!.Text);

        // The pick of the group needs the function: the question, then the pick with the answer
        await player.Continue();
        Assert.Equal("has_item(\"oil\")", player.Question!.Call);
        Assert.Equal(4, player.Steps.Count);
        await player.Answer("has_item", true);
        Assert.Equal("Kye: Oil, I see.", player.CurrentStep!.Text);
        Assert.Single(player.Steps, s => s.Kind == PlayerStepKind.Saliency);

        // After the detour the run is back in the first node
        await player.Continue();
        Assert.Equal(("Kye: Back outside.", "Start"), (player.CurrentStep.Text, player.CurrentNode));
        Assert.Equal(2, player.NodePath.Count);
    }

    [Fact]
    public async Task A_variable_set_by_hand_is_a_step_and_survives_going_back_past_it()
    {
        await using var session = await OpenAsync(Lamp);
        var player = session.Player;
        await player.Start(session.ProjectAt("Dialogue/Story"), "Start");

        await player.SetVariable("$gold", 9);

        Assert.Equal((PlayerStepKind.SetVariable, "$gold = 9"), (player.CurrentStep!.Kind, player.CurrentStep.Text));
        Assert.Equal(9f, player.CurrentStep.Variables["$gold"]);
        Assert.True(player.CanContinue);
        await player.Continue();
        Assert.Equal(10f, player.CurrentStep.Variables["$gold"]);

        await player.GoTo(2);
        Assert.Equal(5f, player.CurrentStep.Variables["$gold"]);
        Assert.Equal(PlayerStepKind.SetVariable, player.Steps[3].Kind);
        await player.Continue();
        Assert.Equal(("Odile: Take this.", 4), (player.CurrentStep.Text, player.Cursor));
        Assert.Equal(10f, player.CurrentStep.Variables["$gold"]);

        // A value of another type, a smart variable and a name the scripts do not have are refused
        await Assert.ThrowsAsync<ArgumentException>(() => player.SetVariable("$gold", "plenty"));
        await Assert.ThrowsAsync<ArgumentException>(() => player.SetVariable("$nothing", 1));
        Assert.Equal(5, player.Steps.Count);
    }

    [Fact]
    public async Task An_edit_makes_the_run_stale_and_a_replay_plays_the_same_choices_on_the_new_text()
    {
        await using var session = await OpenAsync(Lamp);
        var player = await PlayLampAsync(session);
        await player.GoTo(6);
        Assert.False(player.IsStale);

        Edit(session, "Odile: Take this.", "Odile: Take this lamp.\nOdile: It was my mother's.");
        await session.Analysis.WhenIdle();

        Assert.True(player.IsStale);
        Assert.Equal("Odile: Take this.", player.Steps[3].Text);

        await player.Replay();

        Assert.False(player.IsStale);
        Assert.Null(player.Divergence);
        Assert.Equal(
            ["Odile: The tide is out.", "Odile: Take this lamp.", "Odile: It was my mother's.", "Odile: Mind the oil.", "Narrator: She leaves."],
            player.Steps.Where(s => s.Kind == PlayerStepKind.Line).Select(s => s.Text));
        Assert.Equal(["Odile: Take this lamp.", "Odile: It was my mother's."], player.Steps.Where(s => s.Changed).Select(s => s.Text));

        // The cursor is at the step it was at: the line after the choice
        Assert.Equal("Odile: Mind the oil.", player.CurrentStep!.Text);
        Assert.Equal(PlayerStepKind.End, player.Steps[^1].Kind);
    }

    [Fact]
    public async Task A_replay_stops_before_a_choice_that_is_gone_and_says_why()
    {
        await using var session = await OpenAsync(Lamp);
        var player = await PlayLampAsync(session);

        Edit(session, "-> Take the lamp #line:take\n    <<set $lamp = true>>\n    Odile: Mind the oil.\n", "");
        await session.Analysis.WhenIdle();
        await player.Replay();

        Assert.Contains("'Take the lamp'", player.Divergence);
        Assert.Equal(WholeRun.Take(5), player.Steps.Select(s => s.Kind));
        Assert.Equal(4, player.Cursor);
        Assert.Equal(["Leave it"], player.Options.Select(o => o.Text));

        // The run goes on from there by a new input
        await player.Choose(0);
        Assert.Null(player.Divergence);
        Assert.Equal("Odile: As you like.", player.CurrentStep!.Text);

        // Scripts with an error are not replayed: the run stays as it was
        Edit(session, "Narrator: She leaves.", "<<if>>");
        await session.Analysis.WhenIdle();
        await player.Replay();
        Assert.Contains("errors", player.Problem);
        Assert.True(player.IsStale);
        Assert.Equal("Odile: As you like.", player.CurrentStep.Text);
    }

    [Fact]
    public async Task A_run_with_random_numbers_replays_to_the_same_lines()
    {
        await using var session = await OpenAsync("""
            title: Start
            ---
            <<declare $unused = false>>
            Dealer: {dice(6)} {dice(6)} {dice(6)} {random_range(1, 1000)}
            => Dealer: Again?
            => Dealer: Once more?
            => Dealer: Another?
            Dealer: {dice(100)} {dice(100)}
            ===

            """);
        var player = session.Player;
        await player.Start(session.ProjectAt("Dialogue/Story"), "Start");
        await player.Continue();
        await player.Continue();
        var lines = player.Steps.Select(s => s.Text).ToList();

        await player.Replay();

        Assert.Equal(lines, player.Steps.Select(s => s.Text));
        Assert.DoesNotContain(player.Steps, s => s.Changed);

        // A new input from an earlier step plays the same numbers too
        await player.GoTo(2);
        await player.SetVariable("$unused", true);
        await player.Continue();
        await player.Continue();
        Assert.Equal(lines.Skip(3), player.Steps.Skip(4).Select(s => s.Text));
    }

    [Fact]
    public async Task A_pick_among_the_nodes_of_a_group_is_a_step_with_what_ruled_the_others_out()
    {
        await using var session = await OpenAsync("""
            title: Start
            ---
            <<declare $gold = 5 as number>>
            <<declare $unused = false>>
            Odile: Well.
            <<jump Greeting>>
            ===
            title: Greeting
            when: $gold > 10
            ---
            Odile: Rich.
            ===
            title: Greeting
            when: $gold > 1
            ---
            Odile: Poor.
            ===

            """);
        var player = session.Player;
        await player.Start(session.ProjectAt("Dialogue/Story"), "Start");
        await player.Continue();

        Assert.Equal("Odile: Poor.", player.CurrentStep!.Text);
        var index = player.Steps.ToList().FindIndex(s => s.Kind == PlayerStepKind.Saliency);
        Assert.InRange(index, 3, player.Cursor - 1);
        var pick = player.Steps[index].Pick!;
        Assert.Equal(("Greeting", false), (pick.Group, pick.IsLineGroup));
        Assert.Equal([(false, "$gold > 10"), (true, null)], pick.Candidates.Select(c => (c.Passed, c.FailedCondition)));
        Assert.Equal(pick.Candidates[1].Name, pick.Chosen);
        Assert.Equal(pick.Chosen, player.CurrentNode);
        Assert.Equal(["Start", pick.Chosen!], player.NodePath);
        Assert.Equal("Greeting picks " + pick.Chosen, player.Steps[index].Text);
        Assert.Equal([(false, false, "$gold > 10 fails"), (true, true, "score 1")], player.Steps[index].Candidates.Select(c => (c.IsChosen, c.Passed, c.Detail)));
    }

    [Fact]
    public async Task The_language_shows_the_translation_of_the_lines()
    {
        await using var session = await OpenAsync(Lamp, "fr");
        var project = session.ProjectAt("Dialogue/Story");
        var exported = await session.Localisation.ExportStrings(project, "fr");
        Assert.Null(exported.Problem);
        var file = Path.Combine(Path.GetDirectoryName(session.FileOf("Dialogue/Story", ".sdyarn"))!, "Strings", "Story.fr.csv");
        File.WriteAllText(file, File.ReadAllText(file).Replace("Odile: The tide is out.", "Odile : La mer est basse.").Replace("Take the lamp", "Prendre la lampe"));
        var player = session.Player;
        await player.Start(project, "Start");
        Assert.Equal(["en", "fr"], player.Languages);
        Assert.Equal("en", player.Language);

        player.Language = "fr";

        Assert.Equal(("Odile : La mer est basse.", 2), (player.CurrentStep!.Text, player.Cursor));
        Assert.DoesNotContain(player.Steps, s => s.Changed);

        // A line without an id has no translation: it shows as written
        await player.Continue();
        Assert.Equal("Odile: Take this.", player.CurrentStep.Text);
        await player.Continue();
        Assert.Equal(["Prendre la lampe", "Leave it"], player.Options.Select(o => o.Text));

        // The language stays for the next run
        await player.Start(project, "Start");
        Assert.Equal("Odile : La mer est basse.", player.CurrentStep.Text);
        player.Language = "en";
        Assert.Equal("Odile: The tide is out.", player.CurrentStep.Text);
    }

    [Fact]
    public async Task Commands_show_and_go_on_at_once_and_a_wait_is_honoured_only_when_asked()
    {
        await using var session = await OpenAsync("""
            title: Start
            ---
            <<fade_out 2>>
            <<wait 0.05>>
            Kye: Dark.
            <<jump Missing>>
            ===

            """);
        var player = session.Player;
        var project = session.ProjectAt("Dialogue/Story");
        // A jump to a node that is missing is an error in a new project: here it is the warning Yarn Spinner makes of it
        project.Asset.DiagnosticSeverities["YS0012"] = YarnSeverity.Warning;
        session.Analysis.Invalidate();

        await player.Start(project, "Start");

        Assert.Equal([PlayerStepKind.Start, PlayerStepKind.NodeEnter, PlayerStepKind.Command, PlayerStepKind.Command, PlayerStepKind.Line], player.Steps.Select(s => s.Kind));
        Assert.Equal(("fade_out 2", "wait 0.05"), (player.Steps[2].Text, player.Steps[3].Text));

        // What fails while playing ends the run with the reason
        await player.Continue();
        Assert.Equal(PlayerStepKind.Error, player.CurrentStep!.Kind);
        Assert.Contains("Missing", player.CurrentStep.Text);
        Assert.False(player.CanContinue);

        // With the waits honoured the run takes their time, and going back over them does not
        player.HonourWait = true;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        await player.Start(project, "Start");
        Assert.InRange(watch.ElapsedMilliseconds, 40, 5000);
        Assert.Equal("Kye: Dark.", player.CurrentStep!.Text);

        // Scripts with an error do not start
        Edit(session, "Kye: Dark.", "<<if>>");
        await player.Start(project, "Start");
        Assert.False(player.IsPlaying);
        Assert.Contains("errors", player.Problem);

        // Nor does a node the project does not have
        Edit(session, "<<if>>", "Kye: Dark.");
        await player.Start(project, "Nowhere");
        Assert.False(player.IsPlaying);
        Assert.Contains("'Nowhere'", player.Problem);
    }
}
