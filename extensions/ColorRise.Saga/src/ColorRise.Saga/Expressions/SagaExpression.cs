using Stride.Core;

namespace ColorRise.Saga.Expressions;

/// <summary>
/// A node of a parsed expression.
/// </summary>
[DataContract(Inherited = true)]
public abstract class SagaExpression
{
    /// <summary>
    /// Where the node starts in its source.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// How many characters of the source the node covers.
    /// </summary>
    public int Length { get; set; }
}

/// <summary>
/// A literal: <c>12</c>, <c>2.5</c>, <c>"sword"</c>, <c>true</c>.
/// </summary>
[DataContract("SagaLiteralExpression")]
public sealed class LiteralExpression : SagaExpression
{
    public SagaValue Value { get; set; }
}

/// <summary>
/// A name, dotted or not: a variable (<c>day</c>, <c>player.strength</c>), a state (<c>quest.mine.state</c>), or a symbol
/// (<c>failed</c>) that the type checker resolves from the other side of a comparison.
/// </summary>
[DataContract("SagaPathExpression")]
public sealed class PathExpression : SagaExpression
{
    public string Path { get; set; } = string.Empty;
}

/// <summary>
/// A call to a function the game provides, or to a built-in (<c>raised</c>, <c>visited</c>, <c>visits</c>).
/// </summary>
[DataContract("SagaCallExpression")]
public sealed class CallExpression : SagaExpression
{
    public string Function { get; set; } = string.Empty;

    public List<SagaExpression> Arguments { get; set; } = [];
}

/// <summary>
/// <c>!operand</c> or <c>-operand</c>.
/// </summary>
[DataContract("SagaUnaryExpression")]
public sealed class UnaryExpression : SagaExpression
{
    public SagaUnaryOperator Operator { get; set; }

    public SagaExpression Operand { get; set; } = null!;
}

/// <summary>
/// <c>left operator right</c>.
/// </summary>
[DataContract("SagaBinaryExpression")]
public sealed class BinaryExpression : SagaExpression
{
    public SagaBinaryOperator Operator { get; set; }

    public SagaExpression Left { get; set; } = null!;

    public SagaExpression Right { get; set; } = null!;
}
