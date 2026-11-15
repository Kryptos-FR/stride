using ColorRise.Saga.Expressions;
using Stride.Core;

namespace ColorRise.Saga;

/// <summary>
/// The lifecycle of a quest.
/// </summary>
public enum QuestState
{
    NotStarted,
    Available,
    Active,
    Succeeded,
    Failed,
}

/// <summary>
/// The lifecycle of a scenario.
/// </summary>
public enum ScenarioState
{
    Pending,
    Running,
    Done,
}

/// <summary>
/// The state of a story: the one object the game saves with its save files.
/// </summary>
[DataContract("SagaState")]
public sealed class SagaState
{
    public string? CurrentActId { get; set; }

    public List<string> CompletedActs { get; set; } = [];

    public Dictionary<string, SagaValue> Variables { get; set; } = [];

    public Dictionary<string, QuestProgress> Quests { get; set; } = [];

    public Dictionary<string, ScenarioProgress> Scenarios { get; set; } = [];

    /// <summary>
    /// How many times each event was raised.
    /// </summary>
    public Dictionary<string, int> Events { get; set; } = [];

    /// <summary>
    /// How many times each named dialogue node was played.
    /// </summary>
    public Dictionary<string, int> Visits { get; set; } = [];

    /// <summary>
    /// The dialogues and choices marked "once" that were used.
    /// </summary>
    public List<string> UsedOnce { get; set; } = [];
}

/// <summary>
/// Where a quest is.
/// </summary>
[DataContract("SagaQuestProgress")]
public sealed class QuestProgress
{
    public QuestState State { get; set; }

    /// <summary>
    /// How many of its stages are done.
    /// </summary>
    public int Stage { get; set; }
}

/// <summary>
/// Where a scenario is.
/// </summary>
[DataContract("SagaScenarioProgress")]
public sealed class ScenarioProgress
{
    public ScenarioState State { get; set; }

    /// <summary>
    /// The outcome kept when the scenario ended, if one held.
    /// </summary>
    public string? Outcome { get; set; }
}
