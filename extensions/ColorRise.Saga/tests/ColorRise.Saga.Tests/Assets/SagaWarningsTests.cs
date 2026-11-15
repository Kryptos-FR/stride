using ColorRise.Saga.Assets;
using ColorRise.Saga.Content;
using ColorRise.Saga.Expressions;
using Stride.Core.Diagnostics;
using Xunit;

namespace ColorRise.Saga.Tests.Assets;

/// <summary>
/// The warnings of the builder: what builds and plays but is likely a mistake.
/// </summary>
public sealed class SagaWarningsTests
{
    [Fact]
    public void UnreachableActIsAWarning()
    {
        var story = new StoryAsset
        {
            FirstActId = "a",
            Acts = { new ActCard { Id = "a", Next = { new ActLinkCard { TargetActId = "b" } } }, new ActCard { Id = "b" }, new ActCard { Id = "c" } },
        };
        var diagnostics = new List<SagaBuildDiagnostic>();

        SagaContentBuilder.BuildStory(story, "Story/Main", diagnostics);

        var warning = Assert.Single(diagnostics);
        Assert.Equal(("act c", SagaDiagnosticSeverity.Warning), (warning.Card, warning.Severity));
        Assert.Equal("No act leads here from the first act: the story never plays it.", warning.Message);
    }

    [Fact]
    public void UnreachableDialogueNodeIsAWarning()
    {
        var dialogue = Dialogue(
            new LineNodeCard { Id = "hello", SpeakerId = "mara", Text = "Hello.", NextId = "bye" },
            new LineNodeCard { Id = "orphan", SpeakerId = "mara", Text = "Nobody hears this.", NextId = "bye" },
            new EndNodeCard { Id = "bye" });
        var diagnostics = new List<SagaBuildDiagnostic>();

        SagaContentBuilder.BuildDialogue(dialogue, "Dialogues/Mara", Story(), diagnostics);

        var warning = Assert.Single(diagnostics);
        Assert.Equal(("node orphan", SagaDiagnosticSeverity.Warning), (warning.Card, warning.Severity));
        Assert.Equal("Nothing leads to this node from the entry: the dialogue never shows it.", warning.Message);

        // Without an entry, that's an error, and no node is said to be unreachable
        dialogue.EntryNodeId = "missing";
        diagnostics.Clear();
        SagaContentBuilder.BuildDialogue(dialogue, "Dialogues/Mara", Story(), diagnostics);
        Assert.Equal([SagaDiagnosticSeverity.Error], diagnostics.Select(x => x.Severity));
    }

