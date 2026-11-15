using System.Collections.Specialized;
using Stride.Core.Assets.Editor.ViewModels;
using Stride.Core.Assets.Presentation.ViewModels;
using Stride.Core.Mathematics;
using Stride.Core.Presentation.Collections;
using Stride.Core.Presentation.Commands;
using Stride.Core.Presentation.Quantum;
using Stride.Core.Quantum;

namespace ColorRise.Saga.Editor.ViewModels;

/// <summary>
/// What the story and dialogue editors share: a graph of nodes and links rebuilt from the asset whenever it changes, a selection
/// shown in the property grid, and changes made through the asset's property graph, one step of the undo history each.
/// </summary>
/// <typeparam name="TAssetViewModel">The view model of the edited asset.</typeparam>
public abstract class SagaGraphEditorViewModel<TAssetViewModel> : AssetEditorViewModel<TAssetViewModel>
    where TAssetViewModel : AssetViewModel
{
    /// <summary>
    /// The horizontal space a new node leaves after the rightmost one.
    /// </summary>
    protected const float NewNodeSpacing = 220;

    private bool refreshing;
    private bool changingSelection;

    protected SagaGraphEditorViewModel(TAssetViewModel asset)
        : base(asset)
    {
        RootNode = Session.AssetNodeContainer.GetNode(asset.Asset) ?? throw new InvalidOperationException("The asset has no property graph.");
        LinkCommand = new AnonymousCommand<SagaLinkRequest>(ServiceProvider, x => Link(x.Source, x.Target), x => x is not null && CanLink(x.Source, x.Target));
        UnlinkCommand = new AnonymousCommand<SagaGraphLinkViewModel>(ServiceProvider, x => Change(UnlinkName(x), () => Unlink(x)));
        DeleteSelectionCommand = new AnonymousCommand(ServiceProvider, DeleteSelection);
    }

    /// <summary>
    /// The nodes of the graph.
    /// </summary>
    public ObservableList<SagaGraphNodeViewModel> Nodes { get; } = [];

    /// <summary>
    /// The links of the graph.
    /// </summary>
    public ObservableList<SagaGraphLinkViewModel> Links { get; } = [];

    /// <summary>
    /// The selected nodes, which the property grid shows.
    /// </summary>
    public ObservableList<object> SelectedNodes { get; } = [];

    /// <summary>
    /// The selected links, which the property grid shows when no node is selected.
    /// </summary>
    public ObservableList<object> SelectedLinks { get; } = [];

    /// <summary>
    /// Links two slots (<see cref="SagaLinkRequest"/>), when <see cref="CanLink"/> accepts it.
    /// </summary>
    public ICommandBase LinkCommand { get; }

    /// <summary>
    /// Removes a link (<see cref="SagaGraphLinkViewModel"/>).
    /// </summary>
    public ICommandBase UnlinkCommand { get; }

    /// <summary>
    /// Deletes the selected nodes and links, and the links to the deleted nodes.
    /// </summary>
    public ICommandBase DeleteSelectionCommand { get; }

    /// <summary>
    /// The node of the edited asset in its property graph.
    /// </summary>
    protected IObjectNode RootNode { get; }

    /// <inheritdoc />
    public override Task<bool> Initialize()
    {
        Asset.PropertyGraph.Changed += AssetChanged;
        Asset.PropertyGraph.ItemChanged += AssetChanged;
        SelectedNodes.CollectionChanged += SelectionChanged;
        SelectedLinks.CollectionChanged += SelectionChanged;
        Refresh();
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public override void Destroy()
    {
        Asset.PropertyGraph.Changed -= AssetChanged;
        Asset.PropertyGraph.ItemChanged -= AssetChanged;
        SelectedNodes.CollectionChanged -= SelectionChanged;
        SelectedLinks.CollectionChanged -= SelectionChanged;
        base.Destroy();
    }

    /// <summary>
    /// Whether a link can go from a slot to another.
    /// </summary>
    public abstract bool CanLink(SagaGraphSlot source, SagaGraphSlot target);

    /// <summary>
    /// Moves nodes to new positions, in one step of the undo history.
    /// </summary>
    public void MoveNodes(IEnumerable<(SagaGraphNodeViewModel Node, Vector2 Position)> moves)
    {
        Change("Move cards", () =>
        {
            foreach (var (node, position) in moves)
            {
                if (node.Position is { } current && current != position)
                    NodeOf(node.Card!)[PositionMember].Update(position);
            }
        });
    }

    /// <summary>
    /// The name of the member that holds the position of a card.
    /// </summary>
    protected virtual string PositionMember => "Position";

    /// <summary>
    /// Builds the nodes of the graph from the asset.
    /// </summary>
    /// <param name="previous">The nodes of the graph before, by card, to keep those whose card is still there.</param>
    protected abstract IEnumerable<SagaGraphNodeViewModel> BuildNodes(IReadOnlyDictionary<object, SagaGraphNodeViewModel> previous);

    /// <summary>
    /// Builds the links of the graph from the asset.
    /// </summary>
    /// <param name="nodes">The nodes of the graph, by card.</param>
    protected abstract IEnumerable<SagaGraphLinkViewModel> BuildLinks(IReadOnlyDictionary<object, SagaGraphNodeViewModel> nodes);

    /// <summary>
    /// Links two slots that <see cref="CanLink"/> accepted, as one named step of the undo history.
    /// </summary>
    protected abstract void Link(SagaGraphSlot source, SagaGraphSlot target);

    /// <summary>
    /// The name of the undo step that removes a link.
    /// </summary>
    protected abstract string UnlinkName(SagaGraphLinkViewModel link);

    /// <summary>
    /// Removes a link; the caller opens the transaction.
    /// </summary>
    protected abstract void Unlink(SagaGraphLinkViewModel link);

    /// <summary>
    /// Deletes a node and what leads to it; the caller opens the transaction.
    /// </summary>
    protected abstract void Delete(SagaGraphNodeViewModel node);

    /// <summary>
    /// Called before the graph is rebuilt.
    /// </summary>
    protected virtual void OnRefreshing()
    {
    }

    /// <summary>
    /// Runs a change of the asset as one named step of the undo history.
    /// </summary>
    protected void Change(string name, Action change)
    {
        using var transaction = UndoRedoService.CreateTransaction();
        change();
        UndoRedoService.SetName(transaction, name);
    }

    /// <summary>
    /// The node of a card in the property graph.
    /// </summary>
    protected IObjectNode NodeOf(object card) => Session.AssetNodeContainer.GetOrCreateNode(card) ?? throw new InvalidOperationException($"{card} is not in the asset.");

    /// <summary>
    /// The node of a list member of a card in the property graph.
    /// </summary>
    protected IObjectNode ListOf(object owner, string member) => NodeOf(owner)[member].Target ?? throw new InvalidOperationException($"{member} of {owner} is not a list.");

    /// <summary>
    /// Removes an item from a list member of a card, if it is there.
    /// </summary>
    protected void RemoveFrom<T>(object owner, string member, List<T> list, T item)
    {
        var index = list.IndexOf(item);
        if (index >= 0)
            ListOf(owner, member).Remove(item, new NodeIndex(index));
    }

    /// <summary>
    /// A path from the asset to an item of a list member of the asset or of a card.
    /// </summary>
    protected GraphNodePath PathToItem(GraphNodePath? owner, string member, int index)
    {
        var path = owner ?? new GraphNodePath(RootNode);
        path.PushMember(member);
        path.PushTarget();
        path.PushIndex(new NodeIndex(index));
        return path;
    }

    /// <summary>
    /// Where a new node goes: right of the rightmost node.
    /// </summary>
    protected Vector2 NextPosition()
    {
        var placed = Nodes.Where(x => x.Position is not null).ToList();
        return placed.Count == 0 ? Vector2.Zero : new Vector2(placed.Max(x => x.Position!.Value.X) + NewNodeSpacing, placed[0].Position!.Value.Y);
    }

    /// <summary>
    /// A new id: the prefix and the first free number from the count of existing ids plus one.
    /// </summary>
    protected static string NewId(string prefix, IEnumerable<string> existing)
    {
        var used = existing.ToHashSet();
        var number = used.Count + 1;
        while (used.Contains(prefix + number))
            ++number;
        return prefix + number;
    }

    /// <summary>
    /// Clears the selection.
    /// </summary>
    protected void ClearSelection()
    {
        SelectedNodes.Clear();
        SelectedLinks.Clear();
    }

    /// <summary>
    /// Rebuilds the graph from the asset, keeping the view models of the cards that are still there.
    /// </summary>
    protected void Refresh()
    {
        if (refreshing)
            return;
        refreshing = true;
        try
        {
            OnRefreshing();
            var previous = Nodes.ToDictionary(x => x.Card!, ReferenceEqualityComparer.Instance);
            var nodes = BuildNodes(previous).ToList();
            if (!nodes.SequenceEqual(Nodes))
            {
                Nodes.Clear();
                Nodes.AddRange(nodes);
            }
            foreach (var node in nodes)
                node.Refresh();

            var byCard = nodes.ToDictionary(x => x.Card!, ReferenceEqualityComparer.Instance);
            var previousLinks = Links.ToDictionary(x => (x.Source, x.Target));
            var links = BuildLinks(byCard).Select(x => previousLinks.TryGetValue((x.Source, x.Target), out var kept) && kept.Card == x.Card ? kept : x).ToList();
            if (!links.SequenceEqual(Links))
            {
                Links.Clear();
                Links.AddRange(links);
            }

            foreach (var gone in SelectedNodes.Where(x => !Nodes.Contains(x)).ToList())
                SelectedNodes.Remove(gone);
            foreach (var gone in SelectedLinks.Where(x => !Links.Contains(x)).ToList())
                SelectedLinks.Remove(gone);
        }
        finally
        {
            refreshing = false;
        }
    }

    private void DeleteSelection()
    {
        var nodes = SelectedNodes.OfType<SagaGraphNodeViewModel>().ToList();
        var links = SelectedLinks.OfType<SagaGraphLinkViewModel>().ToList();
        if (nodes.Count == 0 && links.Count == 0)
            return;
        // Clear the selection first, so that nothing tries to unselect what no longer exists. The property grid is updated once the
        // asset changed: it builds its nodes on another thread, which must not run while the asset changes.
        changingSelection = true;
        try
        {
            ClearSelection();
        }
        finally
        {
            changingSelection = false;
        }
        Change("Delete selection", () =>
        {
            foreach (var link in links)
                Unlink(link);
            foreach (var node in nodes)
                Delete(node);
        });
        UpdateProperties();
    }

    private void AssetChanged(object? sender, EventArgs e) => Refresh();

    private void SelectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!refreshing && !changingSelection)
            UpdateProperties();
    }

    /// <summary>
    /// Shows the selected nodes in the property grid, or the selected links, or else the asset.
    /// </summary>
    private async void UpdateProperties()
    {
        var selected = (SelectedNodes.Count > 0 ? SelectedNodes : SelectedLinks).OfType<SagaGraphItemViewModel>().Where(x => x.CanProvidePropertiesViewModel).ToList();
        if (selected.Count == 0)
        {
            ShowAssetProperties();
            return;
        }
        EditorProperties.UpdateTypeAndName(selected, x => x is SagaGraphNodeViewModel node ? node.Kind.ToString() : "Link", x => x.ToString() ?? "", "cards");
        await EditorProperties.GenerateSelectionPropertiesAsync(selected);
    }
}
