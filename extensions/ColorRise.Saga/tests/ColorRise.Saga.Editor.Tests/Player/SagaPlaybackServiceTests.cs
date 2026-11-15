using ColorRise.Saga.Assets;
using ColorRise.Saga.Editor.Player;
using ColorRise.Saga.Editor.Tests.Helpers;
using ColorRise.Saga.Editor.ViewModels;

namespace ColorRise.Saga.Editor.Tests.Player;

/// <summary>
/// The playback service of the session: a run of a story with a cursor that goes back and forth, that notices edits of the story
/// and replays the run on the new version.
/// </summary>
public sealed class SagaPlaybackServiceTests
{
    [Fact]
    public Task ServiceIsAServiceOfTheSession() => RunAsync(async (fixture, playback) =>
    {
        Assert.Same(playback, fixture.Session.ServiceProvider.Get<SagaPlaybackService>());
        var editor = new StoryEditorViewModel(fixture.Asset<StoryViewModel>("Story/Main"));
        Assert.True(await editor.Initialize());
        try
        {
            Assert.Same(playback, editor.ServiceProvider.TryGet<SagaPlaybackService>());
        }
        finally
        {
            editor.Destroy();
        }
    });

    [Fact]
    public Task GoingBackAndForthRestoresTheDialogue() => RunAsync((fixture, playback) =>
    {
        Play(fixture, playback);
        Assert.Equal(3, playback.Cursor);
        Assert.Null(playback.Run!.Dialogue);
        Assert.Equal(QuestState.Available, playback.Run.Runtime.State.Quests["find_the_lamp"].State);

        playback.GoTo(2);
        Assert.Equal((2, 4), (playback.Cursor, playback.Steps.Count));
        Assert.Equal(["Leave"], playback.Run.Dialogue!.Choices.Select(x => x.Text));
        Assert.Equal(QuestState.NotStarted, playback.Run.Runtime.State.Quests["find_the_lamp"].State);
        Assert.Equal(("hello hub", true), Path(playback, "Mara"));

        playback.GoTo(3);
        Assert.Null(playback.Run.Dialogue);
        Assert.Equal(QuestState.Available, playback.Run.Runtime.State.Quests["find_the_lamp"].State);
        Assert.Equal(("hello hub end", false), Path(playback, "Mara"));

        // A new step from an earlier one drops the steps after it
        playback.GoTo(1);
        Assert.Null(playback.Do(SagaStep.Raise("met_mara")));
        Assert.Equal((2, 3), (playback.Cursor, playback.Steps.Count));
        Assert.Equal("Raise met_mara", playback.CurrentStep!.Step.ToString());
        Assert.Equal("The story declares no event 'dusk'.", playback.Do(SagaStep.Raise("dusk")));
        Assert.Equal(3, playback.Steps.Count);
        return Task.CompletedTask;
    });

    [Fact]
    public Task EditingTheStoryMarksTheRunStale() => RunAsync((fixture, playback) =>
    {
        Play(fixture, playback);
        Assert.False(playback.IsStale);

        fixture.Edit(fixture.Asset<StoryViewModel>("Story/Main").Asset.Acts[0].Scenarios[0], nameof(ScenarioCard.Title), "The cave-in");
        Assert.True(playback.IsStale);
        Assert.True(playback.Replay());
        Assert.False(playback.IsStale);

        var hello = (LineNodeCard)fixture.Asset<DialogueViewModel>("Dialogues/Mara").Asset.Nodes[0];
        fixture.Edit(hello, nameof(LineNodeCard.Text), "Back again?");
        Assert.True(playback.IsStale);
        return Task.CompletedTask;
    });

    [Fact]
    public Task ReplayFollowsTheEditedStory() => RunAsync((fixture, playback) =>
    {
        Play(fixture, playback);
        var dialogue = fixture.Asset<DialogueViewModel>("Dialogues/Mara").Asset;
        fixture.Edit(dialogue.Nodes[0], nameof(LineNodeCard.Text), "Back again?");

        Assert.True(playback.Replay());
        Assert.Equal(3, playback.Cursor);
        Assert.Equal([false, true, false, false], playback.Steps.Select(x => x.PlaysDifferently));
        Assert.Contains("Mara: Back again?", playback.Steps[1].Lines);

        // The choice taken is gone (the other one is always shown now): the run stops before it
        var hub = (ChoicesNodeCard)dialogue.Nodes[1];
        fixture.Edit(hub.Choices[0], nameof(DialogueChoiceCard.When), string.Empty);
        using (var transaction = fixture.Session.UndoRedoService.CreateTransaction())
        {
            fixture.Session.AssetNodeContainer.GetNode(hub)[nameof(ChoicesNodeCard.Choices)].Target!.Remove(hub.Choices[1], new Stride.Core.Quantum.NodeIndex(1));
            fixture.Session.UndoRedoService.SetName(transaction, "Remove choice");
        }
        Assert.True(playback.Replay());
        Assert.Equal((2, 3), (playback.Cursor, playback.DivergedAt));
        Assert.Equal("The choice \"Leave\" is no longer offered.", playback.Divergence);
        Assert.Equal(4, playback.Steps.Count);

        // Going forward stops there again; a new step drops the divergence
        playback.GoTo(3);
        Assert.Equal(2, playback.Cursor);
        Assert.Null(playback.Do(SagaStep.Raise("met_mara")));
        Assert.Null(playback.DivergedAt);
        Assert.Equal(string.Empty, playback.Divergence);
        return Task.CompletedTask;
    });

