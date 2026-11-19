using Yarn;

namespace ColorRise.Skein;

/// <summary>
/// What a running dialogue says, each call awaited before the dialogue goes on, in Yarn Spinner's types.
/// </summary>
internal interface IYarnDialogueEvents
{
    ValueTask NodeStartAsync(string node, CancellationToken cancellation);

    ValueTask NodeCompleteAsync(string node, CancellationToken cancellation);

    ValueTask PrepareForLinesAsync(IReadOnlyList<string> lineIds, CancellationToken cancellation);

    ValueTask LineAsync(Line line, CancellationToken cancellation);

    /// <returns>The id of the chosen option, or <see cref="Dialogue.NoOptionSelected"/>.</returns>
    ValueTask<int> OptionsAsync(OptionSet options, CancellationToken cancellation);

    ValueTask CommandAsync(string text, CancellationToken cancellation);
}

/// <summary>
/// The functions a dialogue calls beside Yarn Spinner's standard library.
/// </summary>
internal interface IYarnFunctionSource
{
    /// <summary>
    /// Whether this source answers the function. A function it does not answer goes to Yarn Spinner's standard library.
    /// </summary>
    bool CanCall(string name);

    /// <summary>
    /// Calls a function this source answers.
    /// </summary>
    /// <returns>A boolean, a number (a float) or a string.</returns>
    ValueTask<IConvertible> InvokeAsync(string name, IConvertible[] arguments, CancellationToken cancellation);
}

/// <summary>
/// Answers a Yarn Spinner <see cref="Dialogue"/>: what the dialogue says goes to the events, and the functions its
/// scripts call to the function source, then to the functions the dialogue leaves to its engine (<c>visited</c>,
/// <c>visited_count</c> and <c>has_any_content</c>), then to Yarn Spinner's standard library.
/// </summary>
internal sealed class YarnResponder : Yarn.IDialogueResponder
{
    private readonly IYarnDialogueEvents? events;
    private readonly IYarnFunctionSource functions;

    /// <param name="storage">Where the dialogue reads and writes its variables.</param>
    /// <param name="program">The program the dialogue runs.</param>
    /// <param name="events">Where what the dialogue says goes; null for a dialogue that only computes smart variables.</param>
    /// <param name="functions">The functions of the game, and the ones that take the place of Yarn Spinner's.</param>
    public YarnResponder(IVariableStorage storage, Program program, IYarnDialogueEvents? events, IYarnFunctionSource functions)
    {
        this.events = events;
        this.functions = functions;
        Dialogue = new Dialogue(storage) { Responder = this, Program = program };
    }

    public Dialogue Dialogue { get; }

    public ValueTask HandleNodeStart(string node, CancellationToken token) => Events.NodeStartAsync(node, token);

    public ValueTask HandleNodeComplete(string node, CancellationToken token) => Events.NodeCompleteAsync(node, token);

    public ValueTask PrepareForLines(List<string> lineIDs, CancellationToken token) => Events.PrepareForLinesAsync(lineIDs, token);

    public ValueTask HandleLine(Line line, CancellationToken token) => Events.LineAsync(line, token);

    public ValueTask<int> HandleOptions(OptionSet options, CancellationToken token) => Events.OptionsAsync(options, token);

    public ValueTask HandleCommand(Command command, CancellationToken token) => Events.CommandAsync(command.Text, token);

    // The session tells the end itself, once per run, a stop included
    public ValueTask HandleDialogueComplete() => ValueTask.CompletedTask;

    public bool CanCallFunction(string functionName)
        => functions.CanCall(functionName) || functionName is StandardLibrary.EngineProvidedFunctionNames.Visited or StandardLibrary.EngineProvidedFunctionNames.VisitedCount or "has_any_content";

    public ValueTask<IConvertible> InvokeFunction(string functionName, IConvertible[] parameters, CancellationToken token)
    {
        if (functions.CanCall(functionName))
            return functions.InvokeAsync(functionName, parameters, token);

        var node = Convert.ToString(parameters[0], System.Globalization.CultureInfo.InvariantCulture) ?? "";
        IConvertible result = functionName switch
        {
            StandardLibrary.EngineProvidedFunctionNames.Visited => Dialogue.IsNodeVisited(node),
            StandardLibrary.EngineProvidedFunctionNames.VisitedCount => Dialogue.GetNodeVisitCount(node),
            "has_any_content" => Dialogue.HasAnyContent(node),
            _ => throw new ArgumentException($"The dialogue has no function named '{functionName}'.", nameof(functionName)),
        };
        return new ValueTask<IConvertible>(result);
    }

    private IYarnDialogueEvents Events => events ?? throw new InvalidOperationException("This dialogue only computes smart variables: it never runs.");
}
