using ColorRise.Saga.Assets;
using Stride.Core.Assets.Editor.Annotations;
using Stride.Core.Presentation.Commands;

namespace ColorRise.Saga.Editor.ViewModels;

/// <summary>
/// The editor of a dialogue: its lines, choices, branches and ends, each way out (the next line, a choice, a branch link) a slot
/// that links to the node it leads to.
/// </summary>
[AssetEditorViewModel<DialogueViewModel>]
public sealed class DialogueEditorViewModel : SagaGraphEditorViewModel<DialogueViewModel>
{
    public DialogueEditorViewModel(DialogueViewModel dialogue)
        : base(dialogue)
    {
        AddLineCommand = new AnonymousCommand(ServiceProvider, () => AddNode("Add line", "line_", new LineNodeCard()));
        AddChoicesCommand = new AnonymousCommand(ServiceProvider, () => AddNode("Add choices", "choices_", new ChoicesNodeCard { Choices = { new DialogueChoiceCard() } }));
        AddBranchCommand = new AnonymousCommand(ServiceProvider, () => AddNode("Add branch", "branch_", new BranchNodeCard { Links = { new DialogueLinkCard() } }));
        AddEndCommand = new AnonymousCommand(ServiceProvider, () => AddNode("Add end", "end_", new EndNodeCard()));
        AddWayOutCommand = new AnonymousCommand<SagaGraphNodeViewModel>(ServiceProvider, AddWayOut, x => x?.Card is ChoicesNodeCard or BranchNodeCard);
        SetEntryCommand = new AnonymousCommand<SagaGraphNodeViewModel>(ServiceProvider, x => Change("Set entry node", () => RootNode[nameof(DialogueAsset.EntryNodeId)].Update(x.Id)), x => x?.Card is DialogueNodeCard);
    }

    public ICommandBase AddLineCommand { get; }

    public ICommandBase AddChoicesCommand { get; }

    public ICommandBase AddBranchCommand { get; }

    public ICommandBase AddEndCommand { get; }

    /// <summary>
    /// Adds a choice to a choices node, or a link to a branch node (<see cref="SagaGraphNodeViewModel"/>).
    /// </summary>
    public ICommandBase AddWayOutCommand { get; }

    /// <summary>
    /// Makes a node (<see cref="SagaGraphNodeViewModel"/>) the one the dialogue starts at.
    /// </summary>
    public ICommandBase SetEntryCommand { get; }

    private DialogueAsset Dialogue => Asset.Asset;

    /// <summary>
    /// Whether a way out of a node can lead to a node it doesn't lead to yet.
    /// </summary>
    public override bool CanLink(SagaGraphSlot source, SagaGraphSlot target)
        => source.Direction == SagaGraphSlotDirection.Output && target.Direction == SagaGraphSlotDirection.Input && TargetOf(source.Card) != target.Node.Id;

    /// <inheritdoc />
    protected override void Link(SagaGraphSlot source, SagaGraphSlot target)
        => Change("Link nodes", () => SetTarget(source.Card!, target.Node.Id));

    /// <inheritdoc />
    protected override string UnlinkName(SagaGraphLinkViewModel link) => "Unlink nodes";

    /// <inheritdoc />
    protected override void Unlink(SagaGraphLinkViewModel link) => SetTarget(link.Card!, string.Empty);

    /// <inheritdoc />
    protected override void Delete(SagaGraphNodeViewModel node)
    {
        if (node.Card is not DialogueNodeCard card)
            return;
        foreach (var way in Dialogue.Nodes.SelectMany(WaysOutOf).Where(x => TargetOf(x) == card.Id))
            SetTarget(way, string.Empty);
        if (Dialogue.EntryNodeId == card.Id)
            RootNode[nameof(DialogueAsset.EntryNodeId)].Update(string.Empty);
        RemoveFrom(Dialogue, nameof(DialogueAsset.Nodes), Dialogue.Nodes, card);
    }

    /// <inheritdoc />
    protected override IEnumerable<SagaGraphNodeViewModel> BuildNodes(IReadOnlyDictionary<object, SagaGraphNodeViewModel> previous)
    {
        foreach (var card in Dialogue.Nodes)
        {
            var ways = WaysOutOf(card).Select((x, i) => (Card: x, Name: NameOf(x, i))).ToList();
            // A node is kept while its ways out are the same; a new choice or a renamed one makes a new node
            if (!previous.TryGetValue(card, out var node) || !node.Outputs.Select(x => (x.Card!, x.Name)).SequenceEqual(ways))
            {
                node = new SagaGraphNodeViewModel(ServiceProvider, KindOf(card), Asset, card, () => PathToItem(null, nameof(DialogueAsset.Nodes), Dialogue.Nodes.IndexOf(card)));
                foreach (var (way, name) in ways)
                    node.AddOutput(name, way);
            }
            node.IsEntry = card.Id == Dialogue.EntryNodeId;
            yield return node;
        }
    }

