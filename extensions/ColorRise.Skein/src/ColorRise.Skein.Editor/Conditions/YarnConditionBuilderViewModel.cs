using ColorRise.Skein.Assets.Text;
using Stride.Core.Presentation.Collections;
using Stride.Core.Presentation.ViewModels;

namespace ColorRise.Skein.Editor.Conditions;

/// <summary>
/// The condition builder: rows of variables and functions, each compared with a value, joined by <c>and</c> or
/// <c>or</c>. It writes a Yarn expression and reads one back; an expression that is not rows is edited as text.
/// </summary>
public sealed class YarnConditionBuilderViewModel : ViewModelBase
{
    private const string Always = "always";
    private const string Once = "once";
    private const string OnceIf = "once if ";

    /// <summary>
    /// Makes a builder.
    /// </summary>
    /// <param name="knowledge">What the project offers.</param>
    /// <param name="condition">The condition, as written; null or empty for none.</param>
    /// <param name="once">Whether the statement runs one time only.</param>
    /// <param name="allowsOnce">Whether the builder shows the "once" choice.</param>
    public YarnConditionBuilderViewModel(YarnConditionKnowledge knowledge, string? condition, bool once = false, bool allowsOnce = false)
    {
        Knowledge = knowledge;
        AllowsOnce = allowsOnce;
        IsOnce = once;
        Load(condition?.Trim() ?? "");
        IsChanged = false;
    }

    /// <summary>
    /// What the project offers.
    /// </summary>
    public YarnConditionKnowledge Knowledge { get; }

    /// <summary>
    /// The rows, in the order of the text.
    /// </summary>
    public ObservableList<YarnConditionTermViewModel> Terms { get; } = [];

    /// <summary>
    /// How the rows are joined.
    /// </summary>
    public YarnJoiner Joiner
    {
        get;
        set
        {
            if (SetValue(ref field, value))
            {
                OnChanged();
            }
        }
    }

    /// <summary>
    /// The joiners to pick from.
    /// </summary>
    public IReadOnlyList<YarnJoiner> Joiners { get; } = [YarnJoiner.And, YarnJoiner.Or];

    /// <summary>
    /// Whether the condition is edited as text: one that is not rows always is.
    /// </summary>
    public bool IsText
    {
        get;
        set
        {
            if (value == field)
                return;

            if (value)
            {
                Text = RowsText();
            }
            else if (YarnCondition.Read(Text) is { } condition)
            {
                Fill(condition);
            }
            else
            {
                // The box that asked for rows shows text again
                Problem = "This condition is not rows of one joiner: it stays text.";
                OnPropertyChanged(nameof(IsText));
                return;
            }

            Problem = null;
            SetValue(ref field, value);
            OnChanged();
        }
    }

    /// <summary>
    /// The condition as text, when it is edited so.
    /// </summary>
    public string Text
    {
        get;
        set
        {
            if (SetValue(ref field, value ?? ""))
            {
                OnChanged();
            }
        }
    } = "";

    /// <summary>
    /// Whether the statement runs one time only.
    /// </summary>
    public bool IsOnce
    {
        get;
        set
        {
            if (SetValue(ref field, value))
            {
                OnChanged();
            }
        }
    }

    /// <summary>
    /// Whether the builder shows the "once" choice: for a line, an option and a <c>when:</c> header.
    /// </summary>
    public bool AllowsOnce { get; }

    /// <summary>
    /// Whether the builder shows the "always" choice: for a <c>when:</c> header.
    /// </summary>
    public bool AllowsAlways { get; private init; }

    /// <summary>
    /// Whether the node of a node group can always run: <c>when: always</c>. The rows are not written then.
    /// </summary>
    public bool IsAlways
    {
        get;
        set
        {
            if (SetValue(ref field, value))
            {
                OnChanged();
            }
        }
    }

