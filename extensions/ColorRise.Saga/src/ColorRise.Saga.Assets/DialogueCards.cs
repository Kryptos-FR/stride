using Stride.Core;
using Stride.Core.Mathematics;

namespace ColorRise.Saga.Assets;

/// <summary>
/// A node of a dialogue asset.
/// </summary>
[DataContract(Inherited = true)]
public abstract class DialogueNodeCard
{
    /// <summary>
    /// The stable id of the node (localization will use it).
    /// </summary>
    [DataMember(1)]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// A name for <c>visited("dialogue.name")</c>; visits are counted for named nodes only.
    /// </summary>
    [DataMember(2)]
    public string Name { get; set; } = string.Empty;

    [DataMember(1000)]
    [Display(Browsable = false)]
    public Vector2 Position { get; set; }
}

[DataContract("SagaLineNodeCard")]
[Display("Line")]
public sealed class LineNodeCard : DialogueNodeCard
{
    [DataMember(10)]
    public string SpeakerId { get; set; } = string.Empty;

    [DataMember(20)]
    public string Text { get; set; } = string.Empty;

    [DataMember(30)]
    public string Actions { get; set; } = string.Empty;

    /// <summary>
    /// The node that follows. Empty ends the dialogue.
    /// </summary>
    [DataMember(40)]
    public string NextId { get; set; } = string.Empty;
}

[DataContract("SagaChoicesNodeCard")]
[Display("Choices")]
public sealed class ChoicesNodeCard : DialogueNodeCard
{
    [DataMember(10)]
    public List<DialogueChoiceCard> Choices { get; set; } = [];
}

[DataContract("SagaDialogueChoiceCard")]
public sealed class DialogueChoiceCard
{
    [DataMember(10)]
    public string Text { get; set; } = string.Empty;

    [DataMember(20)]
    public string When { get; set; } = string.Empty;

    /// <summary>
    /// Whether the choice is hidden while its condition is false; otherwise it is shown, disabled.
    /// </summary>
    [DataMember(30)]
    public bool HideWhenFalse { get; set; } = true;

    [DataMember(40)]
    public bool Once { get; set; }

    [DataMember(50)]
    public string Actions { get; set; } = string.Empty;

    /// <summary>
    /// The node the choice leads to. Empty ends the dialogue.
    /// </summary>
    [DataMember(60)]
    public string TargetId { get; set; } = string.Empty;
}

[DataContract("SagaBranchNodeCard")]
[Display("Branch")]
public sealed class BranchNodeCard : DialogueNodeCard
{
    [DataMember(10)]
    public List<DialogueLinkCard> Links { get; set; } = [];
}

[DataContract("SagaDialogueLinkCard")]
public sealed class DialogueLinkCard
{
    [DataMember(10)]
    public string When { get; set; } = string.Empty;

    [DataMember(20)]
    public string TargetId { get; set; } = string.Empty;
}

[DataContract("SagaEndNodeCard")]
[Display("End")]
public sealed class EndNodeCard : DialogueNodeCard
{
}
