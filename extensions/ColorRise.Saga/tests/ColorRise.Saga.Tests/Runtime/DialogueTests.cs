using ColorRise.Saga.Content;
using ColorRise.Saga.Expressions;
using Stride.Core.Diagnostics;
using Xunit;
using static ColorRise.Saga.Tests.Runtime.StoryBuilder;

namespace ColorRise.Saga.Tests.Runtime;

public sealed class DialogueTests
{
    [Fact]
    public void TalkPicksHighestPriorityActive()
    {
        var story = Story(new ActDefinition
        {
            Id = "act_1",
            Scenarios =
            {
                new ScenarioDefinition
                {
                    Id = "s",
                    Dialogues =
                    {
                        Dialogue("low", priority: 1),
                        Dialogue("high", priority: 5, when: "day > 3"),
                        Dialogue("mid", priority: 3),
                        Dialogue("tie", priority: 3),
                        Dialogue("other_character", priority: 9, character: "bram"),
                    },
                },
            },
        });
        var runtime = Started(story);

        Assert.Equal("mid", runtime.Talk("mara")?.Dialogue.Name);

        runtime.Set("day", SagaValue.Int(5));
        Assert.Equal("high", runtime.Talk("mara")?.Dialogue.Name);
        Assert.Throws<ArgumentException>(() => runtime.Talk("nobody"));
    }

    [Fact]
    public void QuestGiverDialogueAvailableBeforeQuestIsActive()
    {
        var giver = Dialogue("giver", nodes:
        [
            new ChoicesNode
            {
                Id = "offer",
                Choices =
                {
                    new DialogueChoice { Text = "I'll do it", When = C("quest.mine.state == available"), Actions = A("quest.mine.state = active") },
                    new DialogueChoice { Text = "Done", When = C("quest.mine.state == active"), Actions = A("quest.mine.state = succeeded") },
                },
            },
        ]);
        var story = Story(new ActDefinition
        {
            Id = "act_1",
            Quests = { new QuestDefinition { Id = "mine", OfferWhen = C("raised(\"offer\")"), Dialogues = { giver } } },
        });
        var runtime = Started(story);
        Assert.Null(runtime.Talk("mara"));

        runtime.Raise("offer");
        var session = Assert.IsType<DialogueSession>(runtime.Talk("mara"));
        session.Choose(Assert.Single(session.Choices).Index);
        Assert.Equal(QuestState.Active, runtime.State.Quests["mine"].State);

        session = Assert.IsType<DialogueSession>(runtime.Talk("mara"));
        Assert.Equal("Done", Assert.Single(session.Choices).Text);
        session.Choose(1);

        Assert.Equal(QuestState.Succeeded, runtime.State.Quests["mine"].State);
        Assert.Null(runtime.Talk("mara"));
    }

    [Fact]
    public void OwnerGateCanBeTurnedOff()
    {
        var story = Story(new ActDefinition
        {
            Id = "act_1",
            Scenarios =
            {
                new ScenarioDefinition
                {
                    Id = "later",
                    StartWhen = C("raised(\"offer\")"),
                    Dialogues = { Dialogue("gated", priority: 5), Dialogue("free", priority: 1, ownerGate: false) },
                },
            },
        });
        var runtime = Started(story);

        Assert.Equal("free", runtime.Talk("mara")?.Dialogue.Name);

        runtime.Raise("offer");
        Assert.Equal("gated", runtime.Talk("mara")?.Dialogue.Name);
    }

    [Fact]
    public void OnceDialogueIsUsedUp()
    {
        var once = Dialogue("intro", priority: 5);
        once.Once = true;
        var story = Story(new ActDefinition
        {
            Id = "act_1",
            Scenarios = { new ScenarioDefinition { Id = "s", Dialogues = { once, Dialogue("small_talk") } } },
        });
        var runtime = Started(story);

        Assert.Equal("intro", runtime.Talk("mara")?.Dialogue.Name);
        Assert.Equal("small_talk", runtime.Talk("mara")?.Dialogue.Name);
        Assert.Contains("intro", runtime.State.UsedOnce);
    }

