namespace ColorRise.Saga.Expressions;

/// <summary>
/// A condition of the story. Saga evaluates the parsed tree; generated code could implement it later.
/// </summary>
public interface ISagaCondition
{
    bool Evaluate(ISagaEvaluationContext context);
}
