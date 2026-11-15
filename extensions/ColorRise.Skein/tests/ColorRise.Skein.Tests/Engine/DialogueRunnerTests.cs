using Stride.Core.Diagnostics;
using Stride.Engine;

namespace ColorRise.Skein.Tests.Engine;

/// <summary>
/// A runner runs dialogues in a game, as micro-threads of its scripts, and its presenters show them.
/// </summary>
[Collection(GameCollection.Name)]
public sealed class DialogueRunnerTests
{
    private const string Post = """
        title: Start
        ---
        Odile: The post is late.
        -> Wait
            Odile: Then we wait.
        -> Leave
            Odile: Go on, then.
        <<ring bell>>
        Odile: Done.
        ===
        """;

    private static Task<int?> Choose(int? index) => Task.FromResult(index);

    private static T Refused<T>(DialogueRunnerComponent runner, string node)
        where T : Exception
    {
        // The refusal is thrown at the call, not through the task
        Exception? refusal = null;
        try
        {
            _ = runner.StartDialogue(node);
        }
        catch (Exception exception)
        {
            refusal = exception;
        }

        return Assert.IsType<T>(refusal);
    }

    private static async Task<int?> Never(CancellationToken cancellation)
    {
        await Task.Delay(Timeout.Infinite, cancellation);
        return 0;
    }

    [Fact]
    public void A_dialogue_tells_its_presenters_and_its_listeners_in_order()
    {
        using var harness = new GameHarness();
        var presenter = new RecordingPresenter { Options = (_, _) => Choose(1) };
        var runner = harness.AddRunner(Post, "Runner", presenter);
        var seen = presenter.Seen;
        var runningAtTheEnd = true;
        runner.DialogueStarted += () => seen.Add("DialogueStarted");
        runner.NodeStarted += node => seen.Add($"NodeStarted: {node}");
        runner.NodeCompleted += node => seen.Add($"NodeCompleted: {node}");
        runner.UnhandledCommand += command => seen.Add($"UnhandledCommand: {command}");
        runner.DialogueCompleted += () =>
        {
            seen.Add("DialogueCompleted");
            runningAtTheEnd = runner.IsRunning;
        };
        harness.Step();

        var dialogue = runner.StartDialogue("Start");
        Assert.True(runner.IsRunning);
        Assert.False(dialogue.IsCompleted);
        harness.Step();

        Assert.True(dialogue.IsCompletedSuccessfully);
        Assert.Equal(
            [
                "DialogueStarted",
                "started",
                "NodeStarted: Start",
                "enter: Start",
                "line: Odile: The post is late.",
                "options: Wait | Leave",
                "line: Odile: Go on, then.",
                "UnhandledCommand: ring bell",
                "line: Odile: Done.",
                "NodeCompleted: Start",
                "exit: Start",
                "completed",
                "DialogueCompleted",
            ],
            seen);
        Assert.False(runningAtTheEnd);
        Assert.False(runner.IsRunning);
        Assert.Null(runner.CurrentNode);
        Assert.Same(runner, presenter.Runner);
        Assert.Empty(harness.Logged(LogMessageType.Warning));
        Assert.Empty(harness.Logged(LogMessageType.Error));
    }

    [Fact]
    public void A_line_goes_to_every_presenter_and_the_dialogue_waits_for_all()
    {
        using var harness = new GameHarness();
        var first = new RecordingPresenter();
        var second = new RecordingPresenter { Options = (_, _) => Choose(0) };
        first.LineGates.Enqueue(new TaskCompletionSource());
        second.LineGates.Enqueue(new TaskCompletionSource());
        var firstGate = first.LineGates.Peek();
        var secondGate = second.LineGates.Peek();
        var runner = harness.AddRunner(Post, "Runner", first, second);
        harness.Step();

        var dialogue = runner.StartDialogue("Start");
        harness.Step();
        Assert.Equal(["started", "enter: Start", "line: Odile: The post is late."], first.Seen);
        Assert.Equal(first.Seen, second.Seen);
        Assert.Equal("Start", runner.CurrentNode);

        // One presenter is done: the dialogue still waits for the other
        firstGate.SetResult();
        harness.Step(3);
        Assert.Equal(3, first.Seen.Count);
        Assert.Equal(3, second.Seen.Count);

        secondGate.SetResult();
        harness.Step(3);
        Assert.True(dialogue.IsCompletedSuccessfully);
        Assert.Equal("options: Wait | Leave", first.Seen[3]);
        Assert.Equal(["line: Odile: Then we wait.", "line: Odile: Done.", "exit: Start", "completed"], first.Seen[4..]);
        Assert.Equal(first.Seen, second.Seen);
    }

