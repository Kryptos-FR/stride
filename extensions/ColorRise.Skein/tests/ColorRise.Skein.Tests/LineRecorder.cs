using Yarn;

namespace ColorRise.Skein.Tests;

/// <summary>
/// Answers a bare Yarn Spinner dialogue in a test: it records the lines, runs no command, chooses no option, and calls
/// the functions of a registry.
/// </summary>
internal sealed class LineRecorder(YarnActionRegistry? functions = null) : Yarn.IDialogueResponder
{
    /// <summary>
    /// The ids of the lines, in order.
    /// </summary>
    public List<string> LineIds { get; } = [];

    public ValueTask HandleLine(Line line, CancellationToken token)
    {
        LineIds.Add(line.ID);
        return ValueTask.CompletedTask;
    }

    public ValueTask<int> HandleOptions(OptionSet options, CancellationToken token) => new(Dialogue.NoOptionSelected);

    public ValueTask HandleCommand(Command command, CancellationToken token) => ValueTask.CompletedTask;

    public ValueTask HandleNodeStart(string node, CancellationToken token) => ValueTask.CompletedTask;

    public ValueTask HandleNodeComplete(string node, CancellationToken token) => ValueTask.CompletedTask;

    public ValueTask HandleDialogueComplete() => ValueTask.CompletedTask;

    public ValueTask PrepareForLines(List<string> lineIDs, CancellationToken token) => ValueTask.CompletedTask;

    public bool CanCallFunction(string functionName) => functions?.HasFunction(functionName) == true;

    public ValueTask<IConvertible> InvokeFunction(string functionName, IConvertible[] parameters, CancellationToken token)
        => functions!.InvokeFunctionAsync(functionName, parameters, token);
}
