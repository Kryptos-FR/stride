using ColorRise.Saga.Content;
using ColorRise.Saga.Expressions;
using Stride.Core.Diagnostics;
using Xunit;
using static ColorRise.Saga.Tests.Runtime.StoryBuilder;

namespace ColorRise.Saga.Tests.Runtime;

public sealed class SagaRuntimeTests
{
    [Fact]
    public void StartEntersFirstAct()
    {
        var story = new StoryBuilder()
            .Variable("day", SagaTypeKind.Int, "1")
            .Act(new ActDefinition { Id = "act_1" })
            .Act(new ActDefinition { Id = "act_2" })
            .Build();
        var runtime = new SagaRuntime(story);
        var changes = Record(runtime);

        runtime.Start();

        Assert.Equal("act_1", runtime.State.CurrentActId);
        Assert.Equal(SagaValue.Int(1), runtime.Get("day"));
        Assert.Equal([new SagaChangedEventArgs(SagaChangeKind.ActEntered, "act_1")], changes);
    }

    [Fact]
    public void RaiseStartsScenario()
    {
        var story = new StoryBuilder()
            .Variable("day", SagaTypeKind.Int, "1")
            .Events("dawn")
            .Act(new ActDefinition
            {
                Id = "act_1",
                Scenarios = { new ScenarioDefinition { Id = "siege", StartWhen = C("raised(\"dawn\")"), OnStart = A("day = day + 1") } },
            })
            .Build();
        var runtime = new SagaRuntime(story);
        runtime.Start();
        Assert.Equal(ScenarioState.Pending, runtime.State.Scenarios["siege"].State);
        var changes = Record(runtime);

        runtime.Raise("dawn");

        Assert.Equal(ScenarioState.Running, runtime.State.Scenarios["siege"].State);
        Assert.Equal(SagaValue.Int(2), runtime.Get("day"));
        Assert.Equal([new SagaChangedEventArgs(SagaChangeKind.ScenarioStarted, "siege")], changes);
    }

    [Fact]
    public void ScenarioEndKeepsFirstMatchingOutcome()
    {
        var story = new StoryBuilder()
            .Variable("day", SagaTypeKind.Int, "5")
            .Events("end")
            .Act(new ActDefinition
            {
                Id = "act_1",
                Scenarios =
                {
                    new ScenarioDefinition
                    {
                        Id = "siege",
                        EndWhen = C("raised(\"end\")"),
                        Outcomes =
                        {
                            new ScenarioOutcome { Name = "easier", When = C("day > 10") },
                            new ScenarioOutcome { Name = "harder", When = C("day > 1") },
                            new ScenarioOutcome { Name = "lost", When = C("") },
                        },
                        OnEnd = A("day = 0"),
                    },
                },
            })
            .Build();
        var runtime = new SagaRuntime(story);
        runtime.Start();

        runtime.Raise("end");

        Assert.Equal(ScenarioState.Done, runtime.State.Scenarios["siege"].State);
        Assert.Equal("harder", runtime.State.Scenarios["siege"].Outcome);
        Assert.Equal(SagaValue.Int(0), runtime.Get("day"));
    }

    [Fact]
    public void QuestOfferedThenAcceptedByAction()
    {
        var story = new StoryBuilder()
            .Variable("accepted", SagaTypeKind.Bool)
            .Events("offer", "accept")
            .Act(new ActDefinition
            {
                Id = "act_1",
                Scenarios = { new ScenarioDefinition { Id = "accepting", StartWhen = C("raised(\"accept\")"), OnStart = A("quest.mine.state = active") } },
                Quests = { new QuestDefinition { Id = "mine", OfferWhen = C("raised(\"offer\")"), OnActivate = A("accepted = true") } },
            })
            .Build();
        var runtime = new SagaRuntime(story);
        runtime.Start();
        var changes = Record(runtime);

        runtime.Raise("offer");
        Assert.Equal(QuestState.Available, runtime.State.Quests["mine"].State);

        runtime.Raise("accept");
        Assert.Equal(QuestState.Active, runtime.State.Quests["mine"].State);
        Assert.Equal(SagaValue.Bool(true), runtime.Get("accepted"));
        Assert.Equal(
            [
                new SagaChangedEventArgs(SagaChangeKind.QuestAvailable, "mine"),
                new SagaChangedEventArgs(SagaChangeKind.ScenarioStarted, "accepting"),
                new SagaChangedEventArgs(SagaChangeKind.QuestActivated, "mine"),
            ],
            changes);
    }

