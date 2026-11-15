using ColorRise.Saga.Content;

namespace ColorRise.Saga.Expressions;

/// <summary>
/// What a story declares, as the type checker sees it.
/// </summary>
public interface ISagaScope
{
    /// <summary>
    /// Gets the type of a path: a variable, <c>act</c>, <c>quest.&lt;id&gt;.state</c>, <c>quest.&lt;id&gt;.stage</c>,
    /// <c>scenario.&lt;id&gt;.state</c> or <c>scenario.&lt;id&gt;.outcome</c>.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <param name="type">The type of the path.</param>
    /// <param name="symbolSet">For a symbol type, the set its values come from (see <see cref="SagaSymbolSets"/>).</param>
    /// <returns><see langword="true"/> if the path exists.</returns>
    bool TryGetPathType(string path, out SagaTypeKind type, out string? symbolSet);

    FunctionDeclaration? FindFunction(string name);

    bool IsEvent(string name);

    SignalDeclaration? FindSignal(string name);

    /// <summary>
    /// Tells whether actions can assign the path: variables, <c>quest.&lt;id&gt;.state</c> and <c>quest.&lt;id&gt;.stage</c>.
    /// </summary>
    bool IsAssignable(string path);

    /// <summary>
    /// Gets the symbols of a set.
    /// </summary>
    IReadOnlyCollection<string> GetSymbols(string symbolSet);
}

/// <summary>
/// The names of the symbol sets, and the fixed ones.
/// </summary>
public static class SagaSymbolSets
{
    public const string QuestStates = "quest_state";

    public const string ScenarioStates = "scenario_state";

    public const string Acts = "act";

    /// <summary>
    /// The states of a quest, in the order of its lifecycle.
    /// </summary>
    public static IReadOnlyList<string> QuestStateNames { get; } = ["not_started", "available", "active", "succeeded", "failed"];

    /// <summary>
    /// The states of a scenario, in the order of its lifecycle.
    /// </summary>
    public static IReadOnlyList<string> ScenarioStateNames { get; } = ["pending", "running", "done"];

    /// <summary>
    /// The set of the named outcomes of a scenario.
    /// </summary>
    public static string Outcomes(string scenarioId) => $"scenario.{scenarioId}.outcome";

    /// <summary>
    /// Gets the set the values of a type come from, when the type alone determines it.
    /// </summary>
    public static string? ForType(SagaTypeKind type) => type switch
    {
        SagaTypeKind.QuestState => QuestStates,
        SagaTypeKind.ScenarioState => ScenarioStates,
        SagaTypeKind.Act => Acts,
        _ => null,
    };
}
