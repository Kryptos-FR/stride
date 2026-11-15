using ColorRise.Saga.Expressions;
using Stride.Core;

namespace ColorRise.Saga.Content;

/// <summary>
/// The named elements a story shares with the game: what conditions and actions may use.
/// </summary>
[DataContract("SagaStoryDeclarations")]
public sealed class StoryDeclarations
{
    /// <summary>
    /// The variables of the story state. The game and the actions can change them.
    /// </summary>
    public List<VariableDeclaration> Variables { get; set; } = [];

    /// <summary>
    /// The events the game raises (and actions can raise).
    /// </summary>
    public List<EventDeclaration> Events { get; set; } = [];

    /// <summary>
    /// The functions conditions can call. The game registers them by name.
    /// </summary>
    public List<FunctionDeclaration> Functions { get; set; } = [];

    /// <summary>
    /// The signals actions can emit. The game listens to them by name.
    /// </summary>
    public List<SignalDeclaration> Signals { get; set; } = [];

    /// <summary>
    /// The characters dialogues can be attached to and spoken by.
    /// </summary>
    public List<CharacterDeclaration> Characters { get; set; } = [];

    /// <summary>
    /// The attributes cards can carry, for the game to read (a deadline, a region, a reward).
    /// </summary>
    public List<AttributeTypeDeclaration> AttributeTypes { get; set; } = [];
}

/// <summary>
/// A variable of the story state. Its name may be dotted (<c>player.strength</c>).
/// </summary>
[DataContract("SagaVariableDeclaration")]
public sealed class VariableDeclaration
{
    public string Name { get; set; } = string.Empty;

    public SagaTypeKind Type { get; set; }

    /// <summary>
    /// The initial value, as a literal (<c>0</c>, <c>"Mara"</c>, <c>true</c>). Empty means the default of the type.
    /// </summary>
    public string Default { get; set; } = string.Empty;
}

/// <summary>
/// An event the game raises, by name (<c>mine_collapsed</c>).
/// </summary>
[DataContract("SagaEventDeclaration")]
public sealed class EventDeclaration
{
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// A function conditions can call (<c>has_item("sword", 2)</c>). The game registers its implementation.
/// </summary>
[DataContract("SagaFunctionDeclaration")]
public sealed class FunctionDeclaration
{
    public string Name { get; set; } = string.Empty;

    public List<SagaTypeKind> Parameters { get; set; } = [];

    public SagaTypeKind ReturnType { get; set; } = SagaTypeKind.Bool;
}

/// <summary>
/// A signal actions can emit (<c>emit give_item("sword", 2)</c>). The game listens to it.
/// </summary>
[DataContract("SagaSignalDeclaration")]
public sealed class SignalDeclaration
{
    public string Name { get; set; } = string.Empty;

    public List<SagaTypeKind> Parameters { get; set; } = [];
}

/// <summary>
/// A character of the story. Entities carry its id in their character component.
/// </summary>
[DataContract("SagaCharacterDeclaration")]
public sealed class CharacterDeclaration
{
    public string Id { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;
}

/// <summary>
/// The kinds of card an attribute can be put on.
/// </summary>
[Flags]
public enum SagaCardKinds
{
    None = 0,
    Act = 1,
    Scenario = 2,
    Quest = 4,
    Dialogue = 8,
    All = Act | Scenario | Quest | Dialogue,
}

/// <summary>
/// An attribute cards can carry. Saga only stores it; the game gives it a meaning.
/// </summary>
[DataContract("SagaAttributeTypeDeclaration")]
public sealed class AttributeTypeDeclaration
{
    public string Name { get; set; } = string.Empty;

    public SagaTypeKind Type { get; set; } = SagaTypeKind.String;

    public SagaCardKinds AppliesTo { get; set; } = SagaCardKinds.All;
}

/// <summary>
/// The value of an attribute on a card, as a literal.
/// </summary>
[DataContract("SagaAttributeValue")]
public sealed class AttributeValue
{
    public string Name { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}