    [Fact]
    public void BranchWithoutFallbackIsAWarning()
    {
        var branch = new BranchNodeCard
        {
            Id = "check",
            Links = { new DialogueLinkCard { When = "day > 1", TargetId = "bye" }, new DialogueLinkCard { When = "day > 5", TargetId = "bye" } },
        };
        var dialogue = Dialogue(branch, new EndNodeCard { Id = "bye" });
        var diagnostics = new List<SagaBuildDiagnostic>();

        SagaContentBuilder.BuildDialogue(dialogue, "Dialogues/Mara", Story(), diagnostics);

        var warning = Assert.Single(diagnostics);
        Assert.Equal(("node check", SagaDiagnosticSeverity.Warning), (warning.Card, warning.Severity));
        Assert.Equal("Every link has a condition: when none holds, the dialogue ends here. Leave the last link's condition empty to catch the rest.", warning.Message);

        branch.Links[1].When = string.Empty;
        diagnostics.Clear();
        SagaContentBuilder.BuildDialogue(dialogue, "Dialogues/Mara", Story(), diagnostics);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void ChoicesThatCanRunOutAreAWarning()
    {
        var choices = new ChoicesNodeCard
        {
            Id = "hub",
            Choices = { new DialogueChoiceCard { Text = "Ask", When = "day > 1", TargetId = "bye" }, new DialogueChoiceCard { Text = "Again", Once = true, TargetId = "bye" } },
        };
        var dialogue = Dialogue(choices, new EndNodeCard { Id = "bye" });
        var diagnostics = new List<SagaBuildDiagnostic>();

        SagaContentBuilder.BuildDialogue(dialogue, "Dialogues/Mara", Story(), diagnostics);

        var warning = Assert.Single(diagnostics);
        Assert.Equal(("node hub", SagaDiagnosticSeverity.Warning), (warning.Card, warning.Severity));
        Assert.Equal("Every choice has a condition or can be taken once: when none is left, the dialogue ends here.", warning.Message);

        choices.Choices.Add(new DialogueChoiceCard { Text = "Leave", TargetId = "bye" });
        diagnostics.Clear();
        SagaContentBuilder.BuildDialogue(dialogue, "Dialogues/Mara", Story(), diagnostics);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void CompilationLogsWarningsWithoutFailing()
    {
        var warning = new SagaBuildDiagnostic("Story/Main", "act c", 0, "Unreachable.", SagaDiagnosticSeverity.Warning);
        var error = new SagaBuildDiagnostic("Story/Main", "act d", 0, "Broken.");
        var logger = new LoggerResult();

        Assert.False(SagaCompilation.Report([warning], logger));
        Assert.Equal([(LogMessageType.Warning, warning.ToString())], logger.Messages.Select(x => (x.Type, x.Text)));

        logger.Clear();
        Assert.True(SagaCompilation.Report([warning, error], logger));
        Assert.Equal([LogMessageType.Warning, LogMessageType.Error], logger.Messages.Select(x => x.Type));
    }

    [Fact]
    public void VisitedKeyThatNamesNothingIsAWarning()
    {
        var (story, dialogue) = StoryWithDialogue();
        story.Acts[0].Scenarios[0].StartWhen = "visited(\"Mara.gretting\")";
        story.Acts[0].Scenarios[0].EndWhen = "visited(\"Mara\") && visits(\"Mara.greeting\") > 1";
        var diagnostics = new List<SagaBuildDiagnostic>();

        SagaContentBuilder.CheckStory(story, "Story/Main", [(dialogue, "Dialogues/Mara")], diagnostics);

        var warning = Assert.Single(diagnostics, x => x.Message.Contains("gretting"));
        Assert.Equal(("Story/Main", "act a / scenario s / StartWhen", 8, SagaDiagnosticSeverity.Warning), (warning.Asset, warning.Card, warning.Position, warning.Severity));
        Assert.Equal("'Mara.gretting' names no dialogue of the story, nor a named node of one (dialogue.node): visited(\"Mara.gretting\") never counts anything.", warning.Message);
        Assert.DoesNotContain(diagnostics, x => x.Card.Contains("EndWhen"));
    }

    [Fact]
    public void UnusedDeclarationsAreWarnings()
    {
        var (story, dialogue) = StoryWithDialogue();
        var declarations = story.Declarations;
        declarations.Variables.Add(new VariableDeclaration { Name = "unused", Type = SagaTypeKind.Bool });
        declarations.Events.Add(new EventDeclaration { Name = "never_checked" });
        declarations.Functions.Add(new FunctionDeclaration { Name = "uncalled", ReturnType = SagaTypeKind.Bool });
        declarations.Signals.Add(new SignalDeclaration { Name = "never_emitted" });
        declarations.Characters.Add(new CharacterDeclaration { Id = "ghost" });
        var diagnostics = new List<SagaBuildDiagnostic>();

        SagaContentBuilder.CheckStory(story, "Story/Main", [(dialogue, "Dialogues/Mara")], diagnostics);

        Assert.All(diagnostics, x => Assert.Equal(("Story/Main", SagaDiagnosticSeverity.Warning), (x.Asset, x.Severity)));
        Assert.Equal(
        [
            ("declarations / variable unused", "Nothing in the story or its dialogues reads, sets or shows this variable."),
            ("declarations / event never_checked", "No condition checks this event (raised(\"never_checked\")): raising it changes nothing."),
            ("declarations / function uncalled", "Nothing in the story or its dialogues calls this function."),
            ("declarations / signal never_emitted", "Nothing in the story or its dialogues emits this signal."),
            ("declarations / character ghost", "No dialogue belongs to this character, has it as a participant, or gives it a line."),
        ], diagnostics.Select(x => (x.Card, x.Message)));
    }

    [Fact]
    public void QuestNothingStartsIsAWarning()
    {
        var (story, dialogue) = StoryWithDialogue();
        story.Acts[0].Quests.Add(new QuestCard { Id = "lonely" });
        var diagnostics = new List<SagaBuildDiagnostic>();

        SagaContentBuilder.CheckStory(story, "Story/Main", [(dialogue, "Dialogues/Mara")], diagnostics);

        var warning = Assert.Single(diagnostics);
        Assert.Equal(("act a / quest lonely", SagaDiagnosticSeverity.Warning), (warning.Card, warning.Severity));
        Assert.Equal("The quest doesn't start by itself and no action sets quest.lonely.state = active: it never becomes active.", warning.Message);

        // A choice that takes it on, or a quest that starts by itself, is enough
        ((ChoicesNodeCard)dialogue.Nodes[1]).Choices[0].Actions = "quest.lonely.state = active";
        diagnostics.Clear();
        SagaContentBuilder.CheckStory(story, "Story/Main", [(dialogue, "Dialogues/Mara")], diagnostics);
        Assert.Contains(diagnostics, x => x.Card == "declarations / signal ring");
        Assert.DoesNotContain(diagnostics, x => x.Card.Contains("lonely"));
    }

    [Fact]
    public void NoCrossChecksOnABrokenStory()
    {
        var (story, dialogue) = StoryWithDialogue();
        story.Declarations.Events.Add(new EventDeclaration { Name = "never_checked" });
        story.Acts[0].Scenarios[0].StartWhen = "day >";
        var diagnostics = new List<SagaBuildDiagnostic>();

        SagaContentBuilder.CheckStory(story, "Story/Main", [(dialogue, "Dialogues/Mara")], diagnostics);

        Assert.Empty(diagnostics);
    }

    /// <summary>
    /// A story that uses everything it declares: a day shown in a line, an event that offers a quest, a function and a signal of a
    /// choice that takes the quest, Mara's dialogue in which Bram also speaks, a named greeting node.
    /// </summary>
    private static (StoryAsset Story, DialogueAsset Dialogue) StoryWithDialogue()
    {
        var dialogue = new DialogueAsset
        {
            CharacterId = "mara",
            Participants = { "player", "mara" },
            EntryNodeId = "hello",
            Nodes =
            {
                new LineNodeCard { Id = "hello", Name = "greeting", SpeakerId = "bram", Text = "Day {day}.", NextId = "offer" },
                new ChoicesNodeCard
                {
                    Id = "offer",
                    Choices =
                    {
                        new DialogueChoiceCard { Text = "Yes", When = "has_lamp()", Actions = "quest.mine.state = active; emit ring()", TargetId = "end" },
                        new DialogueChoiceCard { Text = "No", TargetId = "end" },
                    },
                },
                new EndNodeCard { Id = "end" },
            },
        };
        var story = new StoryAsset
        {
            Declarations =
            {
                Variables = { new VariableDeclaration { Name = "day", Type = SagaTypeKind.Int, Default = "1" } },
                Events = { new EventDeclaration { Name = "offer" } },
                Functions = { new FunctionDeclaration { Name = "has_lamp", ReturnType = SagaTypeKind.Bool } },
                Signals = { new SignalDeclaration { Name = "ring" } },
                Characters = { new CharacterDeclaration { Id = "mara" }, new CharacterDeclaration { Id = "bram" } },
            },
            FirstActId = "a",
            Acts =
            {
                new ActCard
                {
                    Id = "a",
                    Scenarios = { new ScenarioCard { Id = "s" } },
                    Quests = { new QuestCard { Id = "mine", ScenarioId = "s", OfferWhen = "raised(\"offer\")" } },
                },
            },
        };
        return (story, dialogue);
    }

    private static StoryAsset Story() => new()
    {
        Declarations =
        {
            Variables = { new VariableDeclaration { Name = "day", Type = SagaTypeKind.Int, Default = "1" } },
            Characters = { new CharacterDeclaration { Id = "mara", DisplayName = "Mara" } },
        },
        FirstActId = "a",
        Acts = { new ActCard { Id = "a" } },
    };

    private static DialogueAsset Dialogue(params DialogueNodeCard[] nodes)
    {
        var dialogue = new DialogueAsset { CharacterId = "mara", Participants = { "player", "mara" }, EntryNodeId = nodes[0].Id };
        dialogue.Nodes.AddRange(nodes);
        return dialogue;
    }
}
