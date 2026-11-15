using ColorRise.Saga.Expressions;
using Stride.Core;

namespace ColorRise.Saga.Content;

/// <summary>
/// A quest: optional, or one that changes a scenario's branch, or one that gives something. The game reports facts; the quest
/// follows its conditions.
/// </summary>
[DataContract("SagaQuestDefinition")]
public sealed class QuestDefinition
{
    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// The scenario the quest belongs to, if any. The quest is offered only while it runs.
    /// </summary>
    public string? ScenarioId { get; set; }

    /// <summary>
    /// When the quest becomes available. Empty means as soon as its act starts (or its scenario, if it has one).
    /// </summary>
    public SagaCondition OfferWhen { get; set; } = new();

    /// <summary>
    /// Whether the quest becomes active as soon as it is available (otherwise the game or an action activates it).
    /// </summary>
    public bool StartsAutomatically { get; set; }

    public List<QuestStage> Stages { get; set; } = [];

    /// <summary>
    /// When the active quest succeeds. Empty means when its last stage is done (never for a quest without stages: an action
    /// or the game sets its state).
    /// </summary>
    public SagaCondition SucceedWhen { get; set; } = new();

    /// <summary>
    /// When the active quest fails. Empty means never (only the end of its act or an action can fail it).
    /// </summary>
    public SagaCondition FailWhen { get; set; } = new();

    public SagaActions OnActivate { get; set; } = new();

    public SagaActions OnSucceed { get; set; } = new();

    public SagaActions OnFail { get; set; } = new();

    /// <summary>
    /// The dialogues of the quest (content references).
    /// </summary>
    public List<DialogueDefinition> Dialogues { get; set; } = [];

    public List<AttributeValue> Attributes { get; set; } = [];
}

/// <summary>
/// A stage of a quest, done when its condition holds.
/// </summary>
[DataContract("SagaQuestStage")]
public sealed class QuestStage
{
    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public SagaCondition DoneWhen { get; set; } = new();

    public SagaActions OnDone { get; set; } = new();
}
