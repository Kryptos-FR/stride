using ColorRise.Saga.Content;
using Stride.Core;
using Stride.Core.Assets;

namespace ColorRise.Saga.Assets;

/// <summary>
/// A story: its declarations and its plot of acts, with the scenarios and quests of each act. Compiled to a
/// <see cref="StoryDefinition"/>; its dialogues are assets of their own.
/// </summary>
[DataContract("SagaStoryAsset")]
[AssetDescription(FileExtension)]
[AssetContentType(typeof(StoryDefinition))]
[AssetFormatVersion(SagaAssetFormat.Name, SagaAssetFormat.CurrentVersion)]
[Display(1000, "Saga story")]
public sealed class StoryAsset : Asset
{
    public const string FileExtension = ".sdsaga";

    /// <summary>
    /// The names the story shares with the game.
    /// </summary>
    [DataMember(10)]
    public StoryDeclarations Declarations { get; set; } = new();

    /// <summary>
    /// The act the story starts with.
    /// </summary>
    [DataMember(20)]
    public string FirstActId { get; set; } = string.Empty;

    [DataMember(30)]
    public List<ActCard> Acts { get; set; } = [];
}

/// <summary>
/// The name and versions of Saga's asset format.
/// </summary>
public static class SagaAssetFormat
{
    public const string Name = "ColorRise.Saga";

    public const string CurrentVersion = "1.0.0.0";
}
