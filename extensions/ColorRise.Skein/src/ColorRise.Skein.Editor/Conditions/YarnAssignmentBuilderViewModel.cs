using ColorRise.Skein.Assets.Compilation;
using ColorRise.Skein.Assets.Text;
using Stride.Core.Presentation.ViewModels;

namespace ColorRise.Skein.Editor.Conditions;

/// <summary>
/// The builder of a <c>&lt;&lt;set&gt;&gt;</c> command: a variable, how it changes, and a value.
/// </summary>
public sealed class YarnAssignmentBuilderViewModel : ViewModelBase
{
    /// <summary>
    /// Makes a builder.
    /// </summary>
    /// <param name="knowledge">What the project offers.</param>
    /// <param name="assignment">The command as it is; null for a new one.</param>
    public YarnAssignmentBuilderViewModel(YarnConditionKnowledge knowledge, YarnAssignment? assignment)
    {
        Knowledge = knowledge;
        Variable = assignment?.Variable ?? Variables.FirstOrDefault() ?? "";
        Operator = assignment?.Operator ?? YarnAssignmentOperator.Set;
        Value = assignment?.Value ?? "";
    }

    /// <summary>
    /// What the project offers.
    /// </summary>
    public YarnConditionKnowledge Knowledge { get; }

    /// <summary>
    /// The variable, with its <c>$</c>.
    /// </summary>
    public string Variable
    {
        get;
        set
        {
            if (!SetValue(ref field, value?.Trim() ?? ""))
                return;

            if (!Operators.Contains(Operator))
            {
                Operator = YarnAssignmentOperator.Set;
            }

            OnPropertyChanged(nameof(Type), nameof(Operators), nameof(Values), nameof(Command));
        }
    } = "";

    /// <summary>
    /// The variables a command can set: a smart variable is computed, never set.
    /// </summary>
    public IReadOnlyList<string> Variables => [.. Knowledge.Variables.Where(v => !v.IsSmart).Select(v => v.Name)];

    /// <summary>
    /// The type of the variable, or null when the project does not know it.
    /// </summary>
    public string? Type => Knowledge.TypeOf(Variable);

    /// <summary>
    /// How the variable changes.
    /// </summary>
    public YarnAssignmentOperator Operator
    {
        get;
        set
        {
            if (SetValue(ref field, value))
            {
                OnPropertyChanged(nameof(Command));
            }
        }
    }

    /// <summary>
    /// How a variable of the type can change: a number by arithmetic too.
    /// </summary>
    public IReadOnlyList<YarnAssignmentOperator> Operators => Type is YarnFunctionDeclaration.Number or null ? Enum.GetValues<YarnAssignmentOperator>() : [YarnAssignmentOperator.Set];

    /// <summary>
    /// The value, as typed.
    /// </summary>
    public string Value
    {
        get;
        set
        {
            if (SetValue(ref field, value ?? ""))
            {
                OnPropertyChanged(nameof(Command));
            }
        }
    } = "";

    /// <summary>
    /// The values to offer: the cases of an enum, <c>true</c> and <c>false</c>, the variables of the same type.
    /// </summary>
    public IReadOnlyList<string> Values => Knowledge.ValuesOf(Type);

    /// <summary>
    /// The command the builder writes, without <c>&lt;&lt;</c> and <c>&gt;&gt;</c>; null until it has a variable and a value.
    /// </summary>
    public string? Command
    {
        get
        {
            var value = Knowledge.FitValue(Type, Value);
            return Variable.StartsWith('$') && Variable.Length > 1 && value.Length > 0
                ? new YarnAssignment { Variable = Variable, Operator = Operator, Value = value }.Write()
                : null;
        }
    }

    /// <summary>
    /// The text of an operator in the builder.
    /// </summary>
    public static string LabelOf(YarnAssignmentOperator op) => op == YarnAssignmentOperator.Set ? "to" : YarnAssignment.OperatorOf(op);
}
