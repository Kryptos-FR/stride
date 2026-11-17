namespace ColorRise.Skein.Assets.Text;

/// <summary>
/// How a <c>&lt;&lt;set&gt;&gt;</c> changes its variable.
/// </summary>
public enum YarnAssignmentOperator
{
    /// <summary>
    /// <c>to</c>, or <c>=</c>: the variable takes the value.
    /// </summary>
    Set,

    /// <summary>
    /// <c>+=</c>.
    /// </summary>
    Add,

    /// <summary>
    /// <c>-=</c>.
    /// </summary>
    Subtract,

    /// <summary>
    /// <c>*=</c>.
    /// </summary>
    Multiply,

    /// <summary>
    /// <c>/=</c>.
    /// </summary>
    Divide,

    /// <summary>
    /// <c>%=</c>.
    /// </summary>
    Remainder,
}

/// <summary>
/// A <c>&lt;&lt;set&gt;&gt;</c> command as a row: a variable, how it changes, and a value.
/// </summary>
public sealed record YarnAssignment
{
    /// <summary>
    /// The variable, with its <c>$</c>.
    /// </summary>
    public required string Variable { get; init; }

    /// <summary>
    /// How the variable changes.
    /// </summary>
    public YarnAssignmentOperator Operator { get; init; }

    /// <summary>
    /// The value, as written: <c>true</c>, <c>Act.Wreck</c>, <c>$coins - 3</c>.
    /// </summary>
    public required string Value { get; init; }

    /// <summary>
    /// Reads a command as an assignment.
    /// </summary>
    /// <param name="command">What is between <c>&lt;&lt;</c> and <c>&gt;&gt;</c>: <c>set $met to true</c>.</param>
    /// <returns>The assignment, or null for a command that is not a <c>set</c> Yarn Spinner reads.</returns>
    public static YarnAssignment? Read(string command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return YarnExpressionReader.ReadAssignment(command);
    }

    /// <summary>
    /// The operator Yarn writes: <c>to</c>, <c>+=</c>...
    /// </summary>
    public static string OperatorOf(YarnAssignmentOperator op) => op switch
    {
        YarnAssignmentOperator.Set => "to",
        YarnAssignmentOperator.Add => "+=",
        YarnAssignmentOperator.Subtract => "-=",
        YarnAssignmentOperator.Multiply => "*=",
        YarnAssignmentOperator.Divide => "/=",
        YarnAssignmentOperator.Remainder => "%=",
        _ => throw new ArgumentOutOfRangeException(nameof(op), op, null),
    };

    /// <summary>
    /// The command as Yarn writes it, without <c>&lt;&lt;</c> and <c>&gt;&gt;</c>: <c>set $coins += 2</c>.
    /// </summary>
    public string Write() => $"set {Variable.Trim()} {OperatorOf(Operator)} {Value.Trim()}";
}
