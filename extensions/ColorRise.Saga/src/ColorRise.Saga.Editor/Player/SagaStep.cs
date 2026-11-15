namespace ColorRise.Saga.Editor.Player;

/// <summary>
/// What a step of a story run does.
/// </summary>
public enum SagaStepKind
{
    /// <summary>
    /// Starts the story.
    /// </summary>
    Start,

    /// <summary>
    /// Raises an event, as the game would.
    /// </summary>
    Raise,

    /// <summary>
    /// Sets a variable, as the game would.
    /// </summary>
    Set,

    /// <summary>
    /// Gives the answer a function of the game returns from now on.
    /// </summary>
    Answer,

    /// <summary>
    /// Talks to a character.
    /// </summary>
    Talk,

    /// <summary>
    /// Goes on after a line of the dialogue.
    /// </summary>
    Continue,

    /// <summary>
    /// Takes a choice of the dialogue.
    /// </summary>
    Choose,
}

/// <summary>
/// A step of a story run, as the writer took it: names and texts only, so that it applies again to another build of the story.
/// </summary>
/// <param name="Kind">What the step does.</param>
/// <param name="Name">The event, variable, function or character.</param>
/// <param name="Value">The value of a variable or the answer of a function, as a literal; the text of a choice.</param>
/// <param name="ChoiceIndex">The index of a choice in its node.</param>
public sealed record SagaStep(SagaStepKind Kind, string Name = "", string Value = "", int ChoiceIndex = -1)
{
    public static SagaStep Start() => new(SagaStepKind.Start);

    public static SagaStep Raise(string eventName) => new(SagaStepKind.Raise, eventName);

    public static SagaStep Set(string variable, string value) => new(SagaStepKind.Set, variable, value);

    public static SagaStep Answer(string function, string answer) => new(SagaStepKind.Answer, function, answer);

    public static SagaStep Talk(string characterId) => new(SagaStepKind.Talk, characterId);

    public static SagaStep Continue() => new(SagaStepKind.Continue);

    public static SagaStep Choose(int index, string text) => new(SagaStepKind.Choose, Value: text, ChoiceIndex: index);

    /// <summary>
    /// The step in words: <c>Raise met_mara</c>, <c>Set day = 4</c>, <c>Choose "Leave"</c>.
    /// </summary>
    public override string ToString() => Kind switch
    {
        SagaStepKind.Raise => $"Raise {Name}",
        SagaStepKind.Set => $"Set {Name} = {Value}",
        SagaStepKind.Answer => $"Answer {Name}() → {Value}",
        SagaStepKind.Talk => $"Talk to {Name}",
        SagaStepKind.Choose => $"Choose \"{Value}\"",
        _ => Kind.ToString(),
    };
}
