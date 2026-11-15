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
