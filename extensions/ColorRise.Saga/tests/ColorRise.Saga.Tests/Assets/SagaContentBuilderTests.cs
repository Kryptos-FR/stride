using ColorRise.Saga.Assets;
using ColorRise.Saga.Content;
using ColorRise.Saga.Expressions;
using Stride.Core.Assets;
using Stride.Core.Serialization;
using Xunit;

namespace ColorRise.Saga.Tests.Assets;

public sealed class SagaContentBuilderTests
{
    [Fact]
    public void BuildsPlotWithParsedConditions()
    {
        var diagnostics = new List<SagaBuildDiagnostic>();

        var story = SagaContentBuilder.BuildStory(Story(), "Story/Main", diagnostics);

        Assert.Empty(diagnostics);
        Assert.Equal("Main", story.Name);
        Assert.Equal("act_1", story.Plot.FirstActId);
        Assert.Equal(["act_1", "act_2"], story.Plot.Acts.Select(x => x.Id));
        var act = story.Plot.Acts[0];
        Assert.IsType<BinaryExpression>(act.EndWhen.Tree);
        Assert.Equal("day >= 3", act.EndWhen.Source);
        Assert.Equal("act_2", Assert.Single(act.Next).TargetActId);
        var scenario = Assert.Single(story.Plot.Acts[1].Scenarios);
        Assert.Equal("overlord", scenario.Id);
        Assert.True(scenario.ContinuesAcrossActs);
        Assert.Equal(SagaValue.Symbol("succeeded"), Assert.IsType<LiteralExpression>(Assert.IsType<BinaryExpression>(scenario.StartWhen.Tree).Right).Value);
        Assert.Equal("harder", Assert.Single(scenario.Outcomes).Name);
        Assert.IsType<AssignStatement>(Assert.Single(scenario.OnStart.Statements));
        var quest = Assert.Single(story.Plot.Acts[0].Quests);
        Assert.Equal("dig", Assert.Single(quest.Stages).Id);
        Assert.Null(quest.SucceedWhen.Tree);
        Assert.Equal("2", Assert.Single(quest.Attributes).Value);
    }

    [Fact]
    public void BrokenConditionNamesAssetCardAndPosition()
    {
        var asset = Story();
        asset.Acts[1].Scenarios[0].StartWhen = "quest.mine.state == succeeded &&";
        var diagnostics = new List<SagaBuildDiagnostic>();

        SagaContentBuilder.BuildStory(asset, "Story/Main", diagnostics);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("Story/Main", diagnostic.Asset);
        Assert.Equal("act act_2 / scenario overlord / StartWhen", diagnostic.Card);
        Assert.Equal(32, diagnostic.Position);
        Assert.Contains("expression", diagnostic.Message);
        Assert.Contains("Story/Main", diagnostic.ToString());
    }

    [Fact]
    public void TypeErrorsAreReported()
    {
        var asset = Story();
        asset.Acts[0].Quests[0].Stages[0].OnDone = "day = \"late\"";
        asset.Acts[0].Next[0].TargetActId = "act_9";
        asset.Acts[0].Quests[0].Attributes[0].Value = "many";
        var diagnostics = new List<SagaBuildDiagnostic>();

        SagaContentBuilder.BuildStory(asset, "Story/Main", diagnostics);

        // With its link moved away, nothing leads to act_2 any more: a warning besides the errors
        Assert.Equal(3, diagnostics.Count(x => x.Severity == SagaDiagnosticSeverity.Error));
        Assert.Contains(diagnostics, x => x.Card == "act act_2" && x.Severity == SagaDiagnosticSeverity.Warning);
        Assert.Contains(diagnostics, x => x.Card == "act act_1 / quest mine / stage dig / OnDone" && x.Position == 6);
        Assert.Contains(diagnostics, x => x.Card == "act act_1 / link to act_9" && x.Message.Contains("act_9"));
        Assert.Contains(diagnostics, x => x.Card == "act act_1 / quest mine / attribute reward" && x.Message.Contains("many"));
    }