    [Fact]
    public void QuestStagesAdvanceInOrder()
    {
        var story = new StoryBuilder()
            .Variable("log", SagaTypeKind.Int)
            .Events("a", "b")
            .Act(new ActDefinition
            {
                Id = "act_1",
                Quests =
                {
                    new QuestDefinition
                    {
                        Id = "dig",
                        StartsAutomatically = true,
                        Stages =
                        {
                            new QuestStage { Id = "first", DoneWhen = C("raised(\"a\")"), OnDone = A("log = log + 1") },
                            new QuestStage { Id = "second", DoneWhen = C("raised(\"b\")"), OnDone = A("log = log + 10") },
                        },
                    },
                },
            })
            .Build();
        var runtime = new SagaRuntime(story);
        runtime.Start();
        Assert.Equal(QuestState.Active, runtime.State.Quests["dig"].State);
        var changes = Record(runtime);

        runtime.Raise("b");
        Assert.Equal(0, runtime.State.Quests["dig"].Stage);
        Assert.Empty(changes);

        runtime.Raise("a");
        Assert.Equal(2, runtime.State.Quests["dig"].Stage);
        Assert.Equal(QuestState.Succeeded, runtime.State.Quests["dig"].State);
        Assert.Equal(SagaValue.Int(11), runtime.Get("log"));
        Assert.Equal(
            [
                new SagaChangedEventArgs(SagaChangeKind.QuestStageDone, "dig"),
                new SagaChangedEventArgs(SagaChangeKind.QuestStageDone, "dig"),
                new SagaChangedEventArgs(SagaChangeKind.QuestSucceeded, "dig"),
            ],
            changes);
    }

    [Fact]
    public void QuestFailsWhenItsScenarioEndsWithTheAct()
    {
        var runtime = new SagaRuntime(TwoActs(continues: false));
        runtime.Start();
        Assert.Equal(QuestState.Active, runtime.State.Quests["hold"].State);
        Assert.Equal(QuestState.Available, runtime.State.Quests["scout"].State);

        runtime.Raise("end");

        Assert.Equal("act_2", runtime.State.CurrentActId);
        Assert.Equal(["act_1"], runtime.State.CompletedActs);
        Assert.Equal(ScenarioState.Done, runtime.State.Scenarios["front"].State);
        Assert.Equal(QuestState.Failed, runtime.State.Quests["hold"].State);
        Assert.Equal(QuestState.NotStarted, runtime.State.Quests["scout"].State);
    }

    [Fact]
    public void ContinuingScenarioSurvivesActChange()
    {
        var runtime = new SagaRuntime(TwoActs(continues: true));
        runtime.Start();

        runtime.Raise("end");

        Assert.Equal("act_2", runtime.State.CurrentActId);
        Assert.Equal(ScenarioState.Running, runtime.State.Scenarios["front"].State);
        Assert.Equal(QuestState.Active, runtime.State.Quests["hold"].State);
    }

    [Fact]
    public void ActBranchFollowsFirstTrueLink()
    {
        var story = new StoryBuilder()
            .Variable("sided_with", SagaTypeKind.String)
            .Events("choose")
            .Act(new ActDefinition
            {
                Id = "act_1",
                EndWhen = C("raised(\"choose\")"),
                Next =
                {
                    new ActLink { TargetActId = "guild", When = C("sided_with == \"guild\"") },
                    new ActLink { TargetActId = "crown", When = C("") },
                },
            })
            .Act(new ActDefinition { Id = "crown" })
            .Act(new ActDefinition { Id = "guild" })
            .Build();
        var runtime = new SagaRuntime(story);
        runtime.Start();
        var changes = Record(runtime);

        runtime.Set("sided_with", SagaValue.String("guild"));
        runtime.Raise("choose");

        Assert.Equal("guild", runtime.State.CurrentActId);
        Assert.Equal(
            [new SagaChangedEventArgs(SagaChangeKind.ActEnded, "act_1"), new SagaChangedEventArgs(SagaChangeKind.ActEntered, "guild")],
            changes);
    }

    [Fact]
    public void StoryEndsWhenNoLinkHolds()
    {
        var story = new StoryBuilder()
            .Events("end")
            .Act(new ActDefinition { Id = "act_1", EndWhen = C("raised(\"end\")") })
            .Build();
        var runtime = new SagaRuntime(story);
        runtime.Start();

        runtime.Raise("end");

        Assert.Null(runtime.State.CurrentActId);
        Assert.True(runtime.IsOver);
    }

