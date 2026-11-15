using ColorRise.Saga.Assets;
using ColorRise.Saga.Content;
using ColorRise.Saga.Editor.Player;
using ColorRise.Saga.Expressions;
using Stride.Core.Assets;
using Stride.Core.Serialization;

namespace ColorRise.Saga.Editor.Tests.Helpers;

/// <summary>
/// A small story to play without a session: a day counter, an event, a function, a signal, Mara, a quest to find a lamp, and
/// Mara's dialogue: a line, then a choice to ask about the mine (with a pickaxe) or to leave, which rings and offers the quest.
/// </summary>
public static class MaraStory
{
    public const string StoryUrl = "Story/Main";

    public const string DialogueUrl = "Dialogues/Mara";

    public static (StoryAsset Story, DialogueAsset Dialogue) Create()
    {
        var dialogue = new DialogueAsset
        {
            Id = AssetId.New(),
            CharacterId = "mara",
            EntryNodeId = "hello",
            Participants = { "player", "mara" },
            Nodes =
            {
                new LineNodeCard { Id = "hello", SpeakerId = "mara", Text = "You came back.", NextId = "hub" },
                new ChoicesNodeCard
                {
                    Id = "hub",
                    Choices =
                    {
                        new DialogueChoiceCard { Text = "Ask about the mine", When = "has_pickaxe()", TargetId = "end" },
                        new DialogueChoiceCard { Text = "Leave", Actions = "lamp_asked = true; emit ring()", TargetId = "end" },
                    },
                },
                new EndNodeCard { Id = "end" },
            },
        };
        var story = new StoryAsset
        {
            FirstActId = "the_mine",
            Declarations =
            {
                Variables = { new VariableDeclaration { Name = "day", Type = SagaTypeKind.Int, Default = "1" }, new VariableDeclaration { Name = "lamp_asked", Type = SagaTypeKind.Bool } },
                Events = { new EventDeclaration { Name = "met_mara" } },
                Functions = { new FunctionDeclaration { Name = "has_pickaxe", ReturnType = SagaTypeKind.Bool } },
                Signals = { new SignalDeclaration { Name = "ring" } },
                Characters = { new CharacterDeclaration { Id = "mara", DisplayName = "Mara" } },
            },
            Acts =
            {
                new ActCard
                {
                    Id = "the_mine",
                    Scenarios = { new ScenarioCard { Id = "cave_in", Dialogues = { AttachedReferenceManager.CreateProxyObject<DialogueDefinition>(dialogue.Id, DialogueUrl) } } },
                    Quests =
                    {
                        new QuestCard
                        {
                            Id = "find_the_lamp",
                            OfferWhen = "lamp_asked",
                            Stages = { new QuestStageCard { Id = "meet", DoneWhen = "raised(\"met_mara\")" }, new QuestStageCard { Id = "dawn", DoneWhen = "day > 1" } },
                        },
                    },
                },
            },
        };
        return (story, dialogue);
    }

    /// <summary>
    /// Builds a story made by <see cref="Create"/> as the player does, failing on errors.
    /// </summary>
    public static StoryDefinition Build(StoryAsset story, DialogueAsset dialogue)
    {
        var diagnostics = new List<SagaBuildDiagnostic>();
        var playback = SagaPlayback.Create(story, StoryUrl, x => AttachedReferenceManager.GetAttachedReference(x)?.Url == DialogueUrl ? (dialogue, DialogueUrl) : null, diagnostics);
        Assert.True(playback is not null, string.Join(Environment.NewLine, diagnostics));
        return playback.Runtime.Story;
    }

    public static StoryDefinition Build()
    {
        var (story, dialogue) = Create();
        return Build(story, dialogue);
    }
}
