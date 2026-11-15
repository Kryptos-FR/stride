using ColorRise.Saga.Assets;
using Stride.Core.Assets.Presentation.ViewModels;
using Stride.Core.Mathematics;
using Stride.Core.Presentation.ViewModels;
using Stride.Core.Quantum;

namespace ColorRise.Saga.Editor.ViewModels;

/// <summary>
/// What a node of a Saga graph stands for.
/// </summary>
public enum SagaGraphNodeKind
{
    Act,
    Scenario,
    Quest,
    Dialogue,
    Line,
    Choices,
    Branch,
    End,
}

/// <summary>
/// A node of a Saga graph: a card of the story (act, scenario, quest), a dialogue of a scenario or quest, or a node of a dialogue.
/// </summary>
public sealed class SagaGraphNodeViewModel : SagaGraphItemViewModel
{
    private readonly List<SagaGraphSlot> outputs = [];
    private bool isEntry;

    internal SagaGraphNodeViewModel(IViewModelServiceProvider serviceProvider, SagaGraphNodeKind kind, AssetViewModel relatedAsset, object card, Func<GraphNodePath>? path)
        : base(serviceProvider, relatedAsset, card, path)
    {
        Kind = kind;
        Input = new SagaGraphSlot(this, SagaGraphSlotDirection.Input);
    }

    public SagaGraphNodeKind Kind { get; }

    /// <summary>
    /// The id of the card, or the name of a dialogue.
    /// </summary>
    public string Id => Card switch
    {
        ActCard act => act.Id,
        ScenarioCard scenario => scenario.Id,
        QuestCard quest => quest.Id,
        DialogueNodeCard node => node.Id,
        _ => DialogueName ?? "",
    };

    /// <summary>
    /// The text of the node: the title of a card that has one, otherwise its id.
    /// </summary>
    public string Title => Card switch
    {
        ActCard { Title.Length: > 0 } act => act.Title,
        ScenarioCard { Title.Length: > 0 } scenario => scenario.Title,
        QuestCard { Title.Length: > 0 } quest => quest.Title,
        DialogueNodeCard { Name.Length: > 0 } node => node.Name,
        LineNodeCard { Text.Length: > 0 } line => line.Text,
        _ => Id,
    };

    /// <summary>
    /// Where the node is drawn; <c>null</c> for a dialogue of a scenario or quest, which the graph places itself.
    /// </summary>
    public Vector2? Position => Card switch
    {
        ActCard act => act.Position,
        ScenarioCard scenario => scenario.Position,
        QuestCard quest => quest.Position,
        DialogueNodeCard node => node.Position,
        _ => null,
    };

    /// <summary>
    /// The slot that links into the node arrive at.
    /// </summary>
    public SagaGraphSlot Input { get; }

    /// <summary>
    /// The main slot that links leave the node from, or <c>null</c> if it has none.
    /// </summary>
    public SagaGraphSlot? Output => outputs.Count > 0 ? outputs[0] : null;

    /// <summary>
    /// The slots that links leave the node from: one for a card of the story or a line, one per choice or branch link.
    /// </summary>
    public IReadOnlyList<SagaGraphSlot> Outputs => outputs;

    /// <summary>
    /// Whether the node is where its dialogue starts.
    /// </summary>
    public bool IsEntry { get => isEntry; internal set => SetValue(ref isEntry, value); }

    /// <summary>
    /// The dialogue a node of a scenario or quest stands for.
    /// </summary>
    public DialogueViewModel? Dialogue { get; internal init; }

    internal string? DialogueName { get; init; }

    internal void AddOutput(string name = "", object? card = null) => outputs.Add(new SagaGraphSlot(this, SagaGraphSlotDirection.Output, name, card));

    /// <summary>
    /// Tells the views that the card behind the node changed.
    /// </summary>
    internal void Refresh() => OnPropertyChanged(nameof(Id), nameof(Title), nameof(Position));

    public override string ToString() => $"{Kind} {Id}";
}