    [Fact]
    public void Two_runners_run_at_once_each_its_own_dialogue()
    {
        using var harness = new GameHarness();
        var talk = new RecordingPresenter();
        var chatter = new RecordingPresenter();
        var talkGates = new[] { new TaskCompletionSource(), new TaskCompletionSource() };
        var chatterGates = new[] { new TaskCompletionSource(), new TaskCompletionSource() };
        Array.ForEach(talkGates, talk.LineGates.Enqueue);
        Array.ForEach(chatterGates, chatter.LineGates.Enqueue);
        var talking = harness.AddRunner("title: Start\n---\n<<declare $said = 0>>\nOdile: One.\n<<set $said = $said + 1>>\nOdile: Two.\n===\n", "Talk", talk);
        var chattering = harness.AddRunner("title: Start\n---\n<<declare $said = 0>>\nGull: Kree.\n<<set $said = $said + 10>>\nGull: Kraa.\n===\n", "Chatter", chatter);
        harness.Step();

        var first = talking.StartDialogue("Start");
        var second = chattering.StartDialogue("Start");
        harness.Step();
        Assert.Equal("line: Odile: One.", talk.Seen[^1]);
        Assert.Equal("line: Gull: Kree.", chatter.Seen[^1]);

        chatterGates[0].SetResult();
        harness.Step(2);
        Assert.Equal("line: Odile: One.", talk.Seen[^1]);
        Assert.Equal("line: Gull: Kraa.", chatter.Seen[^1]);

        talkGates[0].SetResult();
        talkGates[1].SetResult();
        harness.Step(2);
        Assert.True(first.IsCompletedSuccessfully);
        Assert.False(second.IsCompleted);
        Assert.True(chattering.IsRunning);

        chatterGates[1].SetResult();
        harness.Step(2);
        Assert.True(second.IsCompletedSuccessfully);
        Assert.DoesNotContain(talk.Seen, s => s.Contains("Gull"));
        Assert.DoesNotContain(chatter.Seen, s => s.Contains("Odile"));

        // Both wrote the game's variables
        Assert.Same(harness.Dialogues.Variables, talking.Variables);
        Assert.True(harness.Dialogues.Variables.TryGetValue<float>("$said", out var said));
        Assert.Equal(11f, said);
    }

    [Fact]
    public void A_runner_with_variables_of_its_own_leaves_the_game_s_alone()
    {
        using var harness = new GameHarness();
        var runner = harness.AddRunner("title: Start\n---\n<<declare $said = 0>>\n<<set $said = 5>>\n===\n");
        runner.VariablesOverride = new VariableStorage();
        harness.Step();

        runner.StartDialogue("Start");
        harness.Step();

        Assert.True(runner.Variables.TryGetValue<float>("$said", out var said));
        Assert.Equal(5f, said);
        Assert.DoesNotContain("$said", harness.Dialogues.Variables.StoredNames);
    }