    [Fact]
    public void SignalsReachHandlersInOrder()
    {
        var story = new StoryBuilder()
            .Events("go")
            .Signal("give_item", SagaTypeKind.String, SagaTypeKind.Int)
            .Signal("play_sound", SagaTypeKind.String)
            .Act(new ActDefinition
            {
                Id = "act_1",
                Scenarios = { new ScenarioDefinition { Id = "gift", StartWhen = C("raised(\"go\")"), OnStart = A("emit give_item(\"sword\", 2); emit play_sound(\"fanfare\"); emit give_item(\"shield\", 1)") } },
            })
            .Build();
        var runtime = new SagaRuntime(story);
        var received = new List<string>();
        using var gifts = runtime.On("give_item", x => received.Add($"{x.Name}({string.Join(", ", x.Arguments)})"));
        var sounds = runtime.On("play_sound", x => received.Add($"{x.Name}({string.Join(", ", x.Arguments)})"));
        runtime.Start();

        runtime.Raise("go");

        Assert.Equal(["give_item(\"sword\", 2)", "play_sound(\"fanfare\")", "give_item(\"shield\", 1)"], received);
        sounds.Dispose();
    }

    [Fact]
    public void HandlersRunAfterSettling()
    {
        var story = new StoryBuilder()
            .Variable("day", SagaTypeKind.Int)
            .Events("go")
            .Signal("ping")
            .Act(new ActDefinition
            {
                Id = "act_1",
                Scenarios = { new ScenarioDefinition { Id = "s", StartWhen = C("raised(\"go\")"), OnStart = A("emit ping; day = 3") } },
            })
            .Build();
        var runtime = new SagaRuntime(story);
        SagaValue seen = default;
        runtime.On("ping", _ => seen = runtime.Get("day"));
        runtime.Start();

        runtime.Raise("go");

        Assert.Equal(SagaValue.Int(3), seen);
    }

    [Fact]
    public void UnknownVariableThrows()
    {
        var story = new StoryBuilder()
            .Variable("day", SagaTypeKind.Int)
            .Act(new ActDefinition { Id = "act_1" })
            .Build();
        var runtime = new SagaRuntime(story);
        runtime.Start();

        Assert.Contains("nope", Assert.Throws<ArgumentException>(() => runtime.Set("nope", SagaValue.Int(1))).Message);
        Assert.Throws<ArgumentException>(() => runtime.Get("nope"));
        Assert.Throws<ArgumentException>(() => runtime.Raise("nope"));
        Assert.Throws<ArgumentException>(() => runtime.On("nope", _ => { }));
        Assert.Throws<ArgumentException>(() => runtime.RegisterFunction("nope", _ => default));
        Assert.Throws<ArgumentException>(() => runtime.Set("day", SagaValue.String("monday")));
    }

    [Fact]
    public void UnregisteredFunctionReturnsDefault()
    {
        var story = new StoryBuilder()
            .Events("tick")
            .Function("has_item", SagaTypeKind.Bool, SagaTypeKind.String)
            .Act(new ActDefinition
            {
                Id = "act_1",
                Scenarios = { new ScenarioDefinition { Id = "armed", StartWhen = C("raised(\"tick\") && has_item(\"sword\")") } },
            })
            .Build();
        var logger = new LoggerResult();
        var runtime = new SagaRuntime(story, logger: logger);
        runtime.Start();

        runtime.Raise("tick");
        runtime.Raise("tick");

        Assert.Equal(ScenarioState.Pending, runtime.State.Scenarios["armed"].State);
        var message = Assert.Single(logger.Messages);
        Assert.Equal(LogMessageType.Error, message.Type);
        Assert.Contains("has_item", message.Text);

        runtime.RegisterFunction("has_item", x => SagaValue.Bool(x[0].AsString() == "sword"));
        runtime.Raise("tick");
        Assert.Equal(ScenarioState.Running, runtime.State.Scenarios["armed"].State);
    }

    [Fact]
    public void SettlingThatNeverStopsThrows()
    {
        var story = new StoryBuilder()
            .Act(new ActDefinition
            {
                Id = "act_1",
                Quests = { new QuestDefinition { Id = "restless", StartsAutomatically = true, OnActivate = A("quest.restless.state = not_started") } },
            })
            .Build();
        var runtime = new SagaRuntime(story);

        var exception = Assert.Throws<SagaException>(runtime.Start);

        Assert.Contains("quest restless", exception.Message);
        Assert.Contains("100", exception.Message);
    }

