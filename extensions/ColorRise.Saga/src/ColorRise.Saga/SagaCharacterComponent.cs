using Stride.Core;
using Stride.Engine;

namespace ColorRise.Saga;

/// <summary>
/// Makes an entity a character of the story: talking to it plays the dialogues attached to that character.
/// </summary>
[DataContract("SagaCharacterComponent")]
[Display("Saga character")]
[ComponentCategory("Saga")]
public sealed class SagaCharacterComponent : EntityComponent
{
    /// <summary>
    /// The id of the character, as the story declares it.
    /// </summary>
    [DataMember(10)]
    public string CharacterId { get; set; } = string.Empty;

    /// <summary>
    /// Starts the dialogue of this character that can play now.
    /// </summary>
    /// <returns>The session, or null when no story is loaded or no dialogue of the character can play.</returns>
    /// <exception cref="ArgumentException">The loaded story does not declare <see cref="CharacterId"/>.</exception>
    public DialogueSession? Talk()
    {
        if (string.IsNullOrEmpty(CharacterId))
            return null;
        var runtime = Entity?.EntityManager?.Services.GetService<SagaSystem>()?.Runtime;
        return runtime?.Talk(CharacterId);
    }
}