    [Fact]
    public void Stop_during_options_cancels_the_presenters_and_ends_the_dialogue()
    {
        using var harness = new GameHarness();
        var presenter = new RecordingPresenter { Options = (_, cancellation) => Never(cancellation) };
        var runner = harness.AddRunner(Post, "Runner", presenter);
        var ends = 0;
        runner.DialogueCompleted += () => ends++;
        harness.Step();

        var dialogue = runner.StartDialogue("Start");
        harness.Step();
        Assert.Equal("options: Wait | Leave", presenter.Seen[^1]);

        runner.Stop();
        harness.Step(3);

        Assert.True(dialogue.IsCompletedSuccessfully);
        Assert.Equal(["options cancelled", "completed"], presenter.Seen[^2..]);
        Assert.Equal(1, ends);
        Assert.False(runner.IsRunning);

        // The runner starts again, from the start
        presenter.Options = (_, _) => Choose(0);
        presenter.Seen.Clear();
        var again = runner.StartDialogue("Start");
        harness.Step();
        Assert.True(again.IsCompletedSuccessfully);
        Assert.Contains("line: Odile: Done.", presenter.Seen);
        Assert.Equal(2, ends);
    }

    [Fact]
    public void The_first_presenter_that_chooses_answers_and_the_others_are_told_to_leave()
    {
        using var harness = new GameHarness();
        var waiting = new RecordingPresenter { Options = (_, cancellation) => Never(cancellation) };
        var silent = new RecordingPresenter();
        var choice = new TaskCompletionSource<int?>();
        var choosing = new RecordingPresenter { Options = (_, _) => choice.Task };
        var runner = harness.AddRunner(Post, "Runner", waiting, silent, choosing);
        harness.Step();

        var dialogue = runner.StartDialogue("Start");
        harness.Step(2);
        Assert.False(dialogue.IsCompleted);

        choice.SetResult(1);
        harness.Step(3);

        Assert.True(dialogue.IsCompletedSuccessfully);
        Assert.Contains("options cancelled", waiting.Seen);
        Assert.Contains("line: Odile: Go on, then.", silent.Seen);
        Assert.Empty(harness.Logged(LogMessageType.Error));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Options_that_are_all_unavailable_are_skipped_or_shown_as_the_runner_says(bool fallThrough)
    {
        using var harness = new GameHarness();
        var presenter = new RecordingPresenter();
        var runner = harness.AddRunner(
            """
            title: Start
            ---
            <<declare $gold = 0>>
            -> Bribe the guard <<if $gold > 5>>
                Guard: Thank you.
            Guard: Move along.
            -> Bribe him twice <<if $gold > 10>>
                Guard: Thank you, thank you.
            Guard: Still here?
            ===
            """,
            "Runner",
            presenter);
        runner.OptionsFallThrough = fallThrough;
        harness.Step();

        var dialogue = runner.StartDialogue("Start");
        harness.Step();

        Assert.True(dialogue.IsCompletedSuccessfully);
        if (fallThrough)
        {
            Assert.Equal(["line: Guard: Move along.", "line: Guard: Still here?"], presenter.Seen.Where(s => s.StartsWith("line", StringComparison.Ordinal) || s.StartsWith("options", StringComparison.Ordinal)));
            Assert.Empty(harness.Logged(LogMessageType.Error));
        }
        else
        {
            // Shown, and nobody chose: the dialogue goes on, and the log says so once
            Assert.Equal(
                ["options: Bribe the guard [disabled]", "line: Guard: Move along.", "options: Bribe him twice [disabled]", "line: Guard: Still here?"],
                presenter.Seen.Where(s => s.StartsWith("line", StringComparison.Ordinal) || s.StartsWith("options", StringComparison.Ordinal)));
            var error = Assert.Single(harness.Logged(LogMessageType.Error));
            Assert.Contains("No presenter", error);
        }
    }

    [Fact]
    public void A_command_nothing_handles_is_a_warning_once_per_command_without_a_listener()
    {
        using var harness = new GameHarness();
        var runner = harness.AddRunner("title: Start\n---\n<<ring bell>>\n<<ring gong>>\n<<wave>>\n===\n");
        harness.Step();

        runner.StartDialogue("Start");
        harness.Step();

        var warnings = harness.Logged(LogMessageType.Warning).ToList();
        Assert.Equal(2, warnings.Count);
        Assert.Contains("<<ring bell>>", warnings[0]);
        Assert.Contains("[YarnCommand(\"ring\")]", warnings[0]);
        Assert.Contains("<<wave>>", warnings[1]);
    }

    [Fact]
    public void A_command_finds_its_target_among_the_actors_of_the_scene()
    {
        using var harness = new GameHarness();
        var named = harness.Add("Lamp", new LampComponent());
        var actor = harness.Add("Prop_7", new LampComponent(), new DialogueActorComponent { ActorName = "Lamp" });
        var runner = harness.AddRunner("title: Start\n---\n<<light Lamp>>\n<<light Lantern>>\nKye: There.\n===\n");
        harness.Step();

        var dialogue = runner.StartDialogue("Start");
        harness.Step();

        Assert.True(dialogue.IsCompletedSuccessfully);
        Assert.True(actor.Get<LampComponent>().IsLit);
        Assert.False(named.Get<LampComponent>().IsLit);

        // A target the scene lacks is an error in the log, and the dialogue goes on
        var error = Assert.Single(harness.Logged(LogMessageType.Error));
        Assert.Contains("<<light Lantern>>", error);
    }

    [Fact]
    public void A_runner_set_to_start_by_itself_starts_with_its_scene()
    {
        using var harness = new GameHarness();
        var presenter = new RecordingPresenter();
        var runner = harness.AddRunner("title: Start\n---\nKye: Not this one.\n===\ntitle: Morning\n---\nKye: Up already?\n===\n", "Runner", presenter);
        runner.AutoStart = true;
        runner.StartNode = "Morning";
        var idle = harness.AddRunner(Post, "Idle");
        idle.AutoStart = true;

        harness.Step(2);

        Assert.Equal(["started", "enter: Morning", "line: Kye: Up already?", "exit: Morning", "completed"], presenter.Seen);
        Assert.False(idle.IsRunning);
        var warning = Assert.Single(harness.Logged(LogMessageType.Warning));
        Assert.Contains("'Idle'", warning);
    }

    [Fact]
    public void A_wait_counts_the_time_of_the_game()
    {
        using var harness = new GameHarness();
        var presenter = new RecordingPresenter();
        var runner = harness.AddRunner("title: Start\n---\nKye: Hold on.\n<<wait 0.5>>\nKye: There.\n===\n", "Runner", presenter);
        harness.Step();

        var dialogue = runner.StartDialogue("Start");
        harness.Step(3, seconds: 0.2);
        Assert.DoesNotContain("line: Kye: There.", presenter.Seen);

        harness.Step(1, seconds: 0.2);
        Assert.Contains("line: Kye: There.", presenter.Seen);
        Assert.True(dialogue.IsCompletedSuccessfully);
    }

    [Fact]
    public void Hurry_up_and_next_line_reach_the_presenters()
    {
        using var harness = new GameHarness();
        var presenter = new RecordingPresenter();
        presenter.LineGates.Enqueue(new TaskCompletionSource());
        var runner = harness.AddRunner("title: Start\n---\nKye: One.\nKye: Two.\n===\n", "Runner", presenter);
        harness.Step();

        var dialogue = runner.StartDialogue("Start");
        harness.Step();
        runner.RequestHurryUp();
        harness.Step(2);
        Assert.Equal("line: Kye: One.", presenter.Seen[^1]);

        runner.RequestNextLine();
        harness.Step(2);
        Assert.True(dialogue.IsCompletedSuccessfully);
        Assert.Contains("line: Kye: Two.", presenter.Seen);
    }

    [Fact]
    public void The_runner_picks_as_its_project_says_unless_it_says_itself()
    {
        const string Script = """
            title: Start
            ---
            <<declare $tide = "low">>
            <<jump Gulls>>
            ===
            title: Gulls
            when: always
            ---
            Gulls wheel overhead.
            ===
            title: Gulls
            when: $tide == "low"
            ---
            Gulls pick at the wet sand.
            ===
            """;
        using var harness = new GameHarness();
        var presenter = new RecordingPresenter();
        var runner = harness.AddRunner(Script, "Runner", presenter);
        runner.Project!.Saliency = YarnSaliency.First;
        harness.Step();

        runner.StartDialogue("Start");
        harness.Step();
        runner.Saliency = YarnSaliency.Best;
        runner.StartDialogue("Start");
        harness.Step();

        Assert.Equal(["line: Gulls wheel overhead.", "line: Gulls pick at the wet sand."], presenter.Seen.Where(s => s.StartsWith("line", StringComparison.Ordinal)));
        Assert.True(runner.HasSalientContent("Gulls"));
    }

    [Fact]
    public void A_listener_of_the_end_can_start_the_next_dialogue()
    {
        using var harness = new GameHarness();
        var presenter = new RecordingPresenter();
        var runner = harness.AddRunner("title: Start\n---\nKye: One.\n===\ntitle: Next\n---\nKye: Two.\n===\n", "Runner", presenter);
        var chained = false;
        runner.DialogueCompleted += () =>
        {
            if (!chained)
            {
                chained = true;
                runner.StartDialogue("Next");
            }
        };
        harness.Step();

        runner.StartDialogue("Start");
        harness.Step(3);

        Assert.Equal(["line: Kye: One.", "line: Kye: Two."], presenter.Seen.Where(s => s.StartsWith("line", StringComparison.Ordinal)));
        Assert.False(runner.IsRunning);
    }

    [Fact]
    public void A_failure_of_a_presenter_ends_the_dialogue_and_is_an_error_of_the_game()
    {
        using var harness = new GameHarness();
        var presenter = new RecordingPresenter { LineFailure = new FormatException("no font") };
        var runner = harness.AddRunner(Post, "Runner", presenter);
        var ends = 0;
        runner.DialogueCompleted += () => ends++;
        harness.Step();

        var dialogue = runner.StartDialogue("Start");
        var thrown = Assert.Throws<FormatException>(() => harness.Step());

        Assert.Equal("no font", thrown.Message);
        Assert.True(dialogue.IsFaulted);
        Assert.Same(thrown, dialogue.Exception!.InnerException);
        Assert.Equal(1, ends);
        Assert.False(runner.IsRunning);
        Assert.Equal("completed", presenter.Seen[^1]);
    }

    [Fact]
    public void Starting_what_cannot_start_is_refused_with_the_reason()
    {
        using var harness = new GameHarness();
        var presenter = new RecordingPresenter { Options = (_, cancellation) => Never(cancellation) };
        var runner = harness.AddRunner(Post, "Runner", presenter);

        var outside = new DialogueRunnerComponent { Project = runner.Project };
        Assert.Contains("not in a scene", Refused<InvalidOperationException>(outside, "Start").Message);

        harness.Step();
        Assert.Contains("'Nowhere'", Refused<ArgumentException>(runner, "Nowhere").Message);
        Assert.False(runner.IsRunning);

        var empty = new DialogueRunnerComponent();
        harness.Add("Empty", empty);
        harness.Step();
        Assert.Contains("no Yarn project", Refused<InvalidOperationException>(empty, "Start").Message);

        runner.StartDialogue("Start");
        harness.Step();
        Assert.Contains("already running", Refused<InvalidOperationException>(runner, "Start").Message);
        runner.Stop();
        harness.Step(3);
    }

    [Fact]
    public void A_runner_that_leaves_the_scene_stops_its_dialogue()
    {
        using var harness = new GameHarness();
        var presenter = new RecordingPresenter { Options = (_, cancellation) => Never(cancellation) };
        var runner = harness.AddRunner(Post, "Runner", presenter);
        harness.Step();
        var dialogue = runner.StartDialogue("Start");
        harness.Step();

        harness.Scene.Entities.Remove(runner.Entity);
        harness.Step(3);

        Assert.True(dialogue.IsCompletedSuccessfully);
        Assert.False(runner.IsRunning);
        Assert.Contains("options cancelled", presenter.Seen);
    }
}
