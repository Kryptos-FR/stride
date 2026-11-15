using ColorRise.Saga.Assets;
using ColorRise.Saga.Editor.Player;
using ColorRise.Saga.Editor.Tests.Helpers;
using ColorRise.Saga.Editor.ViewModels;
using Stride.Assets.Editor.Commands;
using Stride.Assets.Editor.ViewModels;
using Stride.Assets.Entities;
using Stride.Assets.Presentation.ViewModels;
using Stride.Core.Assets;
using Stride.Core.Assets.Editor.Commands;
using Stride.Core.Assets.Editor.Panels;
using Stride.Core.Assets.Editor.ViewModels;
using Stride.Core.Assets.Quantum;
using Stride.Engine;

namespace ColorRise.Saga.Editor.Tests;

/// <summary>
/// Saga's commands: what they are enabled on, and what they find in the session.
/// </summary>
public sealed class SagaCommandsTests
{
    [Fact]
    public Task PlayStoryOnlyForOneStoryAsset() => RunAsync(fixture =>
    {
        var playStory = SagaCommands.Create().Single(x => x.Id == SagaCommands.PlayStory);
        Assert.Equal(("Play story", "Saga", true), (playStory.Title(), playStory.Category, playStory.ShowInPalette));
        Assert.Contains(playStory.Placements, x => x.Location == CommandLocations.AssetViewContext);
        var story = fixture.Asset<StoryViewModel>("Story/Main");
        var dialogue = fixture.Asset<DialogueViewModel>("Dialogues/Mara");

        bool CanPlay(params object[] selection)
            => playStory.CanExecute!.Invoke(new CommandContext { Session = (SessionViewModel)fixture.Session, Location = CommandLocations.AssetViewContext, Selection = selection });

        Assert.True(CanPlay(story));
        Assert.False(CanPlay());
        Assert.False(CanPlay(dialogue));
        Assert.False(CanPlay(story, dialogue));
        return Task.CompletedTask;
    });

    [Fact]
    public Task OpenDialogueIsOnTheEntityTreeAndFindsTheCharactersDialoguesByPriority() => RunAsync(fixture =>
    {
        var openDialogue = SagaCommands.Create().Single(x => x.Id == SagaCommands.OpenDialogue);
        Assert.Equal(("Open dialogue", "Saga"), (openDialogue.Title(), openDialogue.Category));
        Assert.Contains(openDialogue.Placements, x => x.Location == SceneLocations.EntityTreeContext);
        Assert.False(openDialogue.CanExecute!.Invoke(new CommandContext { Session = (SessionViewModel)fixture.Session, Location = CommandLocations.Palette, Selection = [fixture.Asset<StoryViewModel>("Story/Main")] }));

        Assert.Equal(["Mara", "Mara_greeting"], SagaCommands.DialoguesOf(fixture.Session, "mara").Select(x => x.Name));
        Assert.Empty(SagaCommands.DialoguesOf(fixture.Session, "nobody"));
        return Task.CompletedTask;
    });

    [Fact]
    public Task OpenDialogueIsEnabledOnACharacterOfTheEntityTreeAndOfThePalette() => RunAsync(async fixture =>
    {
        var openDialogue = SagaCommands.Create().Single(x => x.Id == SagaCommands.OpenDialogue);
        var editor = new SceneEditorViewModel(fixture.Asset<SceneViewModel>("MainScene"));
        try
        {
            Assert.True(await editor.Initialize());
            var entities = editor.HierarchyRoot.Children.OfType<EntityViewModel>().ToDictionary(x => x.Name);
            var session = (SessionViewModel)fixture.Session;

            // The context menu of the entity tree gives its selection
            bool CanOpenFromTheTree(EntityViewModel entity)
                => openDialogue.CanExecute!.Invoke(new CommandContext { Session = session, Location = SceneLocations.EntityTreeContext, Selection = [entity], Editor = editor });
            Assert.True(CanOpenFromTheTree(entities["Mara"]));
            Assert.False(CanOpenFromTheTree(entities["Rock"]));

            // The palette gives the selection of the asset view, and the editor it was opened from
            editor.SelectedContent.Add(entities["Mara"]);
            Assert.True(openDialogue.CanExecute!.Invoke(CommandContext.ForPalette(session, editor: editor)));
            editor.SelectedContent.Clear();
            editor.SelectedContent.Add(entities["Rock"]);
            Assert.False(openDialogue.CanExecute!.Invoke(CommandContext.ForPalette(session, editor: editor)));
        }
        finally
        {
            editor.Destroy();
        }
    });

    [Fact]
    public Task PlayingAStoryStartsItInThePlayer() => RunAsync(fixture =>
    {
        var context = new PanelContext { Descriptor = StoryPlayerViewModel.Panel, Session = fixture.Session, ServiceProvider = fixture.Session.ServiceProvider };
        var player = (StoryPlayerViewModel)StoryPlayerViewModel.Panel.CreateViewModel(context);
        try
        {
            player.Play(fixture.Asset<StoryViewModel>("Story/Main"));
            Assert.Equal("Main", player.SelectedStory?.Name);
            Assert.True(player.IsPlaying);
            Assert.Equal("the_mine", player.CurrentAct);

            // Playing it again starts it over
            player.Characters[0].TalkCommand.Execute(null);
            player.Play(fixture.Asset<StoryViewModel>("Story/Main"));
            Assert.True(player.IsPlaying);
            Assert.Empty(player.Transcript);
        }
        finally
        {
            player.Destroy();
        }
        return Task.CompletedTask;
    });

    /// <summary>
    /// A session with a story (one act, Mara declared), two dialogues of Mara's, the second with a higher priority, and a scene
    /// with Mara and a rock.
    /// </summary>
    private static Task RunAsync(Func<SagaSessionFixture, Task> test)
    {
        var story = new StoryAsset
        {
            FirstActId = "the_mine",
            Declarations = { Characters = { new Content.CharacterDeclaration { Id = "mara", DisplayName = "Mara" } } },
            Acts = { new ActCard { Id = "the_mine" } },
        };
        var low = new DialogueAsset { CharacterId = "mara", EntryNodeId = "end", Nodes = { new EndNodeCard { Id = "end" } } };
        var high = new DialogueAsset { CharacterId = "mara", Priority = 5, EntryNodeId = "end", Nodes = { new EndNodeCard { Id = "end" } } };
        return SagaSessionFixture.RunAsync(async fixture =>
        {
            Assert.False(fixture.Result.HasErrors, fixture.Log);
            // The entities go through the property graph, as the editor adds them
            var graph = (AssetCompositeHierarchyPropertyGraph<EntityDesign, Entity>)fixture.Asset<SceneViewModel>("MainScene").PropertyGraph;
            using (fixture.Session.UndoRedoService.CreateTransaction())
            {
                var index = 0;
                foreach (var entity in new[] { new Entity("Mara") { new SagaCharacterComponent { CharacterId = "mara" } }, new Entity("Rock") })
                {
                    var design = new EntityDesign(entity);
                    graph.AddPartToAsset(new AssetPartCollection<EntityDesign, Entity> { design }, design, null, index++);
                }
            }
            await test(fixture);
        }, ("Story/Main", story), ("Dialogues/Mara_greeting", low), ("Dialogues/Mara", high), ("MainScene", new SceneAsset()));
    }
}