    /// <summary>
    /// Whether the user changed something since the builder was made: one that only looked writes nothing, not even the
    /// usual spelling of its operators.
    /// </summary>
    public bool IsChanged { get; private set; }

    /// <summary>
    /// Why the last thing asked of the builder was not done, or null.
    /// </summary>
    public string? Problem { get; private set => SetValue(ref field, value); }

    /// <summary>
    /// The condition the builder writes, without <c>if</c> or <c>once</c>; null for none.
    /// </summary>
    public string? Condition
    {
        get
        {
            var written = IsText ? Text.Trim() : RowsText();
            return written.Length == 0 ? null : written;
        }
    }

    /// <summary>
    /// The value of a <c>when:</c> header: <c>always</c>, <c>once</c>, <c>once if ...</c> or the condition; null for none.
    /// </summary>
    public string? HeaderText => IsAlways ? Always : (IsOnce, Condition) switch
    {
        (true, null) => Once,
        (true, { } condition) => OnceIf + condition,
        (false, var condition) => condition,
    };

    /// <summary>
    /// What the builder writes, as it shows in the text: <c>&lt;&lt;if ...&gt;&gt;</c> after a line, <c>when: ...</c> for a header.
    /// </summary>
    public string Preview
    {
        get
        {
            if (AllowsAlways)
                return HeaderText is { } header ? $"when: {header}" : "no when: header";

            return (IsOnce, Condition) switch
            {
                (true, null) => "<<once>>",
                (true, { } condition) => $"<<once if {condition}>>",
                (false, { } condition) => $"<<if {condition}>>",
                _ => "no condition",
            };
        }
    }

    /// <summary>
    /// Makes the builder of a <c>when:</c> header.
    /// </summary>
    /// <param name="knowledge">What the project offers.</param>
    /// <param name="header">The value of the header: <c>always</c>, <c>once</c>, <c>once if ...</c> or a condition.</param>
    public static YarnConditionBuilderViewModel ForHeader(YarnConditionKnowledge knowledge, string? header)
    {
        var text = header?.Trim() ?? "";
        var once = text == Once || text.StartsWith(OnceIf, StringComparison.Ordinal);
        var condition = text == Always || text == Once ? null : once ? text[OnceIf.Length..] : text;
        var builder = new YarnConditionBuilderViewModel(knowledge, condition, once, allowsOnce: true) { AllowsAlways = true, IsAlways = text == Always };
        builder.IsChanged = false;
        return builder;
    }

    /// <summary>
    /// Adds a row: the first variable of the project, or an empty one.
    /// </summary>
    public YarnConditionTermViewModel AddTerm()
    {
        var term = new YarnConditionTermViewModel(Knowledge, new YarnConditionTerm { Subject = Knowledge.Variables.FirstOrDefault()?.Name ?? "" }, OnChanged);
        Terms.Add(term);
        OnChanged();
        return term;
    }

    /// <summary>
    /// Removes a row.
    /// </summary>
    public void RemoveTerm(YarnConditionTermViewModel term)
    {
        if (Terms.Remove(term))
        {
            OnChanged();
        }
    }

    private void Load(string condition)
    {
        if (YarnCondition.Read(condition) is { } rows)
        {
            Fill(rows);
        }
        else
        {
            // Text shows the rows when it opens: the condition goes in after
            IsText = true;
            Text = condition;
        }
    }

    private void Fill(YarnCondition condition)
    {
        Terms.Clear();
        foreach (var term in condition.Terms)
        {
            Terms.Add(new YarnConditionTermViewModel(Knowledge, term, OnChanged));
        }

        Joiner = condition.Joiner;
    }

    private string RowsText() => new YarnCondition { Terms = [.. Terms.Select(t => t.ToTerm())], Joiner = Joiner }.Write();

    private void OnChanged()
    {
        IsChanged = true;
        OnPropertyChanged(nameof(Condition), nameof(HeaderText), nameof(Preview));
    }
}