    [Fact]
    public void HubLoopsAndOnceChoiceDisappears()
    {
        var dialogue = Dialogue("mara", nodes:
        [
            new LineNode { Id = "hello", SpeakerId = "mara", Text = "You came back.", NextId = "hub" },
            new ChoicesNode
            {
                Id = "hub",
                Name = "hub",
                Choices =
                {
                    new DialogueChoice { Text = "Ask about the mine", Once = true, TargetId = "mine" },
                    new DialogueChoice { Text = "Leave", TargetId = "end" },
                },
            },
            new LineNode { Id = "mine", SpeakerId = "mara", Text = "It collapsed.", NextId = "hub" },
            new EndNode { Id = "end" },
        ]);
        var runtime = Started(Story(new ActDefinition { Id = "act_1", Scenarios = { new ScenarioDefinition { Id = "s", Dialogues = { dialogue } } } }));

        var session = Assert.IsType<DialogueSession>(runtime.Talk("mara"));
        Assert.Equal(new DialogueLine("mara", "You came back."), session.CurrentLine);
        Assert.Empty(session.Choices);
        session.Continue();
        Assert.Equal(["Ask about the mine", "Leave"], session.Choices.Select(x => x.Text));

        session.Choose(0);
        Assert.Equal("It collapsed.", session.CurrentLine?.Text);
        session.Continue();

        var leave = Assert.Single(session.Choices);
        Assert.Equal(new DialogueChoiceView(1, "Leave", true), leave);
        Assert.Equal(2, runtime.State.Visits["mara.hub"]);
        Assert.Equal(1, runtime.State.Visits["mara"]);
        session.Choose(1);
        Assert.True(session.IsEnded);
        Assert.Null(session.CurrentLine);
        Assert.Throws<InvalidOperationException>(session.Continue);
    }

