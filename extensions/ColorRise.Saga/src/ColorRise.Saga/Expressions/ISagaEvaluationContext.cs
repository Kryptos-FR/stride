namespace ColorRise.Saga.Expressions;

/// <summary>
/// What a condition reads: the story state and the game's functions.
/// </summary>
public interface ISagaEvaluationContext
{
    /// <summary>
    /// Gets the value of a path: a variable, <c>act</c>, a quest or scenario state, a stage, an outcome.
    /// </summary>
    SagaValue Resolve(string path);

    /// <summary>
    /// Calls a function the game registered.
    /// </summary>
    SagaValue Call(string function, IReadOnlyList<SagaValue> arguments);

    /// <summary>
    /// Gets how many times an event was raised.
    /// </summary>
    int EventCount(string name);

    /// <summary>
    /// Gets how many times a named dialogue node was played.
    /// </summary>
    int Visits(string name);
}

/// <summary>
/// What an action changes, on top of what it reads.
/// </summary>
public interface ISagaExecutionContext : ISagaEvaluationContext
{
    void Assign(string path, SagaValue value);

    void Raise(string name);

    void Emit(string signal, IReadOnlyList<SagaValue> arguments);
}