    [Fact]
    public void DuplicateIdsAreReported()
    {
        var asset = Story();
        asset.Acts[1].Quests.Add(new QuestCard { Id = "mine" });
        asset.Acts[1].Scenarios.Add(new ScenarioCard { Id = "bad id" });
        asset.Declarations.Variables.Add(new VariableDeclaration { Name = "day", Type = SagaTypeKind.Int });
        var diagnostics = new List<SagaBuildDiagnostic>();

        SagaContentBuilder.BuildStory(asset, "Story/Main", diagnostics);

        Assert.Equal(3, diagnostics.Count);
        Assert.Contains(diagnostics, x => x.Card == "act act_2 / quest mine" && x.Message.Contains("mine"));
        Assert.Contains(diagnostics, x => x.Card == "act act_2 / scenario bad id" && x.Message.Contains("bad id"));
        Assert.Contains(diagnostics, x => x.Card == "declarations / variable day");
    }

    [Fact]
    public void UnknownCharacterInDialogueIsReported()
    {
        var dialogue = Dialogue();
        dialogue.CharacterId = "ghost";
        ((LineNodeCard)dialogue.Nodes[0]).SpeakerId = "phantom";
        var diagnostics = new List<SagaBuildDiagnostic>();

        SagaContentBuilder.BuildDialogue(dialogue, "Dialogues/mara_intro", Story(), diagnostics);

        Assert.Equal(2, diagnostics.Count);
        Assert.Contains(diagnostics, x => x.Card == "CharacterId" && x.Message.Contains("ghost"));
        Assert.Contains(diagnostics, x => x.Card == "node hello / SpeakerId" && x.Message.Contains("phantom"));
    }

    [Fact]
    public void DialogueChecksAgainstItsStory()
    {
        var diagnostics = new List<SagaBuildDiagnostic>();

        var built = SagaContentBuilder.BuildDialogue(Dialogue(), "Dialogues/mara_intro", Story(), diagnostics);

        Assert.Empty(diagnostics);
        Assert.Equal("mara_intro", built.Name);
        Assert.Equal("mara", built.CharacterId);
        Assert.Equal("hello", built.EntryNodeId);
        var choices = Assert.IsType<ChoicesNode>(built.Nodes[1]);
        Assert.Equal(SagaValue.Symbol("available"), Assert.IsType<LiteralExpression>(Assert.IsType<BinaryExpression>(choices.Choices[0].When.Tree).Right).Value);
        Assert.IsType<AssignStatement>(Assert.Single(choices.Choices[0].Actions.Statements));
        Assert.IsType<EndNode>(built.Nodes[2]);

        var broken = Dialogue();
        ((ChoicesNodeCard)broken.Nodes[1]).Choices[0].When = "quest.nothing.state == available";
        ((ChoicesNodeCard)broken.Nodes[1]).Choices[1].TargetId = "nowhere";
        diagnostics.Clear();
        SagaContentBuilder.BuildDialogue(broken, "Dialogues/mara_intro", Story(), diagnostics);
        Assert.Equal(2, diagnostics.Count);
        Assert.Contains(diagnostics, x => x.Card == "node offer / choice 1 / When" && x.Message.Contains("quest.nothing.state"));
        Assert.Contains(diagnostics, x => x.Card == "node offer / choice 2 / TargetId" && x.Message.Contains("nowhere"));
    }

    [Fact]
    public void NewStoryBuildsWithoutErrors()
    {
        var diagnostics = new List<SagaBuildDiagnostic>();

        var story = SagaContentBuilder.BuildStory(new StoryAssetFactory().New(), "Story", diagnostics);

        Assert.Empty(diagnostics);
        Assert.Equal("act_1", Assert.Single(story.Plot.Acts).Id);
    }

    [Fact]
    public void PlotLoopIsReportedOnTheLinkThatClosesIt()
    {
        var asset = Story();
        asset.Acts.Add(new ActCard { Id = "act_3", Next = { new ActLinkCard { TargetActId = "act_1" } } });
        asset.Acts[1].Next.Add(new ActLinkCard { TargetActId = "act_3" });
        var diagnostics = new List<SagaBuildDiagnostic>();

        SagaContentBuilder.BuildStory(asset, "Story/Main", diagnostics);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("act act_3 / link to act_1", diagnostic.Card);
        Assert.Contains("act_1 → act_2 → act_3 → act_1", diagnostic.Message);
    }

