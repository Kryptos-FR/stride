using ColorRise.Saga.Content;

namespace ColorRise.Saga.Editor.Player;

public enum TimelineItemKind
{
    Act,
    Running,
    Ended,
    Offered,
    Active,
    Succeeded,
    Failed,
    Stage,
    Dialogue,
    Choice,
    Event,
    Variable,
    Answer,
    Signal,
}

/// <summary>
/// An item of a lane of the timeline: a span over the steps after which something is in a state, from <see cref="Start"/> to
/// <see cref="End"/>, or a mark at the step where something happened (<see cref="IsMark"/>, at <see cref="Start"/>).
/// </summary>
/// <param name="IsOpen">Whether the span still holds after the last step of the run.</param>
public sealed record TimelineItem(int Start, int End, string Label, TimelineItemKind Kind, string Tip, bool IsMark, bool IsOpen);

public sealed record TimelineLane(string Title, IReadOnlyList<TimelineItem> Items);

/// <summary>
/// The lanes of the story timeline, built from the steps of a run: the acts; one lane per scenario started and quest offered, in
/// the order they first appear; the dialogues and the choices taken; what the game did (events raised, variables set, answers of
/// its functions); the signals the story emitted.
/// </summary>
public static class SagaTimeline
{
    /// <param name="story">The story the run plays, for the names of the quest stages and the titles.</param>
    public static IReadOnlyList<TimelineLane> Build(IReadOnlyList<SagaRunStep> steps, StoryDefinition story)
    {
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(story);
        var acts = new Dictionary<string, ActDefinition>();
        var scenarios = new Dictionary<string, ScenarioDefinition>();
        var quests = new Dictionary<string, QuestDefinition>();
        foreach (var act in story.Plot.Acts)
        {
            acts.TryAdd(act.Id, act);
            foreach (var scenario in act.Scenarios)
                scenarios.TryAdd(scenario.Id, scenario);
            foreach (var quest in act.Quests)
                quests.TryAdd(quest.Id, quest);
        }

        var lanes = new List<TimelineLane>
        {
            Lane("Acts", Spans(steps, x => x.State.CurrentActId is { } id ? (id, TimelineItemKind.Act) : null,
                (label, _, range) => $"Act {Name(label, acts.GetValueOrDefault(label)?.Title)} is the current act {range}")),
        };
        foreach (var (id, isQuest) in Appearances(steps))
            lanes.Add(isQuest ? QuestLane(steps, id, quests.GetValueOrDefault(id)) : ScenarioLane(steps, id, scenarios.GetValueOrDefault(id)?.Title));
        lanes.Add(DialogueLane(steps));
        lanes.Add(GameLane(steps));
        lanes.Add(Lane("Signals", steps.SelectMany((step, i) => step.Signals.Select(signal => SagaRunStep.Describe(signal))
            .Select(text => Mark(i, text, TimelineItemKind.Signal, $"Step {i}: the story emits {text}")))));
        return lanes;
    }

    /// <summary>
    /// What a step did, in words: the step, its transcript, the changes of the story and the signals it emitted.
    /// </summary>
    public static IReadOnlyList<string> Describe(SagaRunStep step, int index)
    {
        ArgumentNullException.ThrowIfNull(step);
        var lines = new List<string> { $"Step {index}: {step.Step}" };
        lines.AddRange(step.Transcript);
        lines.AddRange(step.Changes.Select(change => change.Kind switch
        {
            SagaChangeKind.ActEntered => $"Act {change.Id} begins",
            SagaChangeKind.ActEnded => $"Act {change.Id} ends",
            SagaChangeKind.ScenarioStarted => $"Scenario {change.Id} starts",
            SagaChangeKind.ScenarioEnded => step.State.Scenarios.GetValueOrDefault(change.Id)?.Outcome is { } outcome
                ? $"Scenario {change.Id} ends: {outcome}"
                : $"Scenario {change.Id} ends",
            SagaChangeKind.QuestAvailable => $"Quest {change.Id} is offered",
            SagaChangeKind.QuestActivated => $"Quest {change.Id} becomes active",
            SagaChangeKind.QuestStageDone => $"Quest {change.Id}: a stage is done",
            SagaChangeKind.QuestSucceeded => $"Quest {change.Id} succeeds",
            SagaChangeKind.QuestFailed => $"Quest {change.Id} fails",
            _ => $"{change.Kind} {change.Id}",
        }));
        lines.AddRange(step.Signals.Select(x => $"Signal {SagaRunStep.Describe(x)}"));
        return lines;
    }

