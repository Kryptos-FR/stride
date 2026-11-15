using Stride.Core;

namespace ColorRise.Saga.Expressions;

/// <summary>
/// Actions as compiled content: their source, for messages and the editor, and their checked statements.
/// </summary>
[DataContract("SagaActions")]
public sealed class SagaActions : ISagaAction
{
    public string Source { get; set; } = string.Empty;

    public List<SagaStatement> Statements { get; set; } = [];

    public void Execute(ISagaExecutionContext context)
    {
        foreach (var statement in Statements)
            SagaTreeEvaluator.Execute(statement, context);
    }

    public override string ToString() => Source;
}
