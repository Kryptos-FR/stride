namespace ColorRise.Skein;

/// <summary>
/// Whether an action is a command or a function.
/// </summary>
public enum YarnActionKind
{
    /// <summary>
    /// An instruction to the game: <c>&lt;&lt;open CellarDoor&gt;&gt;</c>.
    /// </summary>
    Command,

    /// <summary>
    /// A value for expressions: <c>has_item("oil")</c>.
    /// </summary>
    Function,
}

/// <summary>
/// What a game gives to its scripts, as the compiler and the editors need to know it: the source of the function
/// declarations and of the definitions file of Yarn Spinner's tools.
/// </summary>
public sealed record YarnActionDescription
{
    /// <summary>
    /// The name scripts write.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// A command or a function.
    /// </summary>
    public required YarnActionKind Kind { get; init; }

    /// <summary>
    /// The parameters scripts write, in order. The target of an instance command is not one of them.
    /// </summary>
    public IReadOnlyList<YarnActionParameter> Parameters { get; init; } = [];

    /// <summary>
    /// For a function, the type of its result: <c>number</c>, <c>string</c> or <c>bool</c>. Null for a command.
    /// </summary>
    public string? ReturnType { get; init; }

    /// <summary>
    /// For a command that runs on an object, the name of the object's type: the first word after the command names one.
    /// </summary>
    public string? TargetType { get; init; }

    /// <summary>
    /// Whether the dialogue waits for the command: it returns a task.
    /// </summary>
    public bool IsAsync { get; init; }

    /// <summary>
    /// What the attribute says the action does.
    /// </summary>
    public string? Documentation { get; init; }

    /// <summary>
    /// The method behind the action, as <c>Type.Method</c>.
    /// </summary>
    public required string Method { get; init; }
}

/// <summary>
/// A parameter of a command or a function.
/// </summary>
public sealed record YarnActionParameter
{
    /// <summary>
    /// The name of the parameter in the code.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// What scripts write there: <c>number</c>, <c>string</c>, <c>bool</c>, or <c>any</c> for the name of an object.
    /// An enum is a <c>string</c> with <see cref="Choices"/>.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Whether scripts may leave it out.
    /// </summary>
    public bool IsOptional { get; init; }

    /// <summary>
    /// What it is when left out, as a script would write it; null without a default, or when the default is no object.
    /// </summary>
    public string? DefaultValue { get; init; }

    /// <summary>
    /// Whether it takes every word that is left.
    /// </summary>
    public bool IsParams { get; init; }

    /// <summary>
    /// For an enum, the words it accepts.
    /// </summary>
    public IReadOnlyList<string> Choices { get; init; } = [];
}
