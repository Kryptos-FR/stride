namespace ColorRise.Saga.Expressions;

/// <summary>
/// The types of the expression language.
/// </summary>
public enum SagaTypeKind
{
    Bool,
    Int,
    Float,
    String,

    /// <summary>
    /// The state of a quest: <c>not_started</c>, <c>available</c>, <c>active</c>, <c>succeeded</c>, <c>failed</c>.
    /// </summary>
    QuestState,

    /// <summary>
    /// The state of a scenario: <c>pending</c>, <c>running</c>, <c>done</c>.
    /// </summary>
    ScenarioState,

    /// <summary>
    /// An outcome of one scenario, by name.
    /// </summary>
    Outcome,

    /// <summary>
    /// An act of the plot, by id.
    /// </summary>
    Act,
}

/// <summary>
/// The unary operators.
/// </summary>
public enum SagaUnaryOperator
{
    Not,
    Negate,
}

/// <summary>
/// The binary operators, by increasing precedence group: <c>||</c>; <c>&amp;&amp;</c>; <c>== !=</c>; <c>&lt; &lt;= &gt; &gt;=</c>; <c>+ -</c>.
/// </summary>
public enum SagaBinaryOperator
{
    Or,
    And,
    Equal,
    NotEqual,
    Less,
    LessOrEqual,
    Greater,
    GreaterOrEqual,
    Add,
    Subtract,
}