    [Fact]
    public void RestoreDropsUnknownAndDefaultsNew()
    {
        var story = new StoryBuilder()
            .Variable("day", SagaTypeKind.Int, "1")
            .Variable("added", SagaTypeKind.Int, "7")
            .Variable("speed", SagaTypeKind.Float, "1.5")
            .Act(new ActDefinition
            {
                Id = "act_1",
                Scenarios = { new ScenarioDefinition { Id = "kept", StartWhen = C("day > 100") }, new ScenarioDefinition { Id = "new_one", StartWhen = C("day > 100") } },
                Quests = { new QuestDefinition { Id = "q", OfferWhen = C("day > 100") } },
            })
            .Build();
        var saved = new SagaState
        {
            CurrentActId = "act_1",
            Variables = { ["day"] = SagaValue.Int(5), ["gone"] = SagaValue.Int(1), ["speed"] = SagaValue.String("fast") },
            Scenarios = { ["kept"] = new ScenarioProgress { State = ScenarioState.Running }, ["gone_s"] = new ScenarioProgress() },
            Quests = { ["gone_q"] = new QuestProgress { State = QuestState.Active } },
        };
        var logger = new LoggerResult();

        var runtime = new SagaRuntime(story, saved, logger);
        runtime.Start();

        Assert.Equal(SagaValue.Int(5), runtime.Get("day"));
        Assert.Equal(SagaValue.Int(7), runtime.Get("added"));
        Assert.Equal(SagaValue.Float(1.5f), runtime.Get("speed"));
        Assert.False(runtime.State.Variables.ContainsKey("gone"));
        Assert.Equal(ScenarioState.Running, runtime.State.Scenarios["kept"].State);
        Assert.Equal(ScenarioState.Pending, runtime.State.Scenarios["new_one"].State);
        Assert.False(runtime.State.Scenarios.ContainsKey("gone_s"));
        Assert.Equal(QuestState.NotStarted, runtime.State.Quests["q"].State);
        Assert.False(runtime.State.Quests.ContainsKey("gone_q"));
        Assert.Equal("act_1", runtime.State.CurrentActId);
        Assert.NotEmpty(logger.Messages);
    }

    [Fact]
    public void RestoringLeavesTheSavedStateAsItWas()
    {
        var story = TwoActs(continues: true);
        var original = new SagaRuntime(story);
        original.Start();
        original.Raise("scouted");
        var saved = original.CaptureState();
        var before = Describe(saved);

        // A quick-save slot restored, played on, then restored again
        var restored = new SagaRuntime(story, saved);
        restored.Start();
        restored.Raise("end");
        Assert.Equal(before, Describe(saved));
        var again = new SagaRuntime(story, saved);
        again.Start();
        Assert.Equal("act_1", again.State.CurrentActId);
    }

    [Fact]
    public void CaptureThenRestoreGivesSameBehavior()
    {
        var story = TwoActs(continues: true);
        var original = new SagaRuntime(story);
        original.Start();
        original.Raise("scouted");
        var saved = original.CaptureState();
        original.Raise("end");

        var restored = new SagaRuntime(story, saved);
        restored.Start();
        Assert.Equal("act_1", restored.State.CurrentActId);
        restored.Raise("end");

        Assert.Equal(Describe(original.State), Describe(restored.State));
        Assert.NotSame(saved, original.State);
    }

    private static StoryDefinition TwoActs(bool continues) => new StoryBuilder()
        .Events("end", "scouted")
        .Act(new ActDefinition
        {
            Id = "act_1",
            EndWhen = C("raised(\"end\")"),
            Next = { new ActLink { TargetActId = "act_2" } },
            Scenarios = { new ScenarioDefinition { Id = "front", ContinuesAcrossActs = continues } },
            Quests =
            {
                new QuestDefinition { Id = "hold", ScenarioId = "front", StartsAutomatically = true },
                new QuestDefinition { Id = "scout", Stages = { new QuestStage { Id = "look", DoneWhen = C("raised(\"scouted\")") } } },
            },
        })
        .Act(new ActDefinition { Id = "act_2" })
        .Build();

    private static List<SagaChangedEventArgs> Record(SagaRuntime runtime)
    {
        var changes = new List<SagaChangedEventArgs>();
        runtime.Changed += (_, e) => changes.Add(e);
        return changes;
    }

    private static string Describe(SagaState state) => string.Join(
        "|",
        state.CurrentActId,
        string.Join(",", state.CompletedActs),
        string.Join(",", state.Variables.OrderBy(x => x.Key).Select(x => $"{x.Key}={x.Value}")),
        string.Join(",", state.Quests.OrderBy(x => x.Key).Select(x => $"{x.Key}={x.Value.State}/{x.Value.Stage}")),
        string.Join(",", state.Scenarios.OrderBy(x => x.Key).Select(x => $"{x.Key}={x.Value.State}/{x.Value.Outcome}")),
        string.Join(",", state.Events.OrderBy(x => x.Key).Select(x => $"{x.Key}={x.Value}")));
}
