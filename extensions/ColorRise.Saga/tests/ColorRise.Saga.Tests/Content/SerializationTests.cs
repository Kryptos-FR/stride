using ColorRise.Saga.Content;
using ColorRise.Saga.Expressions;
using Stride.Core.Serialization;
using Xunit;

namespace ColorRise.Saga.Tests.Content;

public sealed class SerializationTests
{
    [Fact]
    public void StoryDefinitionRoundTrips()
    {
        var story = new StoryDefinition
        {
            Name = "Main",
            Declarations = new StoryDeclarations
            {
                Variables = { new VariableDeclaration { Name = "day", Type = SagaTypeKind.Int, Default = "1" } },
                Events = { new EventDeclaration { Name = "mine_collapsed" } },
                Functions = { new FunctionDeclaration { Name = "has_item", Parameters = { SagaTypeKind.String }, ReturnType = SagaTypeKind.Bool } },
                Signals = { new SignalDeclaration { Name = "give_item", Parameters = { SagaTypeKind.String, SagaTypeKind.Int } } },
                Characters = { new CharacterDeclaration { Id = "mara", DisplayName = "Mara" } },
                AttributeTypes = { new AttributeTypeDeclaration { Name = "deadline", Type = SagaTypeKind.Int, AppliesTo = SagaCardKinds.Quest } },
            },
            Plot = new PlotDefinition
            {
                FirstActId = "act_1",
                Acts =
                {
                    new ActDefinition
                    {
                        Id = "act_1",
                        Title = "The mine",
                        EndWhen = Condition("day >= 12 && raised(\"mine_collapsed\")"),
                        Next = { new ActLink { TargetActId = "act_2", When = Condition("") } },
                        Scenarios =
                        {
                            new ScenarioDefinition
                            {
                                Id = "collapse",
                                StartWhen = Condition("day > 3"),
                                OnStart = Actions("day = day + 1; raise mine_collapsed"),
                                Outcomes = { new ScenarioOutcome { Name = "harder", When = Condition("day > 5") } },
                                Dialogues = { Dialogue() },
                            },
                        },
                        Quests =
                        {
                            new QuestDefinition
                            {
                                Id = "rescue",
                                ScenarioId = "collapse",
                                StartsAutomatically = true,
                                Stages = { new QuestStage { Id = "dig", DoneWhen = Condition("day > 4"), OnDone = Actions("emit give_item(\"shovel\", 1)") } },
                                Attributes = { new AttributeValue { Name = "deadline", Value = "14" } },
                            },
                        },
                    },
                },
            },
        };

        var copy = SerializerExtensions.Clone(story);

        Assert.Equal("Main", copy.Name);
        Assert.Equal("give_item", copy.Declarations.Signals[0].Name);
        Assert.Equal([SagaTypeKind.String, SagaTypeKind.Int], copy.Declarations.Signals[0].Parameters);
        var act = Assert.Single(copy.Plot.Acts);
        Assert.Equal("day >= 12 && raised(\"mine_collapsed\")", act.EndWhen.Source);
        var and = Assert.IsType<BinaryExpression>(act.EndWhen.Tree);
        Assert.Equal(SagaBinaryOperator.And, and.Operator);
        Assert.Equal(SagaValue.Int(12), Assert.IsType<LiteralExpression>(Assert.IsType<BinaryExpression>(and.Left).Right).Value);
        Assert.Equal("mine_collapsed", Assert.IsType<RaiseStatement>(act.Scenarios[0].OnStart.Statements[1]).Event);
        Assert.Null(act.Next[0].When.Tree);
        Assert.Equal("collapse", act.Quests[0].ScenarioId);
        Assert.Equal("14", act.Quests[0].Attributes[0].Value);

        var dialogue = Assert.Single(act.Scenarios[0].Dialogues);
        Assert.Equal("mara", dialogue.CharacterId);
        var choices = Assert.IsType<ChoicesNode>(dialogue.Nodes[1]);
        Assert.Equal(2, choices.Choices.Count);
        Assert.Equal("Ask about the mine", choices.Choices[0].Text);
        Assert.False(choices.Choices[1].HideWhenFalse);
        Assert.Equal("end", choices.Choices[1].TargetId);
        Assert.IsType<EndNode>(dialogue.Nodes[2]);
    }

    [Fact]
    public void SagaStateRoundTrips()
    {
        var state = new SagaState
        {
            CurrentActId = "act_2",
            CompletedActs = { "act_1" },
            Variables = { ["day"] = SagaValue.Int(12), ["name"] = SagaValue.String("Mara"), ["speed"] = SagaValue.Float(2.5f) },
            Quests = { ["rescue"] = new QuestProgress { State = QuestState.Active, Stage = 1 } },
            Scenarios = { ["collapse"] = new ScenarioProgress { State = ScenarioState.Done, Outcome = "harder" } },
            Events = { ["mine_collapsed"] = 2 },
            Visits = { ["greeting"] = 3 },
            UsedOnce = { "mara_intro" },
        };

        var copy = SerializerExtensions.Clone(state);

        Assert.Equal("act_2", copy.CurrentActId);
        Assert.Equal(["act_1"], copy.CompletedActs);
        Assert.Equal(SagaValue.Int(12), copy.Variables["day"]);
        Assert.Equal(SagaValue.String("Mara"), copy.Variables["name"]);
        Assert.Equal(SagaValue.Float(2.5f), copy.Variables["speed"]);
        Assert.Equal(QuestState.Active, copy.Quests["rescue"].State);
        Assert.Equal(1, copy.Quests["rescue"].Stage);
        Assert.Equal(ScenarioState.Done, copy.Scenarios["collapse"].State);
        Assert.Equal("harder", copy.Scenarios["collapse"].Outcome);
        Assert.Equal(2, copy.Events["mine_collapsed"]);
        Assert.Equal(3, copy.Visits["greeting"]);
        Assert.Equal(["mara_intro"], copy.UsedOnce);
    }

    private static DialogueDefinition Dialogue() => new()
    {
        Name = "Mara at the mine",
        CharacterId = "mara",
        When = Condition("day > 2"),
        Priority = 5,
        Participants = { "player", "mara" },
        EntryNodeId = "hello",
        Nodes =
        {
            new LineNode { Id = "hello", Name = "greeting", SpeakerId = "mara", Text = "You came back.", NextId = "ask" },
            new ChoicesNode
            {
                Id = "ask",
                Choices =
                {
                    new DialogueChoice { Text = "Ask about the mine", When = Condition("day > 3"), Once = true, TargetId = "hello" },
                    new DialogueChoice { Text = "Leave", HideWhenFalse = false, Actions = Actions("raise mine_collapsed"), TargetId = "end" },
                },
            },
            new EndNode { Id = "end" },
        },
    };

    private static SagaCondition Condition(string source)
    {
        var parsed = SagaParser.ParseCondition(source);
        Assert.Empty(parsed.Diagnostics);
        return new SagaCondition { Source = source, Tree = parsed.Value };
    }

    private static SagaActions Actions(string source)
    {
        var parsed = SagaParser.ParseActions(source);
        Assert.Empty(parsed.Diagnostics);
        return new SagaActions { Source = source, Statements = parsed.Value };
    }
}
