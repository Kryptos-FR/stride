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
        // Warnings don't keep the story from starting: nothing checks the event, nothing makes the quest active
        Assert.Contains(player.Diagnostics, x => x.Card == "declarations / event met_mara" && x.Severity == SagaDiagnosticSeverity.Warning);
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
        Assert.Equal(SagaValue.Int(4), player.Service.Run!.Runtime.Get("day"));

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
    public Task RewindingShowsTheDialogueAtThatStep() => RunAsync((_, player) =>
    {
        player.SelectedStory = player.Stories[0];
        player.StartCommand.Execute(null);
        player.Characters[0].TalkCommand.Execute(null);
        player.ContinueCommand.Execute(null);
        player.Choices[0].ChooseCommand.Execute(null);
        Assert.Equal("available", player.Quests[0].State);

        player.Service.GoTo(2);

        Assert.Equal(["Leave"], player.Choices.Select(x => x.Text));
        Assert.Equal(["Talking to Mara", "Mara: You came back."], player.Transcript);
        Assert.Equal("not_started", player.Quests[0].State);
        Assert.Equal("false", player.Variables.Single(x => x.Name == "lamp_asked").Value);

        // Taking the choice again from there plays as before
        player.Choices[0].ChooseCommand.Execute(null);
        Assert.Equal("available", player.Quests[0].State);
        Assert.Equal(4, player.Service.Steps.Count);

        // The answers of the functions follow the cursor too
        var pickaxe = player.Functions[0];
        pickaxe.Answer = "true";
        Assert.Equal(5, player.Service.Steps.Count);
        player.Service.GoTo(3);
        Assert.Equal("false", pickaxe.Answer);
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

    [Fact]
    public Task PlayerShowsTheTimeline() => RunAsync((fixture, player) =>
    {
        var panels = new RecordingPanelService();
        var services = (Stride.Core.Presentation.ViewModels.ViewModelServiceProvider)fixture.Session.ServiceProvider;
        services.RegisterService(panels);
        try
        {
            player.ShowTimelineCommand.Execute(null);
            Assert.Equal([(StoryTimelineViewModel.PanelId, true)], panels.Shown);
        }
        finally
        {
            services.UnregisterService(panels);
        }
        return Task.CompletedTask;
    });

    [Fact]
    public Task ValuesSetByHandAreRecordedAsTheyAre()
    {
        var (story, dialogue) = MaraStory.Create();
        story.Declarations.Variables.Add(new VariableDeclaration { Name = "greeting", Type = SagaTypeKind.String });
        story.Declarations.Variables.Add(new VariableDeclaration { Name = "depth", Type = SagaTypeKind.Float });
        return SagaSessionFixture.RunAsync(fixture =>
        {
            var context = new PanelContext { Descriptor = StoryPlayerViewModel.Panel, Session = fixture.Session, ServiceProvider = fixture.Session.ServiceProvider };
            var player = (StoryPlayerViewModel)StoryPlayerViewModel.Panel.CreateViewModel(context);
            try
            {
                player.SelectedStory = player.Stories.Single();
                player.StartCommand.Execute(null);
                var greeting = player.Variables.Single(x => x.Name == "greeting");
                var depth = player.Variables.Single(x => x.Name == "depth");
                greeting.Value = "\"say \\\"hi\\\" in C:\\\\mine\"";
                depth.Value = "0.12345";

                Assert.Equal(("", ""), (greeting.Error, depth.Error));
                var runtime = player.Service.Run!.Runtime;
                Assert.Equal((SagaValue.String("say \"hi\" in C:\\mine"), SagaValue.Float(0.12345f)), (runtime.Get("greeting"), runtime.Get("depth")));
                Assert.Equal(("\"say \\\"hi\\\" in C:\\\\mine\"", "0.12345"), (greeting.Value, depth.Value));
            }
            finally
            {
                player.Destroy();
            }
            return Task.CompletedTask;
        }, (MaraStory.StoryUrl, story), (MaraStory.DialogueUrl, dialogue));
    }

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
