using ColorRise.Skein.Assets.Compilation;
using ColorRise.Skein.Assets.Text;
using Stride.Core.Presentation.Collections;
using Stride.Core.Presentation.ViewModels;

namespace ColorRise.Skein.Editor.Conditions;

/// <summary>
/// A row of the condition builder: a variable or a function, what it is compared with, and the value.
/// </summary>
public sealed class YarnConditionTermViewModel : ViewModelBase
{
    private static readonly YarnComparison[] AllComparisons = Enum.GetValues<YarnComparison>();

    internal YarnConditionTermViewModel(YarnConditionKnowledge knowledge, YarnConditionTerm term, Action changed)
    {
        Knowledge = knowledge;
        Changed = changed;
        Subject = term.Subject;
        if (term.Arguments is { } arguments)
        {
            for (var i = 0; i < Arguments.Count && i < arguments.Count; i++)
            {
                Arguments[i].Text = arguments[i];
            }

            // A function the project does not know keeps the arguments it was written with
            for (var i = Arguments.Count; i < arguments.Count; i++)
            {
                Arguments.Add(new YarnArgumentViewModel(this, i) { Text = arguments[i] });
            }
        }

        Comparison = term.Comparison;
        Value = term.Value ?? "";
    }

    /// <summary>
    /// What the project offers.
    /// </summary>
    public YarnConditionKnowledge Knowledge { get; }

    private Action Changed { get; }

    /// <summary>
    /// The variable, with its <c>$</c>, or the name of the function.
    /// </summary>
    public string Subject
    {
        get;
        set
        {
            value = value?.Trim() ?? "";
            var type = Type;
            if (!SetValue(ref field, value))
                return;

            OnSubjectChanged(type != Type);
        }
    } = "";

    /// <summary>
    /// What a row can be about: the variables and the functions of the project.
    /// </summary>
    public IReadOnlyList<string> Subjects => Knowledge.Subjects;

    /// <summary>
    /// Whether the subject is a function: a name without <c>$</c>.
    /// </summary>
    public bool IsFunction => Subject.Length > 0 && !Subject.StartsWith('$');

    /// <summary>
    /// The type of the subject: <c>number</c>, <c>string</c>, <c>bool</c> or an enum; null when the project does not know it.
    /// </summary>
    public string? Type => Knowledge.TypeOf(Subject);

    /// <summary>
    /// The arguments of the function.
    /// </summary>
    public ObservableList<YarnArgumentViewModel> Arguments { get; } = [];

    /// <summary>
    /// What the subject is compared with.
    /// </summary>
    public YarnComparison Comparison
    {
        get;
        set
        {
            if (SetValue(ref field, value))
            {
                OnPropertyChanged(nameof(HasValue));
                Changed();
            }
        }
    }

    /// <summary>
    /// The comparisons that fit the type of the subject.
    /// </summary>
    public IReadOnlyList<YarnComparison> Comparisons => Type switch
    {
        YarnFunctionDeclaration.Bool => [YarnComparison.IsTrue, YarnComparison.IsFalse, YarnComparison.Equal, YarnComparison.NotEqual],
        YarnFunctionDeclaration.Number => [YarnComparison.Equal, YarnComparison.NotEqual, YarnComparison.Less, YarnComparison.LessOrEqual, YarnComparison.Greater, YarnComparison.GreaterOrEqual],
        null => AllComparisons,
        _ => [YarnComparison.Equal, YarnComparison.NotEqual],
    };

    /// <summary>
    /// Whether the comparison takes a value.
    /// </summary>
    public bool HasValue => Comparison is not (YarnComparison.IsTrue or YarnComparison.IsFalse);

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
                Changed();
            }
        }
    } = "";

    /// <summary>
    /// The values to offer: the cases of an enum, <c>true</c> and <c>false</c>, the other variables of the same type.
    /// </summary>
    public IReadOnlyList<string> Values => [.. Knowledge.ValuesOf(Type).Where(v => v != Subject)];

    /// <summary>
    /// Whether the row says something: a subject, and the value its comparison needs.
    /// </summary>
    public bool IsComplete => ToTerm().IsComplete;

    /// <summary>
    /// The row as a term of a condition, with the values made fit their types.
    /// </summary>
    public YarnConditionTerm ToTerm() => new()
    {
        Subject = Subject,
        Arguments = IsFunction ? [.. Arguments.Select(a => Knowledge.FitValue(a.Type, a.Text))] : null,
        Comparison = Comparison,
        Value = HasValue ? Knowledge.FitValue(Type, Value) : null,
    };

    /// <summary>
    /// The text of a comparison in the builder.
    /// </summary>
    public static string LabelOf(YarnComparison comparison) => comparison switch
    {
        YarnComparison.IsTrue => "is true",
        YarnComparison.IsFalse => "is false",
        YarnComparison.Equal => "is",
        YarnComparison.NotEqual => "is not",
        YarnComparison.Less => "<",
        YarnComparison.LessOrEqual => "≤",
        YarnComparison.Greater => ">",
        YarnComparison.GreaterOrEqual => "≥",
        _ => comparison.ToString(),
    };

    internal void OnArgumentChanged() => Changed();

    private void OnSubjectChanged(bool newType)
    {
        // A function gets a box for each of its parameters; what was typed in the first ones stays
        var count = IsFunction ? Knowledge.Function(Subject) is { } function ? function.ParameterTypes.Count : Arguments.Count : 0;
        while (Arguments.Count > count)
        {
            Arguments.RemoveAt(Arguments.Count - 1);
        }

        while (Arguments.Count < count)
        {
            Arguments.Add(new YarnArgumentViewModel(this, Arguments.Count));
        }

        foreach (var argument in Arguments)
        {
            argument.OnFunctionChanged();
        }

        // The lists change first: a box that loses its selection with its list takes the comparison again after
        OnPropertyChanged(nameof(IsFunction), nameof(Type), nameof(Comparisons), nameof(Values));

        // A value of another type means nothing for the new subject
        if (newType || !Comparisons.Contains(Comparison))
        {
            Comparison = Comparisons[0];
            Value = "";
        }

        OnPropertyChanged(nameof(Comparison));
        Changed();
    }
}

/// <summary>
/// An argument of the function of a row of the condition builder.
/// </summary>
public sealed class YarnArgumentViewModel : ViewModelBase
{
    internal YarnArgumentViewModel(YarnConditionTermViewModel term, int index)
    {
        Term = term;
        Index = index;
    }

    /// <summary>
    /// The row of the argument.
    /// </summary>
    public YarnConditionTermViewModel Term { get; }

    /// <summary>
    /// The rank of the argument, from 0.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// The type of the parameter, or null.
    /// </summary>
    public string? Type => Term.Knowledge.ArgumentTypeOf(Term.Subject, Index);

    /// <summary>
    /// The argument, as typed.
    /// </summary>
    public string Text
    {
        get;
        set
        {
            if (SetValue(ref field, value ?? ""))
            {
                Term.OnArgumentChanged();
            }
        }
    } = "";

    /// <summary>
    /// The values to offer: the nodes for <c>visited</c>, else those of the parameter's type.
    /// </summary>
    public IReadOnlyList<string> Values => Term.Knowledge.ArgumentValuesOf(Term.Subject, Index);

    internal void OnFunctionChanged() => OnPropertyChanged(nameof(Type), nameof(Values));
}