    // The scenarios and quests of the run, in the order they first appear: by their changes, else by their state
    private static List<(string Id, bool IsQuest)> Appearances(IReadOnlyList<SagaRunStep> steps)
    {
        var found = new List<(string Id, bool IsQuest)>();
        foreach (var step in steps)
        {
            foreach (var change in step.Changes)
            {
                if (change.Kind is SagaChangeKind.ScenarioStarted or SagaChangeKind.ScenarioEnded)
                    Add((change.Id, false));
                else if (change.Kind is not (SagaChangeKind.ActEntered or SagaChangeKind.ActEnded))
                    Add((change.Id, true));
            }
            foreach (var (id, progress) in step.State.Scenarios)
            {
                if (progress.State != ScenarioState.Pending)
                    Add((id, false));
            }
            foreach (var (id, progress) in step.State.Quests)
            {
                if (progress.State != QuestState.NotStarted)
                    Add((id, true));
            }
        }
        return found;

        void Add((string, bool) item)
        {
            if (!found.Contains(item))
                found.Add(item);
        }
    }

    private static TimelineLane ScenarioLane(IReadOnlyList<SagaRunStep> steps, string id, string? title)
    {
        var name = Name(id, title);
        var items = Spans(steps, x => x.State.Scenarios.GetValueOrDefault(id)?.State == ScenarioState.Running ? ("running", TimelineItemKind.Running) : null,
            (_, _, range) => $"Scenario {name} runs {range}");
        for (var i = 0; i < steps.Count; ++i)
        {
            if (!steps[i].Changes.Any(x => x.Kind == SagaChangeKind.ScenarioEnded && x.Id == id))
                continue;
            var outcome = steps[i].State.Scenarios.GetValueOrDefault(id)?.Outcome;
            items.Add(Mark(i, outcome ?? "ended", TimelineItemKind.Ended,
                outcome is null ? $"Scenario {name} ends at step {i}, with no outcome" : $"Scenario {name} ends at step {i} with the outcome {outcome}"));
        }
        return Lane(id, items);
    }

    private static TimelineLane QuestLane(IReadOnlyList<SagaRunStep> steps, string id, QuestDefinition? quest)
    {
        var name = Name(id, quest?.Title);
        var items = Spans(steps, x => x.State.Quests.GetValueOrDefault(id)?.State switch
        {
            QuestState.Available => ("offered", TimelineItemKind.Offered),
            QuestState.Active => ("active", TimelineItemKind.Active),
            _ => null,
        }, (label, _, range) => $"Quest {name} is {label} {range}");
        for (var i = 0; i < steps.Count; ++i)
        {
            var changes = steps[i].Changes.Where(x => x.Id == id).ToList();
            // The stages done in the step are the last ones before the stage the quest is at after it
            var stage = (steps[i].State.Quests.GetValueOrDefault(id)?.Stage ?? 0) - changes.Count(x => x.Kind == SagaChangeKind.QuestStageDone);
            foreach (var change in changes)
            {
                switch (change.Kind)
                {
                    case SagaChangeKind.QuestStageDone:
                        var definition = quest is not null && stage >= 0 && stage < quest.Stages.Count ? quest.Stages[stage] : null;
                        var stageName = definition?.Id ?? $"stage {stage + 1}";
                        items.Add(Mark(i, stageName, TimelineItemKind.Stage, $"Quest {name}: the stage {Name(stageName, definition?.Title)} is done at step {i}"));
                        ++stage;
                        break;
                    case SagaChangeKind.QuestSucceeded:
                        items.Add(Mark(i, "succeeded", TimelineItemKind.Succeeded, $"Quest {name} succeeds at step {i}"));
                        break;
                    case SagaChangeKind.QuestFailed:
                        items.Add(Mark(i, "failed", TimelineItemKind.Failed, $"Quest {name} fails at step {i}"));
                        break;
                }
            }
        }
        return Lane(id, items);
    }

