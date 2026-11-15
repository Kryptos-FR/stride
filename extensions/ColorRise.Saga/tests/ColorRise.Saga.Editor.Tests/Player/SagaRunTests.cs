using ColorRise.Saga.Editor.Player;
using ColorRise.Saga.Editor.Tests.Helpers;
using ColorRise.Saga.Expressions;

namespace ColorRise.Saga.Editor.Tests.Player;

/// <summary>
/// A run of a story, step by step: what each step did, the steps that can't apply, how a choice is found again, and that the same
/// steps give the same run.
/// </summary>
public sealed class SagaRunTests
{
    [Fact]
    public void RunRecordsWhatEachStepDid()
    {
        using var run = new SagaRun(MaraStory.Build());

        var start = run.Apply(SagaStep.Start());
        Assert.Equal([(SagaChangeKind.ActEntered, "the_mine"), (SagaChangeKind.ScenarioStarted, "cave_in")], start.Changes.Select(x => (x.Kind, x.Id)));
        Assert.Equal("the_mine", start.State.CurrentActId);
        Assert.Null(start.DialogueName);

        var talk = run.Apply(SagaStep.Talk("mara"));
        Assert.Equal(["Talking to Mara", "Mara: You came back."], talk.Lines);
        Assert.Equal(("Mara", true), (talk.DialogueName, talk.IsDialogueOpen));
        Assert.Equal(["hello"], talk.DialoguePath);
        Assert.Equal("hello", run.Dialogue!.CurrentNodeId);

        var next = run.Apply(SagaStep.Continue());
        Assert.Empty(next.Lines);
        Assert.Equal(["hello", "hub"], next.DialoguePath);

        var leave = run.Apply(SagaStep.Choose(1, "Leave"));
        Assert.Equal(SagaStep.Choose(1, "Leave"), leave.Step);
        Assert.Equal(["> Leave", "(the dialogue ends)"], leave.Lines);
        Assert.Equal([(SagaChangeKind.QuestAvailable, "find_the_lamp")], leave.Changes.Select(x => (x.Kind, x.Id)));
        Assert.Equal(["ring"], leave.Signals.Select(x => x.Name));
        Assert.Equal(("Mara", false), (leave.DialogueName, leave.IsDialogueOpen));
        Assert.Equal(["hello", "hub", "end"], leave.DialoguePath);
        Assert.Null(run.Dialogue);

        // A step that touches no dialogue says so; the state is a copy
        var raise = run.Apply(SagaStep.Raise("met_mara"));
        Assert.Null(raise.DialogueName);
        Assert.Equal(1, raise.State.Events["met_mara"]);
        Assert.False(leave.State.Events.ContainsKey("met_mara"));
    }

    [Fact]
    public void StepsThatCantApplySayWhy()
    {
        using var run = new SagaRun(MaraStory.Build());
        Assert.Equal("The story has not started.", run.CanApply(SagaStep.Raise("met_mara")));
        run.Apply(SagaStep.Start());

        Assert.Equal("The story has already started.", run.CanApply(SagaStep.Start()));
        Assert.Equal("The story declares no event 'dusk'.", run.CanApply(SagaStep.Raise("dusk")));
        Assert.Equal("The story declares no variable 'night'.", run.CanApply(SagaStep.Set("night", "1")));
        Assert.NotNull(run.CanApply(SagaStep.Set("day", "dawn")));
        Assert.NotNull(run.CanApply(SagaStep.Answer("has_pickaxe", "maybe")));
        Assert.Equal("The story declares no function 'has_rope'.", run.CanApply(SagaStep.Answer("has_rope", "true")));
        Assert.Equal("The story declares no character 'bram'.", run.CanApply(SagaStep.Talk("bram")));
        Assert.Equal("No dialogue is playing.", run.CanApply(SagaStep.Continue()));
        Assert.Equal("No dialogue is playing.", run.CanApply(SagaStep.Choose(0, "Leave")));

        run.Apply(SagaStep.Talk("mara"));
        Assert.Equal("The dialogue shows a line: continue first.", run.CanApply(SagaStep.Choose(0, "Leave")));
        run.Apply(SagaStep.Continue());
        Assert.Equal("The dialogue waits for a choice.", run.CanApply(SagaStep.Continue()));
        Assert.Equal("The choice \"Run\" is no longer offered.", run.CanApply(SagaStep.Choose(4, "Run")));

        var error = Assert.Throws<InvalidOperationException>(() => run.Apply(SagaStep.Choose(4, "Run")));
        Assert.Equal("The choice \"Run\" is no longer offered.", error.Message);
        Assert.Null(run.CanApply(SagaStep.Set("day", "3")));
    }

    [Fact]
    public void ChoiceIsMatchedByTextThenIndex()
    {
        using var run = Talking();

        // "Ask about the mine" is hidden without a pickaxe: "Leave" is shown first, at index 1
        Assert.Equal(SagaStep.Choose(1, "Leave"), run.Apply(SagaStep.Choose(7, "Leave")).Step);

        using var other = Talking();
        Assert.Equal(SagaStep.Choose(1, "Leave"), other.Apply(SagaStep.Choose(1, "Go away")).Step);
    }

    [Fact]
    public void AnswersAreSteps()
    {
        using var run = new SagaRun(MaraStory.Build());
        run.Apply(SagaStep.Start());
        Assert.Equal(SagaValue.Bool(false), run.Answer("has_pickaxe"));

        var answer = run.Apply(SagaStep.Answer("has_pickaxe", "true"));
        Assert.Equal(SagaValue.Bool(true), run.Answer("has_pickaxe"));
        Assert.Empty(answer.Changes);
        run.Apply(SagaStep.Talk("mara"));
        run.Apply(SagaStep.Continue());
        Assert.Equal(["Ask about the mine", "Leave"], run.Dialogue!.Choices.Select(x => x.Text));
    }

    [Fact]
    public void ReplayingTheSameStepsGivesTheSameRun()
    {
        var story = MaraStory.Build();
        SagaStep[] steps = [SagaStep.Start(), SagaStep.Set("day", "2"), SagaStep.Talk("mara"), SagaStep.Continue(), SagaStep.Choose(1, "Leave"), SagaStep.Raise("met_mara")];
        using var first = new SagaRun(story);
        using var second = new SagaRun(story);

        var once = steps.Select(first.Apply).Select(x => x.Signature).ToList();
        var again = steps.Select(second.Apply).Select(x => x.Signature).ToList();

        Assert.Equal(once, again);
        Assert.Contains("QuestAvailable find_the_lamp", once[4]);
    }

    [Fact]
    public void StepsSayWhatTheyAre()
    {
        Assert.Equal(
            ["Start", "Raise met_mara", "Set day = 4", "Answer has_pickaxe() → true", "Talk to mara", "Continue", "Choose \"Leave\""],
            new[] { SagaStep.Start(), SagaStep.Raise("met_mara"), SagaStep.Set("day", "4"), SagaStep.Answer("has_pickaxe", "true"), SagaStep.Talk("mara"), SagaStep.Continue(), SagaStep.Choose(1, "Leave") }
                .Select(x => x.ToString()));
    }

    private static SagaRun Talking()
    {
        var run = new SagaRun(MaraStory.Build());
        run.Apply(SagaStep.Start());
        run.Apply(SagaStep.Talk("mara"));
        run.Apply(SagaStep.Continue());
        return run;
    }
}
