namespace ColorRise.Skein.Editor.Player;

/// <summary>
/// What a step of a played dialogue is.
/// </summary>
public enum PlayerStepKind
{
    /// <summary>
    /// The run starts at a node.
    /// </summary>
    Start,

    /// <summary>
    /// The dialogue enters a node.
    /// </summary>
    NodeEnter,

    /// <summary>
    /// A line is said; the run waits there.
    /// </summary>
    Line,

    /// <summary>
    /// Options are offered; the run waits for a choice.
    /// </summary>
    Options,

    /// <summary>
    /// The user chooses an option.
    /// </summary>
    Choose,

    /// <summary>
    /// The scripts send a command to the game; the run goes on at once.
    /// </summary>
    Command,

    /// <summary>
    /// The user answers for a function of the game.
    /// </summary>
    Function,

    /// <summary>
    /// The user sets a variable.
    /// </summary>
    SetVariable,

    /// <summary>
    /// A node group or a line group picks one of its candidates.
    /// </summary>
    Saliency,

    /// <summary>
    /// The dialogue is over.
    /// </summary>
    End,

    /// <summary>
    /// The dialogue failed, and is over.
    /// </summary>
    Error,
}

/// <summary>
/// An option the played dialogue offers.
/// </summary>
/// <param name="Index">What <see cref="DialoguePlayerService.Choose"/> takes to choose it.</param>
/// <param name="LineId">The id of its line.</param>
/// <param name="Text">Its text in the language of the player.</param>
/// <param name="IsAvailable">False when its condition fails: it shows, and cannot be chosen.</param>
public sealed record PlayerOption(int Index, string LineId, string Text, bool IsAvailable);

/// <summary>
/// A call of a function of the game that the played dialogue needs the result of.
/// </summary>
/// <param name="Function">The name of the function.</param>
/// <param name="Call">The call as a script writes it, with its values: <c>has_item("oil")</c>.</param>
/// <param name="Arguments">The values of the call.</param>
/// <param name="ReturnType">The type of the result: <c>bool</c>, <c>number</c> or <c>string</c>.</param>
public sealed record PlayerQuestion(string Function, string Call, IReadOnlyList<object> Arguments, string ReturnType);

/// <summary>
/// What the user answered for a call of a function of the game.
/// </summary>
/// <param name="Call">The call as a script writes it, with its values.</param>
/// <param name="Value">The result: a boolean, a number or a text.</param>
/// <param name="ReturnType">The type of the result.</param>
public sealed record PlayerAnswer(string Call, object Value, string ReturnType);

/// <summary>
/// A step of a played dialogue: what the user did or what the dialogue said, and the state after it.
/// </summary>
public sealed record PlayerStep
{
    /// <summary>
    /// What the step is.
    /// </summary>
    public required PlayerStepKind Kind { get; init; }

    /// <summary>
    /// The name, in the program, of the node the dialogue is in; null before the first node.
    /// </summary>
    public string? Node { get; init; }

    /// <summary>
    /// The file, as the compiler names it, of the line or of the options of the step; null when the step has no place.
    /// </summary>
    public string? File { get; init; }

    /// <summary>
    /// The line, from 1, of the line or of the first option of the step in <see cref="File"/>.
    /// </summary>
    public int? Line { get; init; }

    /// <summary>
    /// The step in words: the line with its speaker, the option chosen, the command, the title of the node, the answer,
    /// the variable and its value, the pick and what ruled the others out, the failure.
    /// </summary>
    public string Text { get; init; } = "";

    /// <summary>
    /// Who says the line, or null.
    /// </summary>
    public string? Speaker { get; init; }

    /// <summary>
    /// The options offered, for <see cref="PlayerStepKind.Options"/>.
    /// </summary>
    public IReadOnlyList<PlayerOption> Options { get; init; } = [];

    /// <summary>
    /// The pick, for <see cref="PlayerStepKind.Saliency"/>.
    /// </summary>
    public SaliencyPick? Pick { get; init; }

    /// <summary>
    /// Whether the last replay made the step, or made it differ from the run before.
    /// </summary>
    public bool Changed { get; init; }

    /// <summary>
    /// The variables of the scripts after the step, the smart ones aside: each with a boolean, a number or a text.
    /// </summary>
    public required IReadOnlyDictionary<string, object> Variables { get; init; }

    /// <summary>
    /// The index, among the recorded inputs of the run, of the one that gave the step.
    /// </summary>
    internal int Input { get; init; }

    /// <summary>
    /// Whether the run waits after the step for the user to go on, to choose, or for nothing more.
    /// </summary>
    internal bool IsStop => Kind is PlayerStepKind.Line or PlayerStepKind.Options or PlayerStepKind.End or PlayerStepKind.Error;

    /// <summary>
    /// What two runs compare to tell whether a step is the same in both.
    /// </summary>
    internal string Signature => $"{Kind}|{Node}|{Text}|{string.Join('|', Options.Select(o => o.Text))}";
}
