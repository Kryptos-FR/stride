using ColorRise.Saga.Content;

namespace ColorRise.Saga.Expressions;

/// <summary>
/// The scope of a story: its declarations, and the paths and symbols its acts, scenarios and quests give.
/// </summary>
public sealed class SagaScope : ISagaScope
{
    private const string QuestPrefix = "quest.";
    private const string ScenarioPrefix = "scenario.";

    private readonly Dictionary<string, VariableDeclaration> variables;
    private readonly Dictionary<string, FunctionDeclaration> functions;
    private readonly HashSet<string> events;
    private readonly Dictionary<string, SignalDeclaration> signals;
    private readonly List<string> acts;
    private readonly HashSet<string> quests;
    private readonly Dictionary<string, IReadOnlyList<string>> scenarioOutcomes;

    /// <param name="declarations">The declarations of the story.</param>
    /// <param name="actIds">The ids of the acts.</param>
    /// <param name="questIds">The ids of the quests.</param>
    /// <param name="scenarioOutcomes">The ids of the scenarios, with the names of their outcomes.</param>
    public SagaScope(StoryDeclarations declarations, IEnumerable<string> actIds, IEnumerable<string> questIds, IReadOnlyDictionary<string, IReadOnlyList<string>> scenarioOutcomes)
    {
        variables = declarations.Variables.GroupBy(x => x.Name).ToDictionary(x => x.Key, x => x.First());
        functions = declarations.Functions.GroupBy(x => x.Name).ToDictionary(x => x.Key, x => x.First());
        events = [.. declarations.Events.Select(x => x.Name)];
        signals = declarations.Signals.GroupBy(x => x.Name).ToDictionary(x => x.Key, x => x.First());
        acts = [.. actIds.Distinct()];
        quests = [.. questIds];
        this.scenarioOutcomes = new Dictionary<string, IReadOnlyList<string>>(scenarioOutcomes);
    }

    /// <summary>
    /// Makes the scope of a compiled story.
    /// </summary>
    public static SagaScope FromStory(StoryDefinition story)
    {
        var acts = story.Plot.Acts;
        return new SagaScope(
            story.Declarations,
            acts.Select(x => x.Id),
            acts.SelectMany(x => x.Quests).Select(x => x.Id),
            acts.SelectMany(x => x.Scenarios).GroupBy(x => x.Id).ToDictionary(x => x.Key, x => (IReadOnlyList<string>)[.. x.First().Outcomes.Select(o => o.Name)]));
    }

    public bool TryGetPathType(string path, out SagaTypeKind type, out string? symbolSet)
    {
        if (variables.TryGetValue(path, out var variable))
        {
            (type, symbolSet) = (variable.Type, SagaSymbolSets.ForType(variable.Type));
            return true;
        }
        if (path == "act")
        {
            (type, symbolSet) = (SagaTypeKind.Act, SagaSymbolSets.Acts);
            return true;
        }
        if (TrySplit(path, QuestPrefix, out var quest, out var member) && quests.Contains(quest))
        {
            switch (member)
            {
                case "state":
                    (type, symbolSet) = (SagaTypeKind.QuestState, SagaSymbolSets.QuestStates);
                    return true;
                case "stage":
                    (type, symbolSet) = (SagaTypeKind.Int, null);
                    return true;
            }
        }
        if (TrySplit(path, ScenarioPrefix, out var scenario, out member) && scenarioOutcomes.ContainsKey(scenario))
        {
            switch (member)
            {
                case "state":
                    (type, symbolSet) = (SagaTypeKind.ScenarioState, SagaSymbolSets.ScenarioStates);
                    return true;
                case "outcome":
                    (type, symbolSet) = (SagaTypeKind.Outcome, SagaSymbolSets.Outcomes(scenario));
                    return true;
            }
        }
        (type, symbolSet) = (default, null);
        return false;
    }

    public FunctionDeclaration? FindFunction(string name) => functions.GetValueOrDefault(name);

    public bool IsEvent(string name) => events.Contains(name);

    public SignalDeclaration? FindSignal(string name) => signals.GetValueOrDefault(name);

    public bool IsAssignable(string path) => variables.ContainsKey(path)
        || (TrySplit(path, QuestPrefix, out var quest, out var member) && quests.Contains(quest) && member is "state" or "stage");

    public IReadOnlyCollection<string> GetSymbols(string symbolSet)
    {
        switch (symbolSet)
        {
            case SagaSymbolSets.QuestStates:
                return SagaSymbolSets.QuestStateNames;
            case SagaSymbolSets.ScenarioStates:
                return SagaSymbolSets.ScenarioStateNames;
            case SagaSymbolSets.Acts:
                return acts;
        }
        if (TrySplit(symbolSet, ScenarioPrefix, out var scenario, out var member) && member == "outcome")
            return scenarioOutcomes.GetValueOrDefault(scenario) ?? [];
        return [];
    }

    /// <summary>
    /// Splits <c>prefix.id.member</c>; the id has no dot.
    /// </summary>
    internal static bool TrySplit(string path, string prefix, out string id, out string member)
    {
        (id, member) = (string.Empty, string.Empty);
        if (!path.StartsWith(prefix, StringComparison.Ordinal))
            return false;
        var dot = path.IndexOf('.', prefix.Length);
        if (dot <= prefix.Length || dot == path.Length - 1 || path.IndexOf('.', dot + 1) >= 0)
            return false;
        (id, member) = (path[prefix.Length..dot], path[(dot + 1)..]);
        return true;
    }
}
