namespace ColorRise.Saga;

/// <summary>
/// What changed in the story.
/// </summary>
public enum SagaChangeKind
{
    ActEntered,
    ActEnded,
    ScenarioStarted,
    ScenarioEnded,
    QuestAvailable,
    QuestActivated,
    QuestStageDone,
    QuestSucceeded,
    QuestFailed,
}

/// <summary>
/// A change of the story: its kind and the id of the act, scenario or quest.
/// </summary>
public sealed record SagaChangedEventArgs(SagaChangeKind Kind, string Id);