    // A dialogue spans the steps it is open during, its closing step included; talking again starts a new play
    private static TimelineLane DialogueLane(IReadOnlyList<SagaRunStep> steps)
    {
        var items = new List<TimelineItem>();
        (int Start, string Name)? open = null;
        for (var i = 0; i < steps.Count; ++i)
        {
            var step = steps[i];
            if (open is { } left && (step.Step.Kind == SagaStepKind.Talk || step.DialogueName != left.Name))
            {
                items.Add(Play(left.Start, i - 1, left.Name, false));
                open = null;
            }
            if (step.DialogueName is not { } dialogue)
                continue;
            open ??= (i, dialogue);
            if (step.Step.Kind == SagaStepKind.Choose)
                items.Add(Mark(i, step.Step.Value, TimelineItemKind.Choice, $"Step {i}: the player chooses \"{step.Step.Value}\""));
            if (!step.IsDialogueOpen)
            {
                items.Add(Play(open.Value.Start, i, dialogue, false));
                open = null;
            }
        }
        if (open is { } last)
            items.Add(Play(last.Start, steps.Count - 1, last.Name, true));
        return Lane("Dialogues", items);

        TimelineItem Play(int start, int end, string dialogue, bool isOpen)
            => new(start, end, dialogue, TimelineItemKind.Dialogue,
                $"Dialogue {dialogue} {Range(start, end, isOpen)}: {string.Join(" → ", steps[end].DialoguePath)}", false, isOpen);
    }

    // What the game did: the events it raised, the variables it set, the answers of its functions
    private static TimelineLane GameLane(IReadOnlyList<SagaRunStep> steps)
    {
        var items = new List<TimelineItem>();
        for (var i = 0; i < steps.Count; ++i)
        {
            var step = steps[i].Step;
            switch (step.Kind)
            {
                case SagaStepKind.Raise:
                    items.Add(Mark(i, step.Name, TimelineItemKind.Event, $"Step {i}: the game raises {step.Name}"));
                    break;
                case SagaStepKind.Set:
                    items.Add(Mark(i, $"{step.Name} = {step.Value}", TimelineItemKind.Variable, $"Step {i}: the game sets {step.Name} to {step.Value}"));
                    break;
                case SagaStepKind.Answer:
                    items.Add(Mark(i, $"{step.Name}() → {step.Value}", TimelineItemKind.Answer, $"Step {i}: {step.Name}() answers {step.Value} from here on"));
                    break;
            }
        }
        return Lane("Game", items);
    }

    // The runs of steps after which a state holds, each a span; the tip gets the label, the kind and the range of steps in words
    private static List<TimelineItem> Spans(IReadOnlyList<SagaRunStep> steps, Func<SagaRunStep, (string Label, TimelineItemKind Kind)?> stateAfter,
        Func<string, TimelineItemKind, string, string> tip)
    {
        var items = new List<TimelineItem>();
        (string Label, TimelineItemKind Kind)? current = null;
        var start = 0;
        for (var i = 0; i <= steps.Count; ++i)
        {
            var next = i < steps.Count ? stateAfter(steps[i]) : null;
            if (next == current)
                continue;
            if (current is { } span)
            {
                var isOpen = i == steps.Count;
                items.Add(new TimelineItem(start, i - 1, span.Label, span.Kind, tip(span.Label, span.Kind, Range(start, i - 1, isOpen)), false, isOpen));
            }
            current = next;
            start = i;
        }
        return items;
    }

    private static TimelineItem Mark(int step, string label, TimelineItemKind kind, string tip) => new(step, step, label, kind, tip, true, false);

    // The items in the order of their steps, a span before the marks of its first step
    private static TimelineLane Lane(string title, IEnumerable<TimelineItem> items) => new(title, [.. items.OrderBy(x => x.Start).ThenBy(x => x.IsMark)]);

    private static string Range(int start, int end, bool isOpen) => isOpen ? $"from step {start} on" : start == end ? $"at step {start}" : $"from step {start} to step {end}";

    private static string Name(string id, string? title) => string.IsNullOrEmpty(title) || title == id ? id : $"{title} ({id})";
}
