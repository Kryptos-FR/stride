using ColorRise.Saga.Editor.Player;
using ColorRise.Saga.Editor.Tests.Helpers;

namespace ColorRise.Saga.Editor.Tests.Player;

/// <summary>
/// The lanes of the story timeline, built from the steps of a run: a span covers the steps after which something is in a state,
/// a mark the step where something happened.
/// </summary>
public sealed class SagaTimelineTests
{
    [Fact]
    public void LanesShowTheActsScenariosQuestsDialoguesAndInputs()
    {
        var story = MaraStory.Build();
        var steps = Play(story, SagaStep.Talk("mara"), SagaStep.Continue(), SagaStep.Choose(1, "Leave"), SagaStep.Raise("met_mara"));

        var lanes = SagaTimeline.Build(steps, story);

        Assert.Equal(["Acts", "cave_in", "find_the_lamp", "Dialogues", "Game", "Signals"], lanes.Select(x => x.Title));
        Assert.Equal([(0, 4, "the_mine", TimelineItemKind.Act, false, true)], Items(lanes[0]));
        Assert.Equal([(0, 4, "running", TimelineItemKind.Running, false, true)], Items(lanes[1]));
        Assert.Equal([(3, 4, "offered", TimelineItemKind.Offered, false, true)], Items(lanes[2]));
        Assert.Equal([(1, 3, "Mara", TimelineItemKind.Dialogue, false, false), (3, 3, "Leave", TimelineItemKind.Choice, true, false)], Items(lanes[3]));
        Assert.Equal([(4, 4, "met_mara", TimelineItemKind.Event, true, false)], Items(lanes[4]));
        Assert.Equal([(3, 3, "ring()", TimelineItemKind.Signal, true, false)], Items(lanes[5]));
        Assert.All(lanes.SelectMany(x => x.Items), x => Assert.NotEmpty(x.Tip));
    }

    [Fact]
    public void QuestLaneShowsItsStagesAndHowItEnds()
    {
        var (story, dialogue) = MaraStory.Create();
        story.Acts[0].Quests[0].StartsAutomatically = true;
        story.Acts[0].Scenarios[0].Outcomes.Add(new Assets.ScenarioOutcomeCard { Name = "rescued" });
        story.Acts[0].Scenarios[0].EndWhen = "quest.find_the_lamp.state == succeeded";
        var built = MaraStory.Build(story, dialogue);
        var steps = Play(built, SagaStep.Talk("mara"), SagaStep.Continue(), SagaStep.Choose(1, "Leave"),
            SagaStep.Raise("met_mara"), SagaStep.Set("day", "2"), SagaStep.Answer("has_pickaxe", "true"));

        var lanes = SagaTimeline.Build(steps, built);

        // Offered and taken on in the same step: active from there; the last stage done, it succeeds and the scenario ends
        Assert.Equal(
            [(3, 4, "active", TimelineItemKind.Active, false, false), (4, 4, "meet", TimelineItemKind.Stage, true, false), (5, 5, "dawn", TimelineItemKind.Stage, true, false), (5, 5, "succeeded", TimelineItemKind.Succeeded, true, false)],
            Items(lanes.Single(x => x.Title == "find_the_lamp")));
        Assert.Equal([(0, 4, "running", TimelineItemKind.Running, false, false), (5, 5, "rescued", TimelineItemKind.Ended, true, false)], Items(lanes.Single(x => x.Title == "cave_in")));
        Assert.Equal([(4, 4, "met_mara", TimelineItemKind.Event, true, false), (5, 5, "day = 2", TimelineItemKind.Variable, true, false), (6, 6, "has_pickaxe() → true", TimelineItemKind.Answer, true, false)],
            Items(lanes.Single(x => x.Title == "Game")));
    }

    [Fact]
    public void ADialogueWithNothingToSayOrLeftForAnotherEndsThere()
    {
        var story = MaraStory.Build();
        var steps = Play(story, SagaStep.Talk("mara"), SagaStep.Raise("met_mara"), SagaStep.Talk("mara"), SagaStep.Continue());

        var dialogues = SagaTimeline.Build(steps, story).Single(x => x.Title == "Dialogues");

        // Talking again starts the dialogue over: two plays
        Assert.Equal([(1, 2, "Mara", TimelineItemKind.Dialogue, false, false), (3, 4, "Mara", TimelineItemKind.Dialogue, false, true)], Items(dialogues));
    }

    [Fact]
    public void ChangesAreDescribedInWords()
    {
        var steps = Play(MaraStory.Build(), SagaStep.Talk("mara"), SagaStep.Continue(), SagaStep.Choose(1, "Leave"));

        Assert.Equal(["Step 0: Start", "Act the_mine begins", "Scenario cave_in starts"], SagaTimeline.Describe(steps[0], 0));
        Assert.Equal(["Step 3: Choose \"Leave\"", "> Leave", "(the dialogue ends)", "Quest find_the_lamp is offered", "Signal ring()"], SagaTimeline.Describe(steps[3], 3));
    }

    private static List<(int, int, string, TimelineItemKind, bool, bool)> Items(TimelineLane lane)
        => [.. lane.Items.Select(x => (x.Start, x.End, x.Label, x.Kind, x.IsMark, x.IsOpen))];

    private static List<SagaRunStep> Play(Content.StoryDefinition story, params SagaStep[] steps)
    {
        using var run = new SagaRun(story);
        return [.. steps.Prepend(SagaStep.Start()).Select(run.Apply)];
    }
}
