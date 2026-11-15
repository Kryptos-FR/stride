using ColorRise.Saga.Content;
using Stride.Core;
using Stride.Core.Assets;

namespace ColorRise.Saga.Assets;

/// <summary>
/// A dialogue: its activation and its nodes. Compiled to a <see cref="DialogueDefinition"/>, checked against its story.
/// </summary>
[DataContract("SagaDialogueAsset")]
[AssetDescription(FileExtension)]
[AssetContentType(typeof(DialogueDefinition))]
[AssetFormatVersion(SagaAssetFormat.Name, SagaAssetFormat.CurrentVersion)]
[Display(1001, "Saga dialogue")]
public sealed class DialogueAsset : Asset
{
    public const string FileExtension = ".sdsagadialog";

    /// <summary>
    /// The story whose names the dialogue uses. An asset reference, so that compiled content has no cycle.
    /// </summary>
    [DataMember(10)]
    public AssetReference? Story { get; set; }

    /// <summary>
    /// The character the dialogue is attached to.
    /// </summary>
    [DataMember(20)]
    public string CharacterId { get; set; } = string.Empty;

    /// <summary>
    /// When the dialogue can play. Empty means always (while its owner is live).
    /// </summary>
    [DataMember(30)]
    public string When { get; set; } = string.Empty;

    [DataMember(40)]
    public int Priority { get; set; }

    [DataMember(50)]
    public bool Once { get; set; }

    [DataMember(60)]
    public bool OnlyWhileOwnerLive { get; set; } = true;

    /// <summary>
    /// The characters taking part; <c>player</c> is the player.
    /// </summary>
    [DataMember(70)]
    public List<string> Participants { get; set; } = [];

    [DataMember(80)]
    public string EntryNodeId { get; set; } = string.Empty;

    [DataMember(90)]
    public List<DialogueNodeCard> Nodes { get; set; } = [];

    [DataMember(100)]
    public List<AttributeValue> Attributes { get; set; } = [];
}
