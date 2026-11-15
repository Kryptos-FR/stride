using ColorRise.Saga.Content;
using ColorRise.Saga.Expressions;
using Stride.Core.Presentation.ViewModels;

namespace ColorRise.Saga.Editor.Player;

/// <summary>
/// A function the story declares, answered by hand in the story player: whatever the arguments, it returns the answer, a
/// literal of its return type. Each answer given is a step of the run.
/// </summary>
public sealed class ManualFunctionViewModel : ViewModelBase
{
    private readonly SagaTypeKind returnType;
    private readonly Func<SagaValue, string?> apply;
    private string answer;
    private string error = string.Empty;

    /// <param name="serviceProvider">The services of the session.</param>
    /// <param name="declaration">The declaration of the function.</param>
    /// <param name="apply">Gives the answer to the story; returns why it couldn't, or null.</param>
    public ManualFunctionViewModel(IViewModelServiceProvider serviceProvider, FunctionDeclaration declaration, Func<SagaValue, string?> apply)
        : base(serviceProvider)
    {
        Name = declaration.Name;
        returnType = declaration.ReturnType;
        this.apply = apply ?? throw new ArgumentNullException(nameof(apply));
        Signature = $"{Name}({string.Join(", ", declaration.Parameters)}) → {returnType}";
        Value = SagaLiterals.DefaultOf(returnType);
        answer = SagaLiterals.Format(Value);
    }

    public string Name { get; }

    /// <summary>
    /// The name, the types of the parameters and the return type.
    /// </summary>
    public string Signature { get; }

    /// <summary>
    /// The answer as typed; a text that is not a literal of the return type leaves <see cref="Value"/> as it was.
    /// </summary>
    public string Answer
    {
        get => answer;
        set
        {
            if (!SetValue(ref answer, value))
                return;
            var message = SagaLiterals.TryParse(value, returnType, out var parsed);
            if (message is null)
            {
                Value = parsed;
                message = apply(parsed);
            }
            Error = message ?? string.Empty;
        }
    }

    /// <summary>
    /// Why the answer is not a literal of the return type, or empty.
    /// </summary>
    public string Error { get => error; private set => SetValue(ref error, value); }

    /// <summary>
    /// What the function returns.
    /// </summary>
    public SagaValue Value { get; private set; }

    /// <summary>
    /// Shows the answer the run gives at its cursor.
    /// </summary>
    internal void Update(SagaValue current)
    {
        if (Error.Length > 0)
            return;
        Value = current;
        SetValue(ref answer, SagaLiterals.Format(current), nameof(Answer));
    }
}
