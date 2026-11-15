using ColorRise.Saga.Assets;
using ColorRise.Saga.Content;
using ColorRise.Saga.Editor.Player;
using Stride.Core.Assets;
using Stride.Core.Serialization;

namespace ColorRise.Saga.Editor.Tests.Player;

/// <summary>
/// What the story player plays: a story built from its asset with the dialogues it references, or the problems that keep it from
/// starting. No session needed: the dialogue references are resolved by the caller.
/// </summary>
public sealed class SagaPlaybackTests
{
    private const string StoryUrl = "Story/Main";
    private const string DialogueUrl = "Dialogues/Mara";

    [Fact]
    public void PlaybackResolvesDialogueReferences()
    {
        var (story, dialogue) = CreateStory();
        var diagnostics = new List<SagaBuildDiagnostic>();

        var playback = SagaPlayback.Create(story, StoryUrl, x => Resolve(x, dialogue), diagnostics);

        Assert.Empty(diagnostics);
        Assert.NotNull(playback);
        Assert.Equal("Main", playback.Runtime.Story.Name);
        playback.Runtime.Start();
        var session = playback.Runtime.Talk("mara");
        Assert.NotNull(session);
        Assert.Equal("Mara", session.Dialogue.Name);
        Assert.Equal(new DialogueLine("mara", "You came back."), session.CurrentLine);
    }

    [Fact]
    public void PlaybackWithBrokenExpressionReturnsDiagnostics()
    {
        var (story, dialogue) = CreateStory();
        story.Acts[0].Scenarios[0].StartWhen = "day >";
        var diagnostics = new List<SagaBuildDiagnostic>();

        var playback = SagaPlayback.Create(story, StoryUrl, x => Resolve(x, dialogue), diagnostics);

        Assert.Null(playback);
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("Story/Main: act the_mine / scenario cave_in / StartWhen (at 5): Expected an expression, but the text ends here.", diagnostic.ToString());
    }

    [Fact]
    public void PlaybackChecksTheDialoguesAndReportsTheMissingOnes()
    {
        var (story, dialogue) = CreateStory();
        ((LineNodeCard)dialogue.Nodes[0]).SpeakerId = "stranger";
        var diagnostics = new List<SagaBuildDiagnostic>();

        Assert.Null(SagaPlayback.Create(story, StoryUrl, x => Resolve(x, dialogue), diagnostics));
        Assert.Contains(diagnostics, x => x.Asset == DialogueUrl && x.Message.Contains("stranger"));

        diagnostics.Clear();
        ((LineNodeCard)dialogue.Nodes[0]).SpeakerId = "mara";
        Assert.Null(SagaPlayback.Create(story, StoryUrl, _ => null, diagnostics));
        var missing = Assert.Single(diagnostics);
        Assert.Equal((StoryUrl, "act the_mine / scenario cave_in / Dialogues"), (missing.Asset, missing.Card));
        Assert.Contains(DialogueUrl, missing.Message);
    }

    [Fact]
    public void PlaybackStartsWithWarnings()
    {
        var (story, dialogue) = CreateStory();
        story.Acts.Add(new ActCard { Id = "nowhere" });
        var diagnostics = new List<SagaBuildDiagnostic>();

        var playback = SagaPlayback.Create(story, StoryUrl, x => Resolve(x, dialogue), diagnostics);

        Assert.NotNull(playback);
        var warning = Assert.Single(diagnostics);
        Assert.Equal(("act nowhere", SagaDiagnosticSeverity.Warning), (warning.Card, warning.Severity));
    }

    private static (DialogueAsset Asset, string Url)? Resolve(DialogueDefinition reference, DialogueAsset dialogue)
        => AttachedReferenceManager.GetAttachedReference(reference)?.Url == DialogueUrl ? (dialogue, DialogueUrl) : null;

    /// <summary>
    /// A story of one act whose scenario holds a dialogue of Mara's: one line, then the end.
    /// </summary>
    private static (StoryAsset Story, DialogueAsset Dialogue) CreateStory()
    {
        var dialogue = new DialogueAsset
        {
            Id = AssetId.New(),
            CharacterId = "mara",
            EntryNodeId = "hello",
            Participants = { "player", "mara" },
            Nodes =
            {
                new LineNodeCard { Id = "hello", SpeakerId = "mara", Text = "You came back.", NextId = "end" },
                new EndNodeCard { Id = "end" },
            },
        };
        var story = new StoryAsset
        {
            FirstActId = "the_mine",
            Declarations = { Characters = { new CharacterDeclaration { Id = "mara", DisplayName = "Mara" } } },
            Acts =
            {
                new ActCard
                {
                    Id = "the_mine",
                    Scenarios = { new ScenarioCard { Id = "cave_in", Dialogues = { AttachedReferenceManager.CreateProxyObject<DialogueDefinition>(dialogue.Id, DialogueUrl) } } },
                },
            },
        };
        return (story, dialogue);
    }
}
