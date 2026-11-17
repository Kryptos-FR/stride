namespace ColorRise.Skein.Assets.Text;

/// <summary>
/// How the terms of a condition are joined.
/// </summary>
public enum YarnJoiner
{
    /// <summary>
    /// Every term holds: <c>and</c>.
    /// </summary>
    And,

    /// <summary>
    /// One term holds: <c>or</c>.
    /// </summary>
    Or,
}

/// <summary>
/// What a term says of its subject.
/// </summary>
public enum YarnComparison
{
    /// <summary>
    /// The subject alone: <c>$met</c>.
    /// </summary>
    IsTrue,

    /// <summary>
    /// <c>not $met</c>.
    /// </summary>
    IsFalse,

    /// <summary>
    /// <c>==</c>.
    /// </summary>
    Equal,

    /// <summary>
    /// <c>!=</c>.
    /// </summary>
    NotEqual,

    /// <summary>
    /// <c>&lt;</c>.
    /// </summary>
    Less,

    /// <summary>
    /// <c>&lt;=</c>.
    /// </summary>
    LessOrEqual,

    /// <summary>
    /// <c>&gt;</c>.
    /// </summary>
    Greater,

    /// <summary>
    /// <c>&gt;=</c>.
    /// </summary>
    GreaterOrEqual,
}

/// <summary>
/// One term of a condition: a variable or a call of a function, and what it is compared with.
/// </summary>
public sealed record YarnConditionTerm
{
    /// <summary>
    /// The variable, with its <c>$</c>, or the name of the function.
    /// </summary>
    public required string Subject { get; init; }

    /// <summary>
    /// The arguments of the function, as written; null for a variable.
    /// </summary>
    public IReadOnlyList<string>? Arguments { get; init; }

    /// <summary>
    /// What the term says of its subject.
    /// </summary>
    public YarnComparison Comparison { get; init; }

    /// <summary>
    /// What the subject is compared with, as written (<c>Act.Wreck</c>, <c>3</c>, <c>"Pier"</c>, <c>$price + 1</c>); null
    /// for <see cref="YarnComparison.IsTrue"/> and <see cref="YarnComparison.IsFalse"/>.
    /// </summary>
    public string? Value { get; init; }

    /// <summary>
    /// Whether the subject is a call of a function.
    /// </summary>
    public bool IsFunction => Arguments is not null;

    /// <summary>
    /// The subject as Yarn writes it: <c>$act</c>, <c>visited("Pier")</c>.
    /// </summary>
    public string SubjectText => Arguments is null ? Subject : $"{Subject}({string.Join(", ", Arguments.Select(a => a.Trim()))})";

    /// <summary>
    /// Whether the term says something: a comparison needs its value.
    /// </summary>
    public bool IsComplete => !string.IsNullOrWhiteSpace(Subject) && (Comparison is YarnComparison.IsTrue or YarnComparison.IsFalse || !string.IsNullOrWhiteSpace(Value));

    /// <summary>
    /// The operator Yarn writes for a comparison: <c>==</c>, <c>&lt;=</c>; empty for <see cref="YarnComparison.IsTrue"/>,
    /// <c>not</c> for <see cref="YarnComparison.IsFalse"/>.
    /// </summary>
    public static string OperatorOf(YarnComparison comparison) => comparison switch
    {
        YarnComparison.IsTrue => "",
        YarnComparison.IsFalse => "not",
        YarnComparison.Equal => "==",
        YarnComparison.NotEqual => "!=",
        YarnComparison.Less => "<",
        YarnComparison.LessOrEqual => "<=",
        YarnComparison.Greater => ">",
        YarnComparison.GreaterOrEqual => ">=",
        _ => throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null),
    };

    /// <summary>
    /// The term as Yarn writes it.
    /// </summary>
    public string Write() => Comparison switch
    {
        YarnComparison.IsTrue => SubjectText,
        YarnComparison.IsFalse => $"not {SubjectText}",
        _ => $"{SubjectText} {OperatorOf(Comparison)} {Value?.Trim()}",
    };
}

/// <summary>
/// A condition of Yarn as rows: terms joined by one joiner, as most conditions are written. Conditions read with Yarn
/// Spinner's own parser, so that what the rows show is what the compiler reads.
/// </summary>
public sealed record YarnCondition
{
    /// <summary>
    /// The terms, in the order of the text.
    /// </summary>
    public IReadOnlyList<YarnConditionTerm> Terms { get; init; } = [];

    /// <summary>
    /// How the terms are joined.
    /// </summary>
    public YarnJoiner Joiner { get; init; }

    /// <summary>
    /// Reads a condition as rows.
    /// </summary>
    /// <param name="expression">The condition, as written after <c>if</c> or <c>when:</c>.</param>
    /// <returns>
    /// The condition; one without terms for an empty text; null for a text that is not terms joined by one joiner (Yarn
    /// gives <c>and</c> and <c>or</c> the same precedence, so a mix stays text), and for one that is not a condition.
    /// </returns>
    public static YarnCondition? Read(string expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        return string.IsNullOrWhiteSpace(expression) ? new YarnCondition() : YarnExpressionReader.ReadCondition(expression);
    }

    /// <summary>
    /// The condition as Yarn writes it, with <c>==</c>, <c>!=</c>, <c>and</c>, <c>or</c> and <c>not</c>; values and arguments as
    /// they were written. A term that is not complete is left out.
    /// </summary>
    public string Write() => string.Join(Joiner == YarnJoiner.And ? " and " : " or ", Terms.Where(t => t.IsComplete).Select(t => t.Write()));
}
