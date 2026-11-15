using ColorRise.Saga.Assets;
using ColorRise.Saga.Editor.Player;
using ColorRise.Saga.Editor.Tests.Helpers;
using ColorRise.Saga.Editor.ViewModels;

namespace ColorRise.Saga.Editor.Tests.Player;

/// <summary>
/// The story and dialogue editors show where the play is: the current act, the scenarios running and the quests offered or
/// taken on, the dialogue playing, and in the dialogue editor the node the dialogue is at and the way it came.
/// </summary>
public sealed class PlayMarksTests
{
    [Fact]
    public Task StoryEditorMarksWhereThePlayIs() => RunAsync(async (fixture, playback) =>
    {
        var editor = new StoryEditorViewModel(fixture.Asset<StoryViewModel>("Story/Main"));
        var other = new StoryEditorViewModel(fixture.Asset<StoryViewModel>("Story/Other"));
        Assert.True(await editor.Initialize());
        Assert.True(await other.Initialize());
        try
        {
            Assert.Equal((SagaPlayMark.None, ""), Mark(editor, "the_mine"));
            playback.Select(fixture.Asset<StoryViewModel>("Story/Main"));
            Assert.True(playback.Start());

            // The plot: the current act
            Assert.Equal((SagaPlayMark.Current, ""), Mark(editor, "the_mine"));
            Assert.Equal((SagaPlayMark.None, ""), Mark(other, "the_mine"));

            // The act: the scenario runs; the quest is offered once Mara is left
            editor.OpenCommand.Execute(Node(editor, "the_mine"));
            Assert.Equal((SagaPlayMark.Running, ""), Mark(editor, "cave_in"));
            Assert.Equal((SagaPlayMark.None, ""), Mark(editor, "find_the_lamp"));
            Assert.Null(playback.Do(SagaStep.Talk("mara")));
            Assert.Null(playback.Do(SagaStep.Continue()));
            Assert.Null(playback.Do(SagaStep.Choose(1, "Leave")));
            Assert.Equal((SagaPlayMark.Offered, ""), Mark(editor, "find_the_lamp"));

            // Taken on at once after an edit and a replay: active at its first stage
            fixture.Edit(fixture.Asset<StoryViewModel>("Story/Main").Asset.Acts[0].Quests[0], nameof(QuestCard.StartsAutomatically), true);
            Assert.True(playback.Replay());
            Assert.Equal((SagaPlayMark.Running, "stage 1/2"), Mark(editor, "find_the_lamp"));

            // The scenario: its dialogue, playing at step 1, played once at step 3
            editor.OpenCommand.Execute(Node(editor, "cave_in"));
            Assert.Equal((SagaPlayMark.Visited, "played ×1"), Mark(editor, "Mara"));
            playback.GoTo(1);
            Assert.Equal((SagaPlayMark.Current, ""), Mark(editor, "Mara"));

            playback.Reset();
            Assert.Equal((SagaPlayMark.None, ""), Mark(editor, "Mara"));
            Assert.All(other.Nodes, x => Assert.Equal(SagaPlayMark.None, x.PlayMark));
        }
        finally
        {
            editor.Destroy();
            other.Destroy();
        }
    });

