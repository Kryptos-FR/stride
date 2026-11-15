namespace ColorRise.Saga.Expressions;

/// <summary>
/// Actions of the story. Saga runs the parsed statements; generated code could implement it later.
/// </summary>
public interface ISagaAction
{
    void Execute(ISagaExecutionContext context);
}