    [Fact]
    public Task ReplayWithErrorsKeepsTheRun() => RunAsync((fixture, playback) =>
    {
        Play(fixture, playback);
        fixture.Edit(fixture.Asset<StoryViewModel>("Story/Main").Asset.Acts[0].Scenarios[0], nameof(ScenarioCard.StartWhen), "day >");

        Assert.False(playback.Replay());

        Assert.True(playback.HasErrors);
        Assert.True(playback.IsStale);
        Assert.Equal((3, 4), (playback.Cursor, playback.Steps.Count));
        Assert.Null(playback.Run!.Dialogue);
        return Task.CompletedTask;
    });

    [Fact]
    public Task ResetForgetsTheRun() => RunAsync((fixture, playback) =>
    {
        Play(fixture, playback);

        playback.Reset();

        Assert.False(playback.IsPlaying);
        Assert.Null(playback.Run);
        Assert.Empty(playback.Steps);
        Assert.Equal(-1, playback.Cursor);
        Assert.Null(playback.CurrentStep);
        Assert.Same(fixture.Asset<StoryViewModel>("Story/Main"), playback.Story);
        Assert.False(playback.HasErrors);
        // Warnings stay: the quest of the lamp never becomes active
        Assert.Contains(playback.Diagnostics, x => x.Card == "act the_mine / quest find_the_lamp");
        return Task.CompletedTask;
    });

    [Fact]
    public Task ChangedIsRaisedOnEachChange() => RunAsync((fixture, playback) =>
    {
        var count = 0;
        playback.Changed += (_, _) => ++count;

        playback.Select(fixture.Asset<StoryViewModel>("Story/Main"));
        Assert.Equal(1, count);
        playback.Start();
        Assert.Equal(2, count);
        playback.Do(SagaStep.Talk("mara"));
        Assert.Equal(3, count);
        playback.GoTo(0);
        Assert.Equal(4, count);
        fixture.Edit(fixture.Asset<StoryViewModel>("Story/Main").Asset.Acts[0].Scenarios[0], nameof(ScenarioCard.Title), "The cave-in");
        Assert.Equal(5, count);
        playback.Replay();
        Assert.Equal(6, count);
        playback.Reset();
        Assert.Equal(7, count);
        return Task.CompletedTask;
    });

    [Fact]
    public Task ServiceKnowsTheDialoguesItPlays() => RunAsync((fixture, playback) =>
    {
        var dialogue = fixture.Asset<DialogueViewModel>("Dialogues/Mara");
        Assert.False(playback.Plays(dialogue.Id));
        Play(fixture, playback);
        Assert.True(playback.Plays(dialogue.Id));
        Assert.False(playback.Plays(fixture.Asset<StoryViewModel>("Story/Main").Id));
        return Task.CompletedTask;
    });

    // Start, talk to Mara, go on to the choices, leave
    private static void Play(SagaSessionFixture fixture, SagaPlaybackService playback)
    {
        playback.Select(fixture.Asset<StoryViewModel>("Story/Main"));
        Assert.True(playback.Start());
        Assert.Null(playback.Do(SagaStep.Talk("mara")));
        Assert.Null(playback.Do(SagaStep.Continue()));
        Assert.Null(playback.Do(SagaStep.Choose(1, "Leave")));
    }

    // The path as one text, to compare
    private static (string, bool) Path(SagaPlaybackService playback, string dialogue)
        => playback.DialoguePath(dialogue) is { } found ? (string.Join(" ", found.Path), found.IsOpen) : (string.Empty, false);

    private static Task RunAsync(Func<SagaSessionFixture, SagaPlaybackService, Task> test)
    {
        var (story, dialogue) = MaraStory.Create();
        return SagaSessionFixture.RunAsync(async fixture =>
        {
            Assert.False(fixture.Result.HasErrors, fixture.Log);
            await test(fixture, fixture.Session.ServiceProvider.Get<SagaPlaybackService>());
        }, ("Story/Main", story), ("Dialogues/Mara", dialogue));
    }
}