    [Fact]
    public Task EndedQuestsAreMarkedWithoutANote() => RunAsync(async (fixture, playback) =>
    {
        var story = fixture.Asset<StoryViewModel>("Story/Main");
        var editor = new StoryEditorViewModel(story);
        Assert.True(await editor.Initialize());
        try
        {
            var quest = story.Asset.Acts[0].Quests[0];
            fixture.Edit(quest, nameof(QuestCard.StartsAutomatically), true);
            playback.Select(story);
            Assert.True(playback.Start());
            editor.OpenCommand.Execute(Node(editor, "the_mine"));

            // Offered and taken on at once, then both stages done: it succeeds
            Assert.Null(playback.Do(SagaStep.Set("lamp_asked", "true")));
            Assert.Equal((SagaPlayMark.Running, "stage 1/2"), Mark(editor, "find_the_lamp"));
            Assert.Null(playback.Do(SagaStep.Raise("met_mara")));
            Assert.Null(playback.Do(SagaStep.Set("day", "2")));
            Assert.Equal((SagaPlayMark.Done, ""), Mark(editor, "find_the_lamp"));

            // Failing once a day passes, before Mara was met
            fixture.Edit(quest, nameof(QuestCard.FailWhen), "day > 1");
            Assert.True(playback.Replay());
            playback.GoTo(1);
            Assert.Null(playback.Do(SagaStep.Set("day", "2")));
            Assert.Equal((SagaPlayMark.Failed, ""), Mark(editor, "find_the_lamp"));
        }
        finally
        {
            editor.Destroy();
        }
    });

    [Fact]
    public Task DialogueEditorMarksTheNodeAndThePath() => RunAsync(async (fixture, playback) =>
    {
        var editor = new DialogueEditorViewModel(fixture.Asset<DialogueViewModel>("Dialogues/Mara"));
        Assert.True(await editor.Initialize());
        try
        {
            playback.Select(fixture.Asset<StoryViewModel>("Story/Main"));
            Assert.True(playback.Start());
            Assert.All(editor.Nodes, x => Assert.Equal(SagaPlayMark.None, x.PlayMark));

            Assert.Null(playback.Do(SagaStep.Talk("mara")));
            Assert.Null(playback.Do(SagaStep.Continue()));
            Assert.Equal([SagaPlayMark.Visited, SagaPlayMark.Current, SagaPlayMark.None], Marks(editor, "hello", "hub", "end"));

            // Ended: the way it went, nothing current
            Assert.Null(playback.Do(SagaStep.Choose(1, "Leave")));
            Assert.Equal([SagaPlayMark.Visited, SagaPlayMark.Visited, SagaPlayMark.Visited], Marks(editor, "hello", "hub", "end"));

            playback.GoTo(1);
            Assert.Equal([SagaPlayMark.Current, SagaPlayMark.None, SagaPlayMark.None], Marks(editor, "hello", "hub", "end"));

            playback.Reset();
            Assert.All(editor.Nodes, x => Assert.Equal(SagaPlayMark.None, x.PlayMark));
        }
        finally
        {
            editor.Destroy();
        }
    });

    private static SagaGraphNodeViewModel Node<T>(SagaGraphEditorViewModel<T> editor, string id)
        where T : Stride.Core.Assets.Presentation.ViewModels.AssetViewModel
        => editor.Nodes.Single(x => x.Id == id);

    private static (SagaPlayMark, string) Mark<T>(SagaGraphEditorViewModel<T> editor, string id)
        where T : Stride.Core.Assets.Presentation.ViewModels.AssetViewModel
    {
        var node = Node(editor, id);
        return (node.PlayMark, node.PlayNote);
    }

    private static List<SagaPlayMark> Marks(DialogueEditorViewModel editor, params string[] ids) => [.. ids.Select(id => Node(editor, id).PlayMark)];

    /// <summary>
    /// A session with Mara's story and dialogue, and another story with an act of the same id.
    /// </summary>
    private static Task RunAsync(Func<SagaSessionFixture, SagaPlaybackService, Task> test)
    {
        var (story, dialogue) = MaraStory.Create();
        var other = new StoryAsset { FirstActId = "the_mine", Acts = { new ActCard { Id = "the_mine" } } };
        return SagaSessionFixture.RunAsync(async fixture =>
        {
            Assert.False(fixture.Result.HasErrors, fixture.Log);
            await test(fixture, fixture.Session.ServiceProvider.Get<SagaPlaybackService>());
        }, ("Story/Main", story), ("Dialogues/Mara", dialogue), ("Story/Other", other));
    }
}
