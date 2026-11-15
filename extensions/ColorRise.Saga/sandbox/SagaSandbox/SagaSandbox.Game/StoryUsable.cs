using Stride.Core;
using Stride.Engine;

namespace SagaSandbox;

/// <summary>
/// Something the player can use (E) at one stage of a quest: using it raises a story event.
/// </summary>
/// <remarks>
/// Game code, not Saga: it shows what a game writes today to tie an object of its scenes to the story.
/// </remarks>
[DataContract("StoryUsable")]
[Display("Story usable")]
[ComponentCategory("Saga sandbox")]
public sealed class StoryUsable : EntityComponent
{
    /// <summary>
    /// What the prompt says, e.g. "Clear the rubble".
    /// </summary>
    [DataMember(10)]
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// The event of the story raised when the player uses it.
    /// </summary>
    [DataMember(20)]
    public string RaiseEvent { get; set; } = string.Empty;

    /// <summary>
    /// The quest it belongs to.
    /// </summary>
    [DataMember(30)]
    public string QuestId { get; set; } = string.Empty;

    /// <summary>
    /// The stage of the quest at which it can be used: how many of the quest's stages are done.
    /// </summary>
    [DataMember(40)]
    public int AtStage { get; set; }

    /// <summary>
    /// Whether it stays hidden until the quest reaches <see cref="AtStage"/>.
    /// </summary>
    [DataMember(50)]
    public bool HideBefore { get; set; }

    /// <summary>
    /// Whether it disappears once used.
    /// </summary>
    [DataMember(60)]
    public bool HideAfter { get; set; }

    /// <summary>
    /// Whether the player used it.
    /// </summary>
    [DataMemberIgnore]
    public bool IsUsed { get; set; }
}
