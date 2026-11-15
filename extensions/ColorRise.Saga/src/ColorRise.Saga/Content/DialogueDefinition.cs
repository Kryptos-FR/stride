using ColorRise.Saga.Expressions;
using Stride.Core;
using Stride.Core.Serialization;
using Stride.Core.Serialization.Contents;

namespace ColorRise.Saga.Content;

/// <summary>
/// A compiled dialogue: its activation (character, condition, priority) and its nodes. It has no reference back to its story.
/// </summary>
[DataContract("SagaDialogueDefinition")]
[ContentSerializer(typeof(DataContentSerializer<DialogueDefinition>))]
[ReferenceSerializer, DataSerializerGlobal(typeof(ReferenceSerializer<DialogueDefinition>), Profile = "Content")]
public sealed class DialogueDefinition
{
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The character the dialogue is attached to: talking to that character can play it.
    /// </summary>
    public string CharacterId { get; set; } = string.Empty;

    public SagaCondition When { get; set; } = new();

    /// <summary>
    /// When several dialogues of a character can play, the highest priority wins.
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Whether the dialogue plays only once.
    /// </summary>
    public bool Once { get; set; }

    /// <summary>
    /// Whether the dialogue plays only while its owner is live: a running scenario, or a quest from "available" until its
    /// outcome. Off, only its own condition matters.
    /// </summary>
    public bool OnlyWhileOwnerLive { get; set; } = true;

    /// <summary>
    /// The characters taking part (<c>player</c> is the player).
    /// </summary>
    public List<string> Participants { get; set; } = [];

    public string EntryNodeId { get; set; } = string.Empty;

    public List<DialogueNode> Nodes { get; set; } = [];

    public List<AttributeValue> Attributes { get; set; } = [];
}
