using ColorRise.Skein.Assets.Compilation;
using Stride.Core.Presentation.Commands;
using Stride.Core.Presentation.ViewModels;

namespace ColorRise.Skein.Editor.Player;

/// <summary>
/// A call of a function of the game answered by hand in the dialogue player: the question the run waits at, or an answer
/// given before, which the user can change from the cursor on.
/// </summary>
public sealed class ManualFunctionViewModel : ViewModelBase
{
    private readonly Func<object, string?> apply;
    private string answer;
    private string error = "";

    /// <param name="serviceProvider">The services of the session.</param>
    /// <param name="call">The call as a script writes it, with its values.</param>
    /// <param name="returnType">The type the function gives.</param>
    /// <param name="value">The answer given before; null for the question the run waits at.</param>
    /// <param name="apply">Gives the answer to the run; gives back why it could not, or null.</param>
    public ManualFunctionViewModel(IViewModelServiceProvider serviceProvider, string call, string returnType, object? value, Func<object, string?> apply)
        : base(serviceProvider)
    {
        this.apply = apply;
        Call = call;
        ReturnType = returnType;
        IsPending = value is null;
        answer = PlayerValues.Show(value);
        ApplyCommand = new AnonymousCommand(serviceProvider, () => Give(Answer));
        AnswerTrueCommand = new AnonymousCommand(serviceProvider, () => Give("true")) { IsEnabled = IsBool };
        AnswerFalseCommand = new AnonymousCommand(serviceProvider, () => Give("false")) { IsEnabled = IsBool };
    }

    /// <summary>
    /// The call as a script writes it, with its values: <c>has_item("oil")</c>.
    /// </summary>
    public string Call { get; }

    /// <summary>
    /// The type the function gives: <c>bool</c>, <c>number</c> or <c>string</c>.
    /// </summary>
    public string ReturnType { get; }

    /// <summary>
    /// Whether the function gives true or false: the panel shows two buttons instead of a box.
    /// </summary>
    public bool IsBool => ReturnType == YarnFunctionDeclaration.Bool;

    /// <summary>
    /// Whether the run waits for this answer.
    /// </summary>
    public bool IsPending { get; }

    /// <summary>
    /// The answer as typed; <see cref="ApplyCommand"/> gives it to the run.
    /// </summary>
    public string Answer { get => answer; set => SetValue(ref answer, value); }

    /// <summary>
    /// Why the last answer was not taken, or empty.
    /// </summary>
    public string Error { get => error; private set => SetValue(ref error, value); }

    /// <summary>
    /// Gives <see cref="Answer"/> to the run.
    /// </summary>
    public ICommandBase ApplyCommand { get; }

    /// <summary>
    /// Answers true, for a function that gives true or false.
    /// </summary>
    public ICommandBase AnswerTrueCommand { get; }

    /// <summary>
    /// Answers false, for a function that gives true or false.
    /// </summary>
    public ICommandBase AnswerFalseCommand { get; }

    /// <summary>
    /// Gives an answer to the run: the question's goes on with the dialogue, a changed one holds from the cursor on.
    /// </summary>
    /// <param name="text">The answer as a script writes it.</param>
    /// <returns>Why the answer was not taken, or null.</returns>
    public string? Give(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var problem = PlayerValues.Parse(text, ReturnType) is { } value ? apply(value) : $"{Call} gives {PlayerValues.Describe(ReturnType)}.";
        Error = problem ?? "";
        return problem;
    }
}