    /// <inheritdoc />
    protected override IEnumerable<SagaGraphLinkViewModel> BuildLinks(IReadOnlyDictionary<object, SagaGraphNodeViewModel> nodes)
    {
        var byId = new Dictionary<string, SagaGraphNodeViewModel>();
        foreach (var node in nodes.Values)
            byId.TryAdd(node.Id, node);
        foreach (var node in Dialogue.Nodes.Select(x => nodes[x]))
        {
            foreach (var output in node.Outputs)
            {
                if (byId.TryGetValue(TargetOf(output.Card), out var target))
                    yield return new SagaGraphLinkViewModel(ServiceProvider, Asset, output, target.Input, output.Card, PathOf(node, output.Card));
            }
        }
    }

    private void AddNode(string name, string prefix, DialogueNodeCard card)
    {
        Change(name, () =>
        {
            var used = Dialogue.Nodes.Select(x => x.Id).ToHashSet();
            var number = Dialogue.Nodes.Count(x => x.GetType() == card.GetType()) + 1;
            while (used.Contains(prefix + number))
                ++number;
            card.Id = prefix + number;
            card.Position = NextPosition();
            ListOf(Dialogue, nameof(DialogueAsset.Nodes)).Add(card);
        });
    }

    private void AddWayOut(SagaGraphNodeViewModel node)
    {
        switch (node.Card)
        {
            case ChoicesNodeCard choices:
                Change("Add choice", () => ListOf(choices, nameof(ChoicesNodeCard.Choices)).Add(new DialogueChoiceCard()));
                break;
            case BranchNodeCard branch:
                Change("Add branch link", () => ListOf(branch, nameof(BranchNodeCard.Links)).Add(new DialogueLinkCard()));
                break;
        }
    }

    /// <inheritdoc />
    protected override void RenameReferences(object card, string oldId, string newId)
    {
        if (card is not DialogueNodeCard)
            return;
        foreach (var way in Dialogue.Nodes.SelectMany(WaysOutOf).Where(x => TargetOf(x) == oldId).ToList())
            SetTarget(way, newId);
        if (Dialogue.EntryNodeId == oldId)
            RootNode[nameof(DialogueAsset.EntryNodeId)].Update(newId);
    }

    private void SetTarget(object way, string target)
    {
        var member = way switch
        {
            LineNodeCard => nameof(LineNodeCard.NextId),
            DialogueChoiceCard => nameof(DialogueChoiceCard.TargetId),
            DialogueLinkCard => nameof(DialogueLinkCard.TargetId),
            _ => null,
        };
        if (member is not null)
            NodeOf(way)[member].Update(target);
    }

    /// <summary>
    /// The path to the card of a way out that has its own properties: a choice or a branch link. The next line of a line has none.
    /// </summary>
    private Func<Stride.Core.Quantum.GraphNodePath>? PathOf(SagaGraphNodeViewModel node, object? way) => (node.Card, way) switch
    {
        (ChoicesNodeCard choices, DialogueChoiceCard choice) => () => PathToItem(PathToItem(null, nameof(DialogueAsset.Nodes), Dialogue.Nodes.IndexOf(choices)), nameof(ChoicesNodeCard.Choices), choices.Choices.IndexOf(choice)),
        (BranchNodeCard branch, DialogueLinkCard link) => () => PathToItem(PathToItem(null, nameof(DialogueAsset.Nodes), Dialogue.Nodes.IndexOf(branch)), nameof(BranchNodeCard.Links), branch.Links.IndexOf(link)),
        _ => null,
    };

    private static IEnumerable<object> WaysOutOf(DialogueNodeCard card) => card switch
    {
        LineNodeCard line => [line],
        ChoicesNodeCard choices => choices.Choices,
        BranchNodeCard branch => branch.Links,
        _ => [],
    };

    private static string TargetOf(object? way) => way switch
    {
        LineNodeCard line => line.NextId,
        DialogueChoiceCard choice => choice.TargetId,
        DialogueLinkCard link => link.TargetId,
        _ => string.Empty,
    };

    private static string NameOf(object way, int index) => way switch
    {
        DialogueChoiceCard choice => choice.Text.Length > 0 ? choice.Text : $"choice {index + 1}",
        DialogueLinkCard link => link.When.Length > 0 ? link.When : "otherwise",
        _ => string.Empty,
    };

    private static SagaGraphNodeKind KindOf(DialogueNodeCard card) => card switch
    {
        LineNodeCard => SagaGraphNodeKind.Line,
        ChoicesNodeCard => SagaGraphNodeKind.Choices,
        BranchNodeCard => SagaGraphNodeKind.Branch,
        _ => SagaGraphNodeKind.End,
    };
}
