using ColorRise.Saga.Expressions;
using Stride.Core;

namespace ColorRise.Saga.Content;

/// <summary>
/// A node of a dialogue.
/// </summary>
[DataContract(Inherited = true)]
public abstract class DialogueNode
{
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// A name conditions can test with <c>visited("name")</c> and <c>visits("name")</c>. Visits are counted only for named nodes.
    /// </summary>
    public string? Name { get; set; }
}

/// <summary>
/// A line said by a participant.
/// </summary>
[DataContract("SagaLineNode")]
public sealed class LineNode : DialogueNode
{
    public string SpeakerId { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public SagaActions Actions { get; set; } = new();

    /// <summary>
    /// The node that follows. None ends the dialogue.
    /// </summary>
    public string? NextId { get; set; }
}

/// <summary>
/// The player's choices.
/// </summary>
[DataContract("SagaChoicesNode")]
public sealed class ChoicesNode : DialogueNode
{
    public List<DialogueChoice> Choices { get; set; } = [];
}

/// <summary>
/// A choice of the player.
/// </summary>
[DataContract("SagaDialogueChoice")]
public sealed class DialogueChoice
{
    public string Text { get; set; } = string.Empty;

    public SagaCondition When { get; set; } = new();

    /// <summary>
    /// Whether the choice is hidden while its condition is false. Otherwise it is shown, disabled.
    /// </summary>
    public bool HideWhenFalse { get; set; } = true;

    /// <summary>
    /// Whether the choice can be taken only once.
    /// </summary>
    public bool Once { get; set; }

    public SagaActions Actions { get; set; } = new();

    /// <summary>
    /// The node the choice leads to. None ends the dialogue.
    /// </summary>
    public string? TargetId { get; set; }
}

/// <summary>
/// A silent branch: the first link whose condition holds is followed.
/// </summary>
[DataContract("SagaBranchNode")]
public sealed class BranchNode : DialogueNode
{
    public List<DialogueLink> Links { get; set; } = [];
}

/// <summary>
/// A link of a branch.
/// </summary>
[DataContract("SagaDialogueLink")]
public sealed class DialogueLink
{
    public SagaCondition When { get; set; } = new();

    public string TargetId { get; set; } = string.Empty;
}

/// <summary>
/// The end of the dialogue.
/// </summary>
[DataContract("SagaEndNode")]
public sealed class EndNode : DialogueNode
{
}
