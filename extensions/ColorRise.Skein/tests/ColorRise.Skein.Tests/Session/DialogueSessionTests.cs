using ColorRise.Skein.Assets.Compilation;

namespace ColorRise.Skein.Tests.Session;

/// <summary>
/// A session runs a program and asks its responder, one awaited call at a time.
/// </summary>
public sealed class DialogueSessionTests
{
    private const string Post = """
        title: Start
        ---
        Odile: The post is late.
        <<ring bell>>
        -> Wait
            Odile: Then we wait.
        -> Leave
            Odile: Go on, then.
        Odile: Done.
        ===
        """;

    private static readonly string[] PostWaiting =
    [
        "line: Odile: The post is late.",
        "command: ring bell",
        "option: Wait",
        "option: Leave",
        "line: Odile: Then we wait.",
        "line: Odile: Done.",
        "complete",
    ];

    [Fact]
    public async Task Lines_options_and_commands_come_in_the_order_of_the_script()
    {
        var dialogue = new TestDialogue(Post);
        dialogue.Responder.Choices.Enqueue(1);

        await dialogue.RunAsync();

        Assert.Equal(
            [
                "line: Odile: The post is late.",
                "command: ring bell",
                "option: Wait",
                "option: Leave",
                "line: Odile: Go on, then.",
                "line: Odile: Done.",
                "complete",
            ],
            dialogue.Transcript);
        Assert.Equal([0, 1], dialogue.Responder.Offers[0].Select(o => o.Index));
        Assert.Empty(dialogue.Errors);
    }

    [Fact]
    public async Task An_option_whose_condition_fails_is_offered_as_unavailable()
    {
        var dialogue = new TestDialogue("""
            title: Start
            ---
            <<declare $gold = 0>>
            -> Bribe the guard <<if $gold > 5>>
                Guard: Thank you.
            -> Walk away
                Guard: Good day.
            ===
            """);

        await dialogue.RunAsync();

        var offer = Assert.Single(dialogue.Responder.Offers);
        Assert.False(offer[0].IsAvailable);
        Assert.True(offer[1].IsAvailable);
        Assert.Equal(["option: Bribe the guard [disabled]", "option: Walk away", "line: Guard: Good day.", "complete"], dialogue.Transcript);
    }

    [Fact]
    public async Task No_choice_goes_on_after_the_options()
    {
        var dialogue = new TestDialogue("""
            title: Start
            ---
            <<declare $gold = 0>>
            -> Bribe the guard <<if $gold > 5>>
                Guard: Thank you.
            Guard: Move along.
            ===
            """);
        dialogue.Responder.Choices.Enqueue(null);

        await dialogue.RunAsync();

        Assert.Equal(["option: Bribe the guard [disabled]", "line: Guard: Move along.", "complete"], dialogue.Transcript);
    }

    [Fact]
    public async Task A_choice_that_was_not_offered_fails_the_run_and_leaves_the_session_free()
    {
        var dialogue = new TestDialogue(Post);
        dialogue.Responder.Choices.Enqueue(7);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => dialogue.RunAsync());

        Assert.Contains("option 7", failure.Message);
        Assert.Equal(1, dialogue.Responder.Completions);
        Assert.False(dialogue.Session.IsRunning);

