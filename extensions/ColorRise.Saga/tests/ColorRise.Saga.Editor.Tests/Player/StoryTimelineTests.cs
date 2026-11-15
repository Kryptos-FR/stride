using ColorRise.Saga.Assets;
using ColorRise.Saga.Editor.Player;
using ColorRise.Saga.Editor.Tests.Helpers;
using ColorRise.Saga.Editor.ViewModels;
using Stride.Core.Assets.Editor.Panels;

namespace ColorRise.Saga.Editor.Tests.Player;

/// <summary>
/// The story timeline panel: it follows the playback of the session, and its commands move the run back and forth and replay it.
/// </summary>
public sealed class StoryTimelineTests
{
    [Fact]
    public Task TimelineFollowsThePlayback() => RunAsync((fixture, playback, timeline) =>
    {
        Assert.Empty(timeline.Steps);
        Assert.Empty(timeline.Lanes);
        Assert.Equal("The story is not playing.", timeline.Status);
        Assert.False(timeline.BackCommand.IsEnabled);
        Assert.False(timeline.ForwardCommand.IsEnabled);

        Play(fixture, playback);

        Assert.Equal([(0, "▶"), (1, "@"), (2, "…"), (3, ">")], timeline.Steps.Select(x => (x.Index, x.Glyph)));
        Assert.All(timeline.Steps, x => Assert.False(x.IsFuture || x.IsDiverged || x.PlaysDifferently));
        Assert.Equal("Step 0: Start\nAct the_mine begins\nScenario cave_in starts", timeline.Steps[0].Tip);
        Assert.Equal(["Acts", "cave_in", "find_the_lamp", "Dialogues", "Game", "Signals"], timeline.Lanes.Select(x => x.Title));
        Assert.Equal(3, timeline.Cursor);
        Assert.Equal("Step 3 of 3.", timeline.Status);
        Assert.Equal(["Step 3: Choose \"Leave\"", "> Leave", "(the dialogue ends)", "Quest find_the_lamp is offered", "Signal ring()"], timeline.Details);
        Assert.True(timeline.BackCommand.IsEnabled);
        Assert.False(timeline.ForwardCommand.IsEnabled);

        // Back: the steps after the cursor stay, to come back to
        timeline.BackCommand.Execute(null);
        Assert.Equal((2, 2), (playback.Cursor, timeline.Cursor));
        Assert.Equal([false, false, false, true], timeline.Steps.Select(x => x.IsFuture));
        Assert.Equal("Step 2: Continue", timeline.Details[0]);
        Assert.True(timeline.ForwardCommand.IsEnabled);

        timeline.GoToStepCommand.Execute(0);
        Assert.Equal(0, playback.Cursor);
        Assert.False(timeline.BackCommand.IsEnabled);
        timeline.ForwardCommand.Execute(null);
        Assert.Equal(1, playback.Cursor);
        Assert.Equal("Step 1 of 3.", timeline.Status);

        playback.Reset();
        Assert.Empty(timeline.Steps);
        Assert.Empty(timeline.Details);
        return Task.CompletedTask;
    });

    [Fact]
    public Task ReplayFollowsTheEditedStoryAndShowsWhereItStops() => RunAsync((fixture, playback, timeline) =>
    {
        Play(fixture, playback);
        Assert.False(timeline.ReplayCommand.IsEnabled);

        var dialogue = fixture.Asset<DialogueViewModel>("Dialogues/Mara").Asset;
        fixture.Edit(dialogue.Nodes[0], nameof(LineNodeCard.Text), "Back again?");
        Assert.True(timeline.ReplayCommand.IsEnabled);
        Assert.Equal("Step 3 of 3. The story changed since this run: replay it to follow the changes.", timeline.Status);

        timeline.ReplayCommand.Execute(null);
        Assert.False(timeline.ReplayCommand.IsEnabled);
        Assert.Equal([false, true, false, false], timeline.Steps.Select(x => x.PlaysDifferently));

        // The choice taken is gone: the run stops before it
        var hub = (ChoicesNodeCard)dialogue.Nodes[1];
        fixture.Edit(hub.Choices[0], nameof(DialogueChoiceCard.When), string.Empty);
        using (var transaction = fixture.Session.UndoRedoService.CreateTransaction())
        {
            fixture.Session.AssetNodeContainer.GetNode(hub)[nameof(ChoicesNodeCard.Choices)].Target!.Remove(hub.Choices[1], new Stride.Core.Quantum.NodeIndex(1));
            fixture.Session.UndoRedoService.SetName(transaction, "Remove choice");
        }
        timeline.ReplayCommand.Execute(null);
        Assert.Equal(2, timeline.Cursor);
        Assert.Equal([false, false, false, true], timeline.Steps.Select(x => x.IsDiverged));
        Assert.Equal("Step 2 of 3. Step 3 no longer applies: The choice \"Leave\" is no longer offered.", timeline.Status);
        Assert.False(timeline.ForwardCommand.IsEnabled);
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

    private static Task RunAsync(Func<SagaSessionFixture, SagaPlaybackService, StoryTimelineViewModel, Task> test)
    {
        var (story, dialogue) = MaraStory.Create();
        return SagaSessionFixture.RunAsync(async fixture =>
        {
            Assert.False(fixture.Result.HasErrors, fixture.Log);
            var context = new PanelContext { Descriptor = StoryTimelineViewModel.Panel, Session = fixture.Session, ServiceProvider = fixture.Session.ServiceProvider };
            var timeline = (StoryTimelineViewModel)StoryTimelineViewModel.Panel.CreateViewModel(context);
            try
            {
                Assert.Equal(("Story timeline", PanelPlacement.Bottom, false), (StoryTimelineViewModel.Panel.Title(), StoryTimelineViewModel.Panel.DefaultPlacement, StoryTimelineViewModel.Panel.VisibleByDefault));
                await test(fixture, fixture.Session.ServiceProvider.Get<SagaPlaybackService>(), timeline);
            }
            finally
            {
                timeline.Destroy();
            }
        }, ("Story/Main", story), ("Dialogues/Mara", dialogue));
    }
}
