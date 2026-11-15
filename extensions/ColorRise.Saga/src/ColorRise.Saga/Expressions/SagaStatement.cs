using Stride.Core;

namespace ColorRise.Saga.Expressions;

/// <summary>
/// A statement of an action list.
/// </summary>
[DataContract(Inherited = true)]
public abstract class SagaStatement
{
    /// <summary>
    /// Where the statement starts in its source.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// How many characters of the source the statement covers.
    /// </summary>
    public int Length { get; set; }
}

/// <summary>
/// <c>path = value</c>: sets a variable, or the state or stage of a quest.
/// </summary>
[DataContract("SagaAssignStatement")]
public sealed class AssignStatement : SagaStatement
{
    public string Path { get; set; } = string.Empty;

    public SagaExpression Value { get; set; } = null!;
}

/// <summary>
/// <c>raise event</c>: raises an event of the story, as the game would.
/// </summary>
[DataContract("SagaRaiseStatement")]
public sealed class RaiseStatement : SagaStatement
{
    public string Event { get; set; } = string.Empty;
}

/// <summary>
/// <c>emit signal(arguments)</c>: asks the game to act.
/// </summary>
[DataContract("SagaEmitStatement")]
public sealed class EmitStatement : SagaStatement
{
    public string Signal { get; set; } = string.Empty;

    public List<SagaExpression> Arguments { get; set; } = [];
}
