using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ColorRise.Saga.Editor.ViewModels;
using Stride.Core.Assets.Presentation.ViewModels;
using Stride.Core.Presentation.Avalonia.Controls;
using Vector2 = Stride.Core.Mathematics.Vector2;

namespace ColorRise.Saga.Editor.Avalonia.Views;

/// <summary>
/// Shows a Saga graph editor in a <see cref="NodeGraphControl"/>: its nodes, links and selection, its link commands, and the
/// positions of its cards, which the graph shows and the editor saves once the user moved them.
/// </summary>
/// <typeparam name="TAssetViewModel">The view model of the edited asset.</typeparam>
/// <remarks>
/// The graph doesn't save positions, nor says when the user moved nodes, so the positions are read back when the pointer is
/// released or the graph loses it. A node without a position of its own (a dialogue of a scenario or quest) goes in a row at the
/// top the first time it is shown.
/// </remarks>
public sealed class SagaGraphBinding<TAssetViewModel> : INodeGraphLinkResolver, IDisposable
    where TAssetViewModel : AssetViewModel
{
    private const double RowSpacing = 220;

    private readonly NodeGraphControl graph;
    private readonly SagaGraphEditorViewModel<TAssetViewModel> editor;
    private readonly Action<SagaGraphNodeViewModel>? open;
    private readonly List<SagaGraphNodeViewModel> observedNodes = [];

    /// <summary>
    /// Shows an editor in a graph.
    /// </summary>
    /// <param name="graph">The graph.</param>
    /// <param name="editor">The editor.</param>
    /// <param name="open">What a double click on a node does, if anything.</param>
    public SagaGraphBinding(NodeGraphControl graph, SagaGraphEditorViewModel<TAssetViewModel> editor, Action<SagaGraphNodeViewModel>? open = null)
    {
        this.graph = graph;
        this.editor = editor;
        this.open = open;

        graph.AutoLayout = false;
        graph.LinkResolver = this;
        graph.LinkCommand = new GraphCommand(x => ToRequest(x) is { } request && editor.LinkCommand.CanExecute(request), x => editor.LinkCommand.Execute(ToRequest(x)));
        graph.UnlinkCommand = new GraphCommand(x => x is SagaGraphLinkViewModel, editor.UnlinkCommand.Execute);
        graph.SelectedNodes = editor.SelectedNodes;
        graph.SelectedLinks = editor.SelectedLinks;
        // The graph follows the nodes before this binding places them
        graph.Nodes = editor.Nodes;
        graph.Links = editor.Links;
        editor.Nodes.CollectionChanged += OnNodesChanged;
        graph.AddHandler(InputElement.PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Bubble, handledEventsToo: true);
        graph.PointerCaptureLost += OnPointerCaptureLost;
        graph.DoubleTapped += OnDoubleTapped;
        ObserveNodes();
        PlaceNodes(editor.Nodes);
        FitToView();
    }

    /// <inheritdoc />
    public object? GetSourceSlot(object link) => (link as SagaGraphLinkViewModel)?.Source;

    /// <inheritdoc />
    public object? GetTargetSlot(object link) => (link as SagaGraphLinkViewModel)?.Target;

    /// <summary>
    /// Shows every node, once the graph has measured them.
    /// </summary>
    public void FitToView() => Dispatcher.UIThread.Post(graph.FitToView, DispatcherPriority.Background);

    /// <inheritdoc />
    public void Dispose()
    {
        editor.Nodes.CollectionChanged -= OnNodesChanged;
        graph.RemoveHandler(InputElement.PointerReleasedEvent, OnPointerReleased);
        graph.PointerCaptureLost -= OnPointerCaptureLost;
        graph.DoubleTapped -= OnDoubleTapped;
        foreach (var node in observedNodes)
            node.PropertyChanged -= OnNodePropertyChanged;
        observedNodes.Clear();
        graph.Nodes = null;
        graph.Links = null;
        graph.LinkResolver = null;
        graph.LinkCommand = null;
        graph.UnlinkCommand = null;
        graph.SelectedNodes = null;
        graph.SelectedLinks = null;
    }

    private static SagaLinkRequest? ToRequest(object? parameter)
        => parameter is NodeGraphLinkRequest { SourceSlot: SagaGraphSlot source, TargetSlot: SagaGraphSlot target } request
            ? new SagaLinkRequest(source, target, request.ReplacedLink as SagaGraphLinkViewModel)
            : null;

    private static Point ToPoint(Vector2 position) => new(position.X, position.Y);

    private void OnNodesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ObserveNodes();
        PlaceNodes(e.NewItems?.OfType<SagaGraphNodeViewModel>() ?? []);
    }

    private void ObserveNodes()
    {
        foreach (var node in observedNodes)
            node.PropertyChanged -= OnNodePropertyChanged;
        observedNodes.Clear();
        observedNodes.AddRange(editor.Nodes);
        foreach (var node in observedNodes)
            node.PropertyChanged += OnNodePropertyChanged;
    }

    private void PlaceNodes(IEnumerable<SagaGraphNodeViewModel> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.Position is { } position)
            {
                graph.SetNodePosition(node, ToPoint(position));
            }
            else if (graph.GetNodePosition(node) is null)
            {
                // In the row of the nodes without a position, after those already shown
                var index = editor.Nodes.Where(x => x.Position is null).TakeWhile(x => x != node).Count();
                graph.SetNodePosition(node, new Point(NodeGraphControl.LayoutMargin + index * RowSpacing, NodeGraphControl.LayoutMargin));
            }
        }
    }

    private void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // A card moved by an undo, or by the property grid
        if (e.PropertyName == nameof(SagaGraphNodeViewModel.Position) && sender is SagaGraphNodeViewModel { Position: { } position } node && graph.GetNodePosition(node) != ToPoint(position))
            graph.SetNodePosition(node, ToPoint(position));
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e) => SavePositions();

    private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e) => SavePositions();

    private void SavePositions()
    {
        var moves = new List<(SagaGraphNodeViewModel, Vector2)>();
        foreach (var node in editor.Nodes)
        {
            if (node.Position is { } current && graph.GetNodePosition(node) is { } shown)
            {
                var position = new Vector2((float)shown.X, (float)shown.Y);
                if (position != current)
                    moves.Add((node, position));
            }
        }
        if (moves.Count > 0)
            editor.MoveNodes(moves);
    }

    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (open is null || (e.Source as Visual)?.GetSelfAndVisualAncestors().OfType<NodeGraphNode>().FirstOrDefault() is not { Node: SagaGraphNodeViewModel node })
            return;
        open(node);
        e.Handled = true;
    }

    private sealed class GraphCommand(Func<object?, bool> canExecute, Action<object?> execute) : ICommand
    {
        /// <inheritdoc/>
        /// <remarks>The graph asks again at each move of a dragged link.</remarks>
        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => canExecute(parameter);

        public void Execute(object? parameter) => execute(parameter);
    }
}
