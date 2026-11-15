using ColorRise.Saga.Assets;
using ColorRise.Saga.Content;
using ColorRise.Saga.Editor.Player;
using ColorRise.Saga.Editor.Tests.Helpers;
using ColorRise.Saga.Expressions;
using Stride.Core.Assets;
using Stride.Core.Assets.Editor.Panels;
using Stride.Core.Serialization;

namespace ColorRise.Saga.Editor.Tests.Player;

/// <summary>
/// The story player panel, on the assets of a session: picking a story, its problems, starting it, talking, choosing, and the
/// story's API by hand (variables, events, functions).
/// </summary>
public sealed class StoryPlayerTests
{
    [Fact]
    public Task PlayerTalksToACharacterAndTakesChoices() => RunAsync((_, player) =>
    {
        Assert.Equal(["Main"], player.Stories.Select(x => x.Name));
        player.SelectedStory = player.Stories[0];
        Assert.Empty(Errors(player));
        Assert.True(player.StartCommand.CanExecute(null));

        player.StartCommand.Execute(null);

        Assert.True(player.IsPlaying);
        Assert.False(player.StartCommand.CanExecute(null));
        Assert.Equal("the_mine", player.CurrentAct);
        Assert.Equal([("cave_in", "running", "")], player.Scenarios.Select(x => (x.Id, x.State, x.Detail)));
        Assert.Equal([("find_the_lamp", "not_started", "")], player.Quests.Select(x => (x.Id, x.State, x.Detail)));
        var mara = Assert.Single(player.Characters);
        Assert.Equal(("mara", "Mara"), (mara.Id, mara.DisplayName));

        mara.TalkCommand.Execute(null);
        Assert.Equal(["Talking to Mara", "Mara: You came back."], player.Transcript);
        Assert.True(player.ContinueCommand.CanExecute(null));
        Assert.Empty(player.Choices);

        player.ContinueCommand.Execute(null);
        // The choice that asks for a pickaxe is hidden until the function says so
        Assert.Equal(["Leave"], player.Choices.Select(x => x.Text));
        Assert.False(player.ContinueCommand.CanExecute(null));

        player.Choices[0].ChooseCommand.Execute(null);
        Assert.Equal(["Talking to Mara", "Mara: You came back.", "> Leave", "(the dialogue ends)"], player.Transcript);
        Assert.Empty(player.Choices);
        Assert.False(player.ContinueCommand.CanExecute(null));
        // Leaving made the quest available
        Assert.Equal("available", player.Quests[0].State);
        return Task.CompletedTask;
    });

    [Fact]
    public Task VariablesEventsAndFunctionsAreSetByHand() => RunAsync((_, player) =>
    {
        player.SelectedStory = player.Stories[0];
        player.StartCommand.Execute(null);

        Assert.Equal(["day", "lamp_asked"], player.Variables.Select(x => x.Name));
        var day = player.Variables[0];
        Assert.Equal(("day", "Int", "1"), (day.Name, day.Type, day.Value));
        day.Value = "dawn";
        Assert.NotEmpty(day.Error);
        day.Value = "4";
        Assert.Empty(day.Error);
        Assert.Equal(SagaValue.Int(4), player.Playback!.Runtime.Get("day"));

        var met = Assert.Single(player.Events);
        met.RaiseCommand.Execute(null);
        met.RaiseCommand.Execute(null);
        Assert.Equal(2, met.Count);

        var pickaxe = Assert.Single(player.Functions);
        Assert.Equal(("has_pickaxe", "has_pickaxe() → Bool", "false"), (pickaxe.Name, pickaxe.Signature, pickaxe.Answer));
        pickaxe.Answer = "maybe";
        Assert.NotEmpty(pickaxe.Error);
        pickaxe.Answer = "true";
        Assert.Empty(pickaxe.Error);
        player.Characters[0].TalkCommand.Execute(null);
        player.ContinueCommand.Execute(null);
        Assert.Equal(["Ask about the mine", "Leave"], player.Choices.Select(x => x.Text));

        player.ResetCommand.Execute(null);
        Assert.False(player.IsPlaying);
        Assert.Empty(player.Transcript);
        Assert.Empty(player.Characters);
        Assert.True(player.StartCommand.CanExecute(null));
        return Task.CompletedTask;
    });

    [Fact]
    public Task ABrokenStoryShowsItsProblemsAndDoesNotStart() => RunAsync((fixture, player) =>
    {
        var story = fixture.Asset<ViewModels.StoryViewModel>("Story/Main");
        story.Asset.Acts[0].Scenarios[0].StartWhen = "day >";

        player.SelectedStory = player.Stories[0];

        // As the compiler reports it, with the URL of the asset in its package
        Assert.Equal(["/SagaTest/Story/Main: act the_mine / scenario cave_in / StartWhen (at 5): Expected an expression, but the text ends here."], Errors(player));
        Assert.False(player.StartCommand.CanExecute(null));

        // Fixed, the story is checked again when it is reset
        story.Asset.Acts[0].Scenarios[0].StartWhen = string.Empty;
        player.ResetCommand.Execute(null);
        Assert.Empty(Errors(player));
        Assert.True(player.StartCommand.CanExecute(null));
        return Task.CompletedTask;
    });

    private static IEnumerable<string> Errors(StoryPlayerViewModel player) => player.Diagnostics.Where(x => x.Severity == SagaDiagnosticSeverity.Error).Select(x => x.ToString());

    /// <summary>
    /// Opens the story player on a session with a story (a day counter, an event, a function, Mara) and Mara's dialogue: a line,
    /// then a choice to ask about the mine (with a pickaxe) or to leave, which offers a quest.
    /// </summary>
    private static Task RunAsync(Func<SagaSessionFixture, StoryPlayerViewModel, Task> test)
    {
        var dialogue = new DialogueAsset
        {
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
                        new DialogueChoiceCard { Text = "Leave", Actions = "lamp_asked = true", TargetId = "end" },
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
                Characters = { new CharacterDeclaration { Id = "mara", DisplayName = "Mara" } },
            },
            Acts =
            {
                new ActCard
                {
                    Id = "the_mine",
                    Scenarios = { new ScenarioCard { Id = "cave_in", Dialogues = { AttachedReferenceManager.CreateProxyObject<DialogueDefinition>(dialogue.Id, "Dialogues/Mara") } } },
                    Quests = { new QuestCard { Id = "find_the_lamp", OfferWhen = "lamp_asked" } },
                },
            },
        };
        return SagaSessionFixture.RunAsync(async fixture =>
        {
            Assert.False(fixture.Result.HasErrors, fixture.Log);
            var context = new PanelContext { Descriptor = StoryPlayerViewModel.Panel, Session = fixture.Session, ServiceProvider = fixture.Session.ServiceProvider };
            // As the editor creates it, through the panel's descriptor
            var player = Assert.IsType<StoryPlayerViewModel>(StoryPlayerViewModel.Panel.CreateViewModel(context));
            try
            {
                await test(fixture, player);
            }
            finally
            {
                player.Destroy();
            }
        }, ("Story/Main", story), ("Dialogues/Mara", dialogue));
    }
}
