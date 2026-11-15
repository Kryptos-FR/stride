using ColorRise.Saga.Content;
using Stride.Core;
using Stride.Core.Mathematics;

namespace ColorRise.Saga.Assets;

/// <summary>
/// An act of a story asset. Conditions and actions are written as text.
/// </summary>
[DataContract("SagaActCard")]
public sealed class ActCard
{
    [DataMember(10)]
    public string Id { get; set; } = string.Empty;

    [DataMember(20)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// When the act ends. Empty means never.
    /// </summary>
    [DataMember(30)]
    public string EndWhen { get; set; } = string.Empty;

    [DataMember(40)]
    public List<ActLinkCard> Next { get; set; } = [];

    [DataMember(50)]
    public List<ScenarioCard> Scenarios { get; set; } = [];

    [DataMember(60)]
    public List<QuestCard> Quests { get; set; } = [];

    [DataMember(70)]
    public List<AttributeValue> Attributes { get; set; } = [];

    /// <summary>
    /// Where the card is in the plot graph.
    /// </summary>
    [DataMember(80)]
    [Display(Browsable = false)]
    public Vector2 Position { get; set; }
}

/// <summary>
/// A link to the act that follows.
/// </summary>
[DataContract("SagaActLinkCard")]
public sealed class ActLinkCard
{
    [DataMember(10)]
    public string TargetActId { get; set; } = string.Empty;

    /// <summary>
    /// When the link is taken. Empty means always.
    /// </summary>
    [DataMember(20)]
    public string When { get; set; } = string.Empty;
}

/// <summary>
/// A scenario of an act.
/// </summary>
[DataContract("SagaScenarioCard")]
public sealed class ScenarioCard
{
    [DataMember(10)]
    public string Id { get; set; } = string.Empty;

    [DataMember(20)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// When the scenario starts. Empty means as soon as its act starts.
    /// </summary>
    [DataMember(30)]
    public string StartWhen { get; set; } = string.Empty;

    /// <summary>
    /// When the scenario ends. Empty means only when its act ends.
    /// </summary>
    [DataMember(40)]
    public string EndWhen { get; set; } = string.Empty;

    [DataMember(50)]
    public bool ContinuesAcrossActs { get; set; }

    [DataMember(60)]
    public string OnStart { get; set; } = string.Empty;

    [DataMember(70)]
    public string OnEnd { get; set; } = string.Empty;

    [DataMember(80)]
    public List<ScenarioOutcomeCard> Outcomes { get; set; } = [];

    /// <summary>
    /// The dialogues of the scenario.
    /// </summary>
    [DataMember(90)]
    public List<DialogueDefinition> Dialogues { get; set; } = [];

    [DataMember(100)]
    public List<AttributeValue> Attributes { get; set; } = [];

    [DataMember(110)]
    [Display(Browsable = false)]
    public Vector2 Position { get; set; }
}

/// <summary>
/// A named outcome of a scenario.
/// </summary>
[DataContract("SagaScenarioOutcomeCard")]
public sealed class ScenarioOutcomeCard
{
    [DataMember(10)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// When the outcome is kept. Empty means always (put it last).
    /// </summary>
    [DataMember(20)]
    public string When { get; set; } = string.Empty;
}

/// <summary>
/// A quest of an act.
/// </summary>
[DataContract("SagaQuestCard")]
public sealed class QuestCard
{
    [DataMember(10)]
    public string Id { get; set; } = string.Empty;

    [DataMember(20)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// The scenario the quest belongs to. Empty means the act itself.
    /// </summary>
    [DataMember(30)]
    public string ScenarioId { get; set; } = string.Empty;

    [DataMember(40)]
    public string OfferWhen { get; set; } = string.Empty;

    [DataMember(50)]
    public bool StartsAutomatically { get; set; }

    [DataMember(60)]
    public List<QuestStageCard> Stages { get; set; } = [];

    [DataMember(70)]
    public string SucceedWhen { get; set; } = string.Empty;

    [DataMember(80)]
    public string FailWhen { get; set; } = string.Empty;

    [DataMember(90)]
    public string OnActivate { get; set; } = string.Empty;

    [DataMember(100)]
    public string OnSucceed { get; set; } = string.Empty;

    [DataMember(110)]
    public string OnFail { get; set; } = string.Empty;

    /// <summary>
    /// The dialogues of the quest.
    /// </summary>
    [DataMember(120)]
    public List<DialogueDefinition> Dialogues { get; set; } = [];

    [DataMember(130)]
    public List<AttributeValue> Attributes { get; set; } = [];

    [DataMember(140)]
    [Display(Browsable = false)]
    public Vector2 Position { get; set; }
}

/// <summary>
/// A stage of a quest.
/// </summary>
[DataContract("SagaQuestStageCard")]
public sealed class QuestStageCard
{
    [DataMember(10)]
    public string Id { get; set; } = string.Empty;

    [DataMember(20)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// When the stage is done. Empty means when an action or the game sets the quest's stage.
    /// </summary>
    [DataMember(30)]
    public string DoneWhen { get; set; } = string.Empty;

    [DataMember(40)]
    public string OnDone { get; set; } = string.Empty;
}
