using ColorRise.Saga.Expressions;
using Stride.Core;

namespace ColorRise.Saga.Content;

/// <summary>
/// An act of the plot: its scenarios and quests, when it ends, and which act follows.
/// </summary>
[DataContract("SagaActDefinition")]
public sealed class ActDefinition
{
    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// When the act ends. Empty means never (the last act).
    /// </summary>
    public SagaCondition EndWhen { get; set; } = new();

    /// <summary>
    /// The acts that can follow; the first link whose condition holds when the act ends is taken.
    /// </summary>
    public List<ActLink> Next { get; set; } = [];

    public List<ScenarioDefinition> Scenarios { get; set; } = [];

    public List<QuestDefinition> Quests { get; set; } = [];

    public List<AttributeValue> Attributes { get; set; } = [];
}

/// <summary>
/// A link to the act that follows, when its condition holds.
/// </summary>
[DataContract("SagaActLink")]
public sealed class ActLink
{
    public string TargetActId { get; set; } = string.Empty;

    public SagaCondition When { get; set; } = new();
}
