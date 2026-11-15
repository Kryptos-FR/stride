using ColorRise.Saga.Content;
using ColorRise.Saga.Expressions;

namespace ColorRise.Saga.Assets;

/// <summary>
/// The scope of a story asset: what its conditions and actions, and those of its dialogues, may use.
/// </summary>
public sealed class StoryAssetScope(StoryAsset story) : ISagaScope
{
    private readonly SagaScope scope = new(
        story.Declarations,
        story.Acts.Select(x => x.Id),
        story.Acts.SelectMany(x => x.Quests).Select(x => x.Id),
        story.Acts.SelectMany(x => x.Scenarios).GroupBy(x => x.Id).ToDictionary(x => x.Key, x => (IReadOnlyList<string>)[.. x.First().Outcomes.Select(o => o.Name)]));

    public bool TryGetPathType(string path, out SagaTypeKind type, out string? symbolSet) => scope.TryGetPathType(path, out type, out symbolSet);

    public FunctionDeclaration? FindFunction(string name) => scope.FindFunction(name);

    public bool IsEvent(string name) => scope.IsEvent(name);

    public SignalDeclaration? FindSignal(string name) => scope.FindSignal(name);

    public bool IsAssignable(string path) => scope.IsAssignable(path);

    public IReadOnlyCollection<string> GetSymbols(string symbolSet) => scope.GetSymbols(symbolSet);
}
