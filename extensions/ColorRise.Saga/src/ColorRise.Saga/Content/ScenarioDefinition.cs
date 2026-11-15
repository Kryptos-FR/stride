using ColorRise.Saga.Expressions;
using Stride.Core;

namespace ColorRise.Saga.Content;

/// <summary>
/// An event or intrigue of the plot. It starts when its condition holds, whatever the player does, and ends with an outcome.
/// </summary>
[DataContract("SagaScenarioDefinition")]
public sealed class ScenarioDefinition
{
    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// When the scenario starts. Empty means as soon as its act starts.
    /// </summary>
    public SagaCondition StartWhen { get; set; } = new();

    /// <summary>
    /// When the running scenario ends. Empty means never (only the end of its act ends it).
    /// </summary>
    public SagaCondition EndWhen { get; set; } = new();

    /// <summary>
    /// Whether the scenario keeps running when its act ends. Otherwise the end of the act ends it.
    /// </summary>
    public bool ContinuesAcrossActs { get; set; }

    public SagaActions OnStart { get; set; } = new();

    public SagaActions OnEnd { get; set; } = new();

    /// <summary>
    /// The named outcomes; the first whose condition holds when the scenario ends is kept.
    /// </summary>
    public List<ScenarioOutcome> Outcomes { get; set; } = [];

    /// <summary>
    /// The dialogues of the scenario (content references).
    /// </summary>
    public List<DialogueDefinition> Dialogues { get; set; } = [];

    public List<AttributeValue> Attributes { get; set; } = [];
}

/// <summary>
/// A named outcome of a scenario (<c>easier</c>, <c>harder</c>, <c>lost</c>).
/// </summary>
[DataContract("SagaScenarioOutcome")]
public sealed class ScenarioOutcome
{
    public string Name { get; set; } = string.Empty;

    public SagaCondition When { get; set; } = new();
}
