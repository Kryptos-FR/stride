using Stride.Core;
using Stride.Engine;
using Stride.Engine.Design;

namespace ColorRise.Skein;

/// <summary>
/// Gives an entity the name the scripts call it by: <c>&lt;&lt;walk Odile Harbour&gt;&gt;</c> finds the entity whose actor
/// is named Odile, whatever the entity's own name and wherever it was loaded or spawned from.
/// </summary>
[DataContract("DialogueActorComponent")]
[Display("Dialogue actor")]
[ComponentCategory("Dialogue")]
[DefaultEntityComponentProcessor(typeof(DialogueActorProcessor), ExecutionMode = ExecutionMode.Runtime)]
public sealed class DialogueActorComponent : EntityComponent
{
    /// <summary>
    /// The name the scripts use. Empty: the entity's own name.
    /// </summary>
    /// <userdoc>The name Yarn scripts call this entity by, in commands and as a speaker. Leave empty to use the entity's name.</userdoc>
    [DataMember(10)]
    public string ActorName { get; set; } = "";

    /// <summary>
    /// The name the scripts find this actor by: <see cref="ActorName"/>, or the entity's name when it is empty.
    /// </summary>
    [DataMemberIgnore]
    public string EffectiveName => string.IsNullOrEmpty(ActorName) ? Entity?.Name ?? "" : ActorName;
}