        dialogue.Transcript.Clear();
        await dialogue.RunAsync();
        Assert.Equal(PostWaiting, dialogue.Transcript);
    }

    [Fact]
    public async Task A_session_runs_again_after_its_end()
    {
        var dialogue = new TestDialogue(Post);

        await dialogue.RunAsync();
        await dialogue.RunAsync();

        Assert.Equal([.. PostWaiting, .. PostWaiting], dialogue.Transcript);
        Assert.Equal(2, dialogue.Responder.Completions);
    }

    [Fact]
    public async Task Nodes_are_told_as_the_dialogue_enters_and_leaves_them()
    {
        var dialogue = new TestDialogue("""
            title: Start
            ---
            Kye: One.
            <<jump Second>>
            ===
            title: Second
            ---
            Kye: Two.
            ===
            """);
        dialogue.Responder.RecordNodes = true;
        var during = new List<(string? Node, bool Running)>();
        dialogue.Responder.OnLine = (_, _) =>
        {
            during.Add((dialogue.Session.CurrentNode, dialogue.Session.IsRunning));
            return default;
        };
        var runningAtTheEnd = true;
        dialogue.Responder.OnComplete = () => runningAtTheEnd = dialogue.Session.IsRunning;

        await dialogue.RunAsync();

        Assert.Equal(
            ["node: Start", "prepare", "line: Kye: One.", "end: Start", "node: Second", "prepare", "line: Kye: Two.", "end: Second", "complete"],
            dialogue.Transcript.Select(t => t.StartsWith("prepare: line:", StringComparison.Ordinal) ? "prepare" : t));
        Assert.Equal([("Start", true), ("Second", true)], during);
        Assert.False(runningAtTheEnd);
        Assert.Null(dialogue.Session.CurrentNode);
    }

    [Fact]
    public async Task Running_twice_at_once_or_an_unknown_node_is_refused()
    {
        var dialogue = new TestDialogue(Post);
        dialogue.Responder.OnLine = (_, cancellation) => Forever(cancellation.NextLine);

        await Assert.ThrowsAsync<ArgumentException>(() => dialogue.RunAsync("Nowhere"));
        Assert.Equal(0, dialogue.Responder.Completions);

        var run = dialogue.RunAsync();
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => dialogue.RunAsync());
        Assert.Contains("already running", failure.Message);

        dialogue.Session.Stop();
        await run;
        Assert.True(dialogue.Session.NodeExists("Start"));
        Assert.False(dialogue.Session.NodeExists("Nowhere"));
    }

    [Theory]
    [InlineData("line")]
    [InlineData("options")]
    [InlineData("command")]
    public async Task Stop_cancels_what_the_responder_awaits_and_ends_the_run(string during)
    {
        var dialogue = new TestDialogue(Post);
        var cancelled = false;
        async ValueTask Wait(CancellationToken cancellation)
        {
            try
            {
                await Forever(cancellation);
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
                throw;
            }
        }

        switch (during)
        {
            case "line":
                dialogue.Responder.OnLine = (_, cancellation) => Wait(cancellation.NextLine);
                break;
            case "options":
                dialogue.Responder.OnOptions = async (_, cancellation) =>
                {
                    await Wait(cancellation);
                    return 0;
                };
                break;
            default:
                dialogue.Responder.OnCommand = (_, cancellation) => Wait(cancellation);
                break;
        }

        var run = dialogue.RunAsync();
        Assert.True(dialogue.Session.IsRunning);
        Assert.False(run.IsCompleted);

        dialogue.Session.Stop();
        await run;

        Assert.True(cancelled);
        Assert.Equal(1, dialogue.Responder.Completions);
        Assert.Equal("complete", dialogue.Transcript[^1]);
        Assert.False(dialogue.Session.IsRunning);

        // The session is free for its next run, from the start
        dialogue.Responder.OnLine = null;
        dialogue.Responder.OnOptions = null;
        dialogue.Responder.OnCommand = null;
        dialogue.Transcript.Clear();
        await dialogue.RunAsync();
        Assert.Equal(PostWaiting, dialogue.Transcript);
        Assert.Equal(2, dialogue.Responder.Completions);
    }

    [Fact]
    public async Task The_cancellation_of_the_run_stops_it_like_Stop()
    {
        var dialogue = new TestDialogue(Post);
        dialogue.Responder.OnCommand = (_, cancellation) => Forever(cancellation);
        using var cancellation = new CancellationTokenSource();

        var run = dialogue.Session.RunAsync("Start", cancellation.Token);
        cancellation.Cancel();
        await run;

        Assert.Equal(["line: Odile: The post is late.", "command: ring bell", "complete"], dialogue.Transcript);
    }

    [Fact]
    public async Task A_stop_the_responder_does_not_await_still_ends_the_run()
    {
        var dialogue = new TestDialogue(Post);
        dialogue.Responder.OnLine = (_, _) =>
        {
            dialogue.Session.Stop();
            return default;
        };

        await dialogue.RunAsync();

        Assert.Equal(["line: Odile: The post is late.", "complete"], dialogue.Transcript);

        // Stopping what does not run is nothing
        dialogue.Session.Stop();
        dialogue.Session.RequestHurryUp();
        dialogue.Session.RequestNextLine();
    }

    [Fact]
    public async Task A_failure_of_the_responder_ends_the_run_and_is_thrown_after_the_end_is_told()
    {
        var dialogue = new TestDialogue(Post);
        dialogue.Responder.OnCommand = (_, _) => throw new FormatException("no bell");

        var failure = await Assert.ThrowsAsync<FormatException>(() => dialogue.RunAsync());

        Assert.Equal("no bell", failure.Message);
        Assert.Equal(["line: Odile: The post is late.", "command: ring bell", "complete"], dialogue.Transcript);
        Assert.False(dialogue.Session.IsRunning);
    }

    [Fact]
    public async Task Hurry_up_and_next_line_reach_the_line_being_shown_only()
    {
        var dialogue = new TestDialogue("""
            title: Start
            ---
            Kye: One.
            Kye: Two.
            ===
            """);
        var seen = new List<string>();
        dialogue.Responder.OnLine = (line, cancellation) =>
        {
            seen.Add($"{cancellation.HurryUp.IsCancellationRequested} {cancellation.NextLine.IsCancellationRequested}");
            if (line.Text.Text.EndsWith("One.", StringComparison.Ordinal))
            {
                dialogue.Session.RequestHurryUp();
                seen.Add($"{cancellation.HurryUp.IsCancellationRequested} {cancellation.NextLine.IsCancellationRequested}");
                dialogue.Session.RequestNextLine();
                seen.Add($"{cancellation.HurryUp.IsCancellationRequested} {cancellation.NextLine.IsCancellationRequested}");

                // A presenter may leave by the cancellation of its line: the line is over, the dialogue goes on
                cancellation.NextLine.ThrowIfCancellationRequested();
            }

            return default;
        };

        await dialogue.RunAsync();

        Assert.Equal(["False False", "True False", "True True", "False False"], seen);
        Assert.Equal(["line: Kye: One.", "line: Kye: Two.", "complete"], dialogue.Transcript);
    }

    [Fact]
    public async Task A_seed_gives_the_same_run_again()
    {
        const string Script = """
            title: Start
            ---
            Roll {dice(6)} {dice(6)} {dice(6)} {dice(6)} {dice(6)} {dice(6)} {random_range(1, 100)} {random()}
            <<jump Gulls>>
            ===
            title: Gulls
            when: always
            ---
            First gull.
            ===
            title: Gulls
            when: always
            ---
            Second gull.
            ===
            title: Gulls
            when: always
            ---
            Third gull.
            ===
            """;

        var first = new TestDialogue(Script, seed: 7);
        var second = new TestDialogue(Script, seed: 7);
        for (var i = 0; i < 3; i++)
        {
            await first.RunAsync();
            await second.RunAsync();
        }

        Assert.Equal(first.Transcript, second.Transcript);

        // The numbers start anew at each run, and the gulls take turns: each one before any comes again
        var rolls = first.Transcript.Where(t => t.StartsWith("line: Roll", StringComparison.Ordinal)).ToList();
        Assert.Equal(3, rolls.Count);
        Assert.Single(rolls.Distinct());
        Assert.All(rolls[0].Split(' ')[2..8], roll => Assert.InRange(int.Parse(roll), 1, 6));
        Assert.Equal(
            ["line: First gull.", "line: Second gull.", "line: Third gull."],
            first.Transcript.Where(t => t.EndsWith("gull.", StringComparison.Ordinal)).Order());
    }

    private const string Gulls = """
        title: Setup
        ---
        <<declare $tide = "high">>
        <<declare $has_bread = false>>
        ===
        title: Gulls
        when: $tide == "low"
        ---
        Gulls pick at the wet sand.
        ===
        title: Gulls
        when: $has_bread
        when: $tide == "high"
        ---
        Gulls dive for the bread.
        ===
        title: Gulls
        when: always
        ---
        Gulls wheel overhead.
        ===
        title: Ask
        ---
        <<if has_any_content("Gulls")>>
            There are gulls.
        <<endif>>
        ===
        """;

    [Fact]
    public async Task A_pick_among_nodes_tells_every_candidate_and_the_condition_that_failed()
    {
        var dialogue = new TestDialogue(Gulls, saliency: YarnSaliency.Best);
        var names = dialogue.Compilation.Nodes.Where(n => n.Title == "Gulls").Select(n => n.UniqueTitle).ToList();
        var picks = new List<SaliencyPick>();
        dialogue.Session.SaliencyPicked += picks.Add;

        await dialogue.RunAsync("Gulls");

        var pick = Assert.Single(picks);
        Assert.Equal("Gulls", pick.Group);
        Assert.False(pick.IsLineGroup);
        Assert.Equal(names, pick.Candidates.Select(c => c.Name));
        Assert.Equal([false, false, true], pick.Candidates.Select(c => c.Passed));
        Assert.Equal(["$tide == \"low\"", "$has_bread", null], pick.Candidates.Select(c => c.FailedCondition));
        Assert.True(pick.Candidates[1].Complexity > pick.Candidates[0].Complexity);
        Assert.Equal(names[2], pick.Chosen);
        Assert.Equal(["line: Gulls wheel overhead.", "complete"], dialogue.Transcript);

        // With bread, the most specific node wins; at low tide its second condition fails
        dialogue.Variables.SetValue("$has_bread", true);
        await dialogue.RunAsync("Gulls");
        Assert.Equal(names[1], picks[1].Chosen);

        dialogue.Variables.SetValue("$tide", "low");
        await dialogue.RunAsync("Gulls");
        Assert.Equal([null, "$tide == \"high\"", null], picks[2].Candidates.Select(c => c.FailedCondition));
        Assert.Equal(names[0], picks[2].Chosen);
    }

    [Fact]
    public async Task Asking_whether_a_group_has_content_is_not_a_pick()
    {
        var dialogue = new TestDialogue(Gulls);
        var picks = new List<SaliencyPick>();
        dialogue.Session.SaliencyPicked += picks.Add;

        Assert.True(dialogue.Session.HasSalientContent("Gulls"));
        Assert.True(dialogue.Session.HasSalientContent("Setup"));
        await dialogue.RunAsync("Ask");

        Assert.Empty(picks);
        Assert.Equal(["line: There are gulls.", "complete"], dialogue.Transcript);
        Assert.Throws<ArgumentException>(() => dialogue.Session.HasSalientContent("Nowhere"));
    }

    [Fact]
    public async Task A_pick_among_the_lines_of_a_line_group_is_told_too()
    {
        var dialogue = new TestDialogue("""
            title: Start
            ---
            <<declare $rain = false>>
            => Odile: Lovely weather. #line:lovely
            => Odile: Mind the puddles. <<if $rain>> #line:puddles
            Odile: Anyway.
            ===
            """);
        var picks = new List<SaliencyPick>();
        dialogue.Session.SaliencyPicked += picks.Add;

        await dialogue.RunAsync();

        var pick = Assert.Single(picks);
        Assert.True(pick.IsLineGroup);
        Assert.Equal("Start", pick.Group);
        Assert.Equal(["line:lovely", "line:puddles"], pick.Candidates.Select(c => c.Name));
        Assert.Equal([true, false], pick.Candidates.Select(c => c.Passed));
        Assert.All(pick.Candidates, c => Assert.Null(c.FailedCondition));
        Assert.Equal("line:lovely", pick.Chosen);
        Assert.Equal(["line: Odile: Lovely weather.", "line: Odile: Anyway.", "complete"], dialogue.Transcript);
    }

    [Fact]
    public async Task A_function_nothing_provides_gives_false_and_one_error()
    {
        var dialogue = new TestDialogue(
            """
            title: Start
            ---
            <<if standing("guild") > 3>>
                Clerk: Welcome back.
            <<else>>
                Clerk: Do I know you?
            <<endif>>
            <<if standing("town") > 3>>
                Clerk: The town speaks well of you.
            <<endif>>
            ===
            """,
            declarations: [new YarnFunctionDeclaration { Name = "standing", ParameterTypes = [YarnFunctionDeclaration.String], ReturnType = YarnFunctionDeclaration.Number }]);

        await dialogue.RunAsync();
        await dialogue.RunAsync();

        Assert.Equal(["line: Clerk: Do I know you?", "complete", "line: Clerk: Do I know you?", "complete"], dialogue.Transcript);
        var error = Assert.Single(dialogue.Errors);
        Assert.Contains("'standing'", error);
    }

    [Fact]
    public async Task The_functions_of_the_game_are_the_ones_of_the_moment_of_the_run()
    {
        var actions = new YarnActionRegistry();
        actions.AddFunction("price", (string item) => item == "lamp" ? 3f : 1f);
        var dialogue = new TestDialogue(
            """
            title: Start
            ---
            Kye: The lamp is {price("lamp")} coins.
            ===
            """,
            actions);

        await dialogue.RunAsync();
        actions.RemoveFunction("price");
        actions.AddFunction("price", (string item) => 10f);
        await dialogue.RunAsync();

        Assert.Equal(["line: Kye: The lamp is 3 coins.", "complete", "line: Kye: The lamp is 10 coins.", "complete"], dialogue.Transcript);
        Assert.Empty(dialogue.Errors);
    }

    [Fact]
    public async Task Wait_asks_the_clock_and_stop_ends_the_dialogue_without_the_responder()
    {
        var dialogue = new TestDialogue("""
            title: Start
            ---
            Kye: Hold on.
            <<wait 2>>
            <<wait 0.5>>
            Kye: There.
            <<wait soon>>
            <<stop>>
            Kye: Never said.
            ===
            """);

        await dialogue.RunAsync();

        Assert.Equal([TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(0.5)], dialogue.Clock.Delays);
        Assert.Equal(["line: Kye: Hold on.", "line: Kye: There.", "command: wait soon", "complete"], dialogue.Transcript);
    }

    [Fact]
    public async Task A_command_of_the_game_runs_before_the_built_in_one_and_the_responder()
    {
        var actions = new YarnActionRegistry();
        var rung = new List<string>();
        var waited = new List<float>();
        actions.AddCommandHandler("ring", (string what) => rung.Add(what));
        actions.AddCommandHandler("wait", (float seconds) => waited.Add(seconds));
        var dialogue = new TestDialogue(
            """
            title: Start
            ---
            <<ring bell>>
            <<wait 3>>
            <<ring>>
            <<wave>>
            Kye: There.
            ===
            """,
            actions);

        await dialogue.RunAsync();

        Assert.Equal(["bell"], rung);
        Assert.Equal([3f], waited);
        Assert.Empty(dialogue.Clock.Delays);

        // A command the game has but the script calls wrongly is an error in the log, and the dialogue goes on
        Assert.Equal(["command: wave", "line: Kye: There.", "complete"], dialogue.Transcript);
        var error = Assert.Single(dialogue.Errors);
        Assert.Contains("<<ring>>", error);
    }

    [Fact]
    public async Task Two_projects_share_one_storage()
    {
        var variables = new VariableStorage();
        var purse = new TestDialogue(
            """
            title: Start
            ---
            <<declare $coins = 5>>
            <<set $coins = $coins + 1>>
            Kye: {$coins} coins.
            ===
            """,
            variables: variables);
        var mood = new TestDialogue(
            """
            title: Start
            ---
            <<declare $mood = "calm">>
            <<declare $is_calm = $mood == "calm">>
            <<if $is_calm>>
                Odile: All is well.
            <<endif>>
            <<set $mood = "cross">>
            <<if not $is_calm>>
                Odile: Now I am {$mood}.
            <<endif>>
            ===
            """,
            variables: variables);

        await purse.RunAsync();
        await mood.RunAsync();
        await purse.RunAsync();

        Assert.Equal(["line: Kye: 6 coins.", "complete", "line: Kye: 7 coins.", "complete"], purse.Transcript);
        Assert.Equal(["line: Odile: All is well.", "line: Odile: Now I am cross.", "complete"], mood.Transcript);
        Assert.True(variables.TryGetValue<float>("$coins", out var coins));
        Assert.Equal(7f, coins);
        Assert.True(variables.TryGetValue<string>("$mood", out var now));
        Assert.Equal("cross", now);
    }

    private static async ValueTask Forever(CancellationToken cancellation) => await Task.Delay(Timeout.Infinite, cancellation);
}