    [Fact]
    public void DisabledChoiceShownButNotChoosable()
    {
        var dialogue = Dialogue("mara", nodes:
        [
            new ChoicesNode
            {
                Id = "hub",
                Choices =
                {
                    new DialogueChoice { Text = "Bribe", When = C("day > 3"), HideWhenFalse = false, TargetId = "end" },
                    new DialogueChoice { Text = "Leave", TargetId = "end" },
                },
            },
            new EndNode { Id = "end" },
        ]);
        var runtime = Started(Story(new ActDefinition { Id = "act_1", Scenarios = { new ScenarioDefinition { Id = "s", Dialogues = { dialogue } } } }));
        var session = Assert.IsType<DialogueSession>(runtime.Talk("mara"));

        Assert.Equal([new DialogueChoiceView(0, "Bribe", false), new DialogueChoiceView(1, "Leave", true)], session.Choices);
        Assert.Throws<InvalidOperationException>(() => session.Choose(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => session.Choose(7));
        Assert.Throws<InvalidOperationException>(session.Continue);
        Assert.False(session.IsEnded);
    }

    [Fact]
    public void AllChoicesHiddenEndsDialogue()
    {
        var dialogue = Dialogue("mara", nodes:
        [
            new LineNode { Id = "hello", SpeakerId = "mara", Text = "Well?", NextId = "hub" },
            new ChoicesNode { Id = "hub", Choices = { new DialogueChoice { Text = "Bribe", When = C("day > 3"), TargetId = "hello" } } },
        ]);
        var logger = new LoggerResult();
        var runtime = new SagaRuntime(Story(new ActDefinition { Id = "act_1", Scenarios = { new ScenarioDefinition { Id = "s", Dialogues = { dialogue } } } }), logger: logger);
        runtime.Start();
        var session = Assert.IsType<DialogueSession>(runtime.Talk("mara"));

        session.Continue();

        Assert.True(session.IsEnded);
        var warning = Assert.Single(logger.Messages);
        Assert.Equal(LogMessageType.Warning, warning.Type);
        Assert.Contains("hub", warning.Text);
    }

    [Fact]
    public void AllChoicesDisabledEndsDialogue()
    {
        var dialogue = Dialogue("mara", nodes:
        [
            new LineNode { Id = "hello", SpeakerId = "mara", Text = "Well?", NextId = "hub" },
            new ChoicesNode { Id = "hub", Choices = { new DialogueChoice { Text = "Bribe", When = C("day > 3"), HideWhenFalse = false, TargetId = "hello" } } },
        ]);
        var logger = new LoggerResult();
        var runtime = new SagaRuntime(Story(new ActDefinition { Id = "act_1", Scenarios = { new ScenarioDefinition { Id = "s", Dialogues = { dialogue } } } }), logger: logger);
        runtime.Start();
        var session = Assert.IsType<DialogueSession>(runtime.Talk("mara"));

        session.Continue();

        // Nothing could be chosen: the dialogue would wait forever
        Assert.True(session.IsEnded);
        Assert.Empty(session.Choices);
        var warning = Assert.Single(logger.Messages);
        Assert.Equal(LogMessageType.Warning, warning.Type);
        Assert.Contains("hub", warning.Text);
    }

    [Fact]
    public void BranchTakesFirstTrueLink()
    {
        var dialogue = Dialogue("mara", nodes:
        [
            new BranchNode
            {
                Id = "check",
                Links = { new DialogueLink { When = C("day > 3"), TargetId = "late" }, new DialogueLink { When = C(""), TargetId = "early" } },
            },
            new LineNode { Id = "late", SpeakerId = "mara", Text = "You're late." },
            new LineNode { Id = "early", SpeakerId = "mara", Text = "You're early." },
        ]);
        var runtime = Started(Story(new ActDefinition { Id = "act_1", Scenarios = { new ScenarioDefinition { Id = "s", Dialogues = { dialogue } } } }));

        Assert.Equal("You're early.", runtime.Talk("mara")?.CurrentLine?.Text);
        runtime.Set("day", SagaValue.Int(4));
        var session = runtime.Talk("mara")!;
        Assert.Equal("You're late.", session.CurrentLine?.Text);

        session.Continue();
        Assert.True(session.IsEnded);
    }

    [Fact]
    public void ChoiceActionAdvancesQuest()
    {
        var dialogue = Dialogue("mara", nodes:
        [
            new ChoicesNode { Id = "ask", Choices = { new DialogueChoice { Text = "Help", Actions = A("quest.rescue.state = active; day = day + 1"), TargetId = "thanks" } } },
            new LineNode { Id = "thanks", Name = "thanks", SpeakerId = "mara", Text = "Thank you.", Actions = A("raise offer") },
        ]);
        var story = Story(new ActDefinition
        {
            Id = "act_1",
            Scenarios = { new ScenarioDefinition { Id = "s", Dialogues = { dialogue } }, new ScenarioDefinition { Id = "thanked", StartWhen = C("visited(\"mara.thanks\") && raised(\"offer\")") } },
            Quests = { new QuestDefinition { Id = "rescue" } },
        });
        var runtime = Started(story);
        var changes = new List<SagaChangedEventArgs>();
        runtime.Changed += (_, e) => changes.Add(e);

        runtime.Talk("mara")!.Choose(0);

        Assert.Equal(QuestState.Active, runtime.State.Quests["rescue"].State);
        Assert.Equal(SagaValue.Int(2), runtime.Get("day"));
        Assert.Equal(ScenarioState.Running, runtime.State.Scenarios["thanked"].State);
        Assert.Equal([new SagaChangedEventArgs(SagaChangeKind.QuestActivated, "rescue"), new SagaChangedEventArgs(SagaChangeKind.ScenarioStarted, "thanked")], changes);
    }

    [Fact]
    public void TextSubstitutesVariables()
    {
        var dialogue = Dialogue("mara", nodes:
        [
            new LineNode { Id = "hello", SpeakerId = "mara", Text = "Welcome, {player_name}. Day {day}, {unknown}, {quest.rescue.state}." },
        ]);
        var runtime = Started(Story(new ActDefinition { Id = "act_1", Scenarios = { new ScenarioDefinition { Id = "s", Dialogues = { dialogue } } }, Quests = { new QuestDefinition { Id = "rescue" } } }));

        Assert.Equal("Welcome, Ash. Day 1, {unknown}, {quest.rescue.state}.", runtime.Talk("mara")?.CurrentLine?.Text);
    }

    private static StoryDefinition Story(ActDefinition act) => new StoryBuilder()
        .Variable("day", SagaTypeKind.Int, "1")
        .Variable("player_name", SagaTypeKind.String, "\"Ash\"")
        .Events("offer")
        .Character("mara")
        .Character("bram")
        .Act(act)
        .Build();

    private static SagaRuntime Started(StoryDefinition story)
    {
        var runtime = new SagaRuntime(story);
        runtime.Start();
        return runtime;
    }

    private static DialogueDefinition Dialogue(string name, int priority = 0, string when = "", string character = "mara", bool ownerGate = true, DialogueNode[]? nodes = null)
    {
        nodes ??= [new LineNode { Id = "line", SpeakerId = character, Text = name }];
        return new DialogueDefinition
        {
            Name = name,
            CharacterId = character,
            Priority = priority,
            When = C(when),
            OnlyWhileOwnerLive = ownerGate,
            Participants = { "player", character },
            EntryNodeId = nodes[0].Id,
            Nodes = [.. nodes],
        };
    }
}