    [Fact]
    public void ActLinkingBackToItselfIsALoop()
    {
        var asset = Story();
        asset.Acts[1].Next.Add(new ActLinkCard { TargetActId = "act_2" });
        var diagnostics = new List<SagaBuildDiagnostic>();

        SagaContentBuilder.BuildStory(asset, "Story/Main", diagnostics);

        Assert.Equal("act act_2 / link to act_2", Assert.Single(diagnostics).Card);
    }

    [Fact]
    public void SecondLinkToTheSameActIsReported()
    {
        var asset = Story();
        asset.Acts[0].Next.Add(new ActLinkCard { TargetActId = "act_2", When = "day > 5" });
        var diagnostics = new List<SagaBuildDiagnostic>();

        SagaContentBuilder.BuildStory(asset, "Story/Main", diagnostics);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("act act_1 / link to act_2", diagnostic.Card);
        Assert.Contains("already", diagnostic.Message);
    }

    [Fact]
    public void DialoguesSharingANameAreReported()
    {
        // The name keys the visits and the once of a dialogue
        var asset = Story();
        var quest = asset.Acts[0].Quests[0];
        var scenario = asset.Acts[1].Scenarios[0];
        quest.Dialogues.Add(AttachedReferenceManager.CreateProxyObject<DialogueDefinition>(AssetId.New(), "Act1/Intro"));
        scenario.Dialogues.Add(AttachedReferenceManager.CreateProxyObject<DialogueDefinition>(AssetId.New(), "Act2/Intro"));
        // The same dialogue twice is not a clash
        scenario.Dialogues.Add(quest.Dialogues[0]);
        var diagnostics = new List<SagaBuildDiagnostic>();

        SagaContentBuilder.BuildStory(asset, "Story/Main", diagnostics);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("act act_2 / scenario overlord / Dialogues", diagnostic.Card);
        Assert.Contains("'Act1/Intro'", diagnostic.Message);
        Assert.Contains("'Act2/Intro'", diagnostic.Message);
    }

    private static StoryAsset Story() => new()
    {
        Declarations = new StoryDeclarations
        {
            Variables = { new VariableDeclaration { Name = "day", Type = SagaTypeKind.Int, Default = "1" } },
            Events = { new EventDeclaration { Name = "dawn" } },
            Characters = { new CharacterDeclaration { Id = "mara", DisplayName = "Mara" } },
            AttributeTypes = { new AttributeTypeDeclaration { Name = "reward", Type = SagaTypeKind.Int, AppliesTo = SagaCardKinds.Quest } },
        },
        FirstActId = "act_1",
        Acts =
        {
            new ActCard
            {
                Id = "act_1",
                EndWhen = "day >= 3",
                Next = { new ActLinkCard { TargetActId = "act_2" } },
                Quests =
                {
                    new QuestCard
                    {
                        Id = "mine",
                        OfferWhen = "raised(\"dawn\")",
                        Stages = { new QuestStageCard { Id = "dig", DoneWhen = "day > 2", OnDone = "day = day + 1" } },
                        Attributes = { new AttributeValue { Name = "reward", Value = "2" } },
                    },
                },
            },
            new ActCard
            {
                Id = "act_2",
                Scenarios =
                {
                    new ScenarioCard
                    {
                        Id = "overlord",
                        StartWhen = "quest.mine.state == succeeded",
                        ContinuesAcrossActs = true,
                        OnStart = "day = 10",
                        Outcomes = { new ScenarioOutcomeCard { Name = "harder", When = "day > 12" } },
                    },
                },
            },
        },
    };

    private static DialogueAsset Dialogue() => new()
    {
        CharacterId = "mara",
        When = "day > 0",
        Participants = { "player", "mara" },
        EntryNodeId = "hello",
        Nodes =
        {
            new LineNodeCard { Id = "hello", SpeakerId = "mara", Text = "Day {day}.", NextId = "offer" },
            new ChoicesNodeCard
            {
                Id = "offer",
                Name = "offer",
                Choices =
                {
                    new DialogueChoiceCard { Text = "Yes", When = "quest.mine.state == available", Actions = "quest.mine.state = active", TargetId = "end" },
                    new DialogueChoiceCard { Text = "No", TargetId = "end" },
                },
            },
            new EndNodeCard { Id = "end" },
        },
    };
}
