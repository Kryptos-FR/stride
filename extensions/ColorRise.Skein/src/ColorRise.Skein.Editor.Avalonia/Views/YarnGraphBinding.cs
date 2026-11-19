using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using ColorRise.Skein.Editor.Graph;
using ColorRise.Skein.Editor.ViewModels;
using Stride.Core.Presentation.Avalonia.Controls;

namespace ColorRise.Skein.Editor.Avalonia.Views;

/// <summary>
/// Shows the editor of a Yarn script in a <see cref="NodeGraphControl"/>: its cards, stubs, links and selection, at the
/// places the editor gives. What the user does on the graph goes to the editor, which changes the text; the graph then
/// shows what the editor reads from the text.
/// </summary>
public sealed class YarnGraphBinding : INodeGraphLinkResolver, IDisposable
{
    private readonly NodeGraphControl graph;
    private readonly YarnScriptEditorViewModel editor;
    // The room around the nodes when the graph shows them all: the frames of the nodes and their labels fit in it
    private const double FitMargin = 60;
    // Between the middles of two cards closer than this, both are in view and the row says where the link leaves from
    private const double LabelMinLength = 420;

    // In multiples of the thickness of the links
    private static readonly IDashStyle DashedLink = new ImmutableDashStyle([4, 3], 0);
    private static readonly IDashStyle DottedLink = new ImmutableDashStyle([1, 2], 0);

    private readonly List<INotifyPropertyChanged> observed = [];
    private bool fitted;
    // What the last fit gave: while the graph still has it, a change of its size fits again
    private (double Zoom, Vector Offset, Size Size) fit;

    // The links whose label the graph shows, for the zoom and the place of the view it last drew
    private HashSet<YarnLinkViewModel> Labelled { get; set; } = [];

    /// <summary>
    /// Shows an editor in a graph.
    /// </summary>
    public YarnGraphBinding(NodeGraphControl graph, YarnScriptEditorViewModel editor)
    {
        this.graph = graph;
        this.editor = editor;

        graph.AutoLayout = false;
        graph.LinkResolver = this;
        graph.LinkCommand = new GraphCommand(x => ToRequest(x) is not null, x => editor.LinkCommand.Execute(ToRequest(x)));
        graph.SelectedNodes = editor.SelectedCards;
        // The graph makes the containers of the nodes before this binding places them
        graph.Nodes = editor.Nodes;
        graph.Links = editor.Links;
        editor.Nodes.CollectionChanged += OnNodesChanged;
        graph.NodesMoved += OnNodesMoved;
        graph.NodeDoubleClicked += OnNodeDoubleClicked;
        graph.LayoutUpdated += OnLayoutUpdated;
        graph.PropertyChanged += OnGraphPropertyChanged;
        Observe();
        foreach (var node in editor.Nodes)
        {
            Show(node);
        }
    }

    /// <summary>
    /// Raised when what the graph shows of a node changed: its place, its look, or the nodes themselves.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// Raised when the user asks to rename a card, with a double click.
    /// </summary>
    public event Action<YarnNodeCardViewModel>? RenameRequested;

    /// <inheritdoc />
    public object? GetSourceSlot(object link) => (link as YarnLinkViewModel)?.SourceSlot;

    /// <inheritdoc />
    public object? GetTargetSlot(object link) => (link as YarnLinkViewModel)?.TargetSlot;

    /// <summary>
    /// The look of a link: a detour dashed and teal, a jump to an expression dotted and blue-grey, a jump to a node that
    /// does not exist red. The option or the branch it leaves from is its label, when its cards are far apart or one of
    /// them is out of view (<see cref="ShowsLabel"/>).
    /// </summary>
    public NodeGraphLinkStyle? GetStyle(object link)
    {
        if (link is not YarnLinkViewModel { } jump)
            return null;

        var computed = jump.Target is YarnStubCardViewModel { IsComputed: true };
        var label = ShowsLabel(jump) ? jump.Label : null;
        if (!jump.IsDetour && !jump.IsBroken && !computed && label is null)
            return null;

        return new NodeGraphLinkStyle
        {
            Brush = jump.IsBroken ? YarnPalette.Error : computed ? YarnPalette.ComputedLink : jump.IsDetour ? YarnPalette.DetourLink : null,
            DashStyle = computed ? DottedLink : jump.IsDetour ? DashedLink : null,
            Label = label,
        };
    }

    /// <summary>
    /// Whether a link shows its label: it has one, and its cards are far apart, or one of them is out of view, so the row
    /// it leaves from may not be on the screen.
    /// </summary>
    public bool ShowsLabel(YarnLinkViewModel link)
    {
        if (link.Label is null || BoundsOf(link.Source) is not { } source || BoundsOf(link.Target) is not { } target)
            return false;

        var (from, to) = (source.Center, target.Center);
        if (Math.Sqrt(Math.Pow(to.X - from.X, 2) + Math.Pow(to.Y - from.Y, 2)) >= LabelMinLength)
            return true;

        if (graph.Bounds.Width <= 0 || graph.Bounds.Height <= 0)
            return false;

        var view = new Rect(graph.ViewToGraph(default), graph.ViewToGraph(new Point(graph.Bounds.Width, graph.Bounds.Height)));
        return !view.Intersects(source) || !view.Intersects(target);
    }

    /// <summary>
    /// Zooms and pans the graph so that it shows every node with its frames, at a zoom of 1 at most.
    /// </summary>
    public void FitToView()
    {
        Rect? found = null;
        foreach (var node in editor.Nodes)
        {
            if (graph.GetNodePosition(node) is { } position && graph.ContainerFromNode(node) is { } container)
            {
                var rect = new Rect(position, container.Bounds.Size);
                found = found?.Union(rect) ?? rect;
            }
        }

        var size = graph.Bounds.Size;
        if (found is not { } nodes || size.Width <= 0 || size.Height <= 0)
            return;

        var area = nodes.Inflate(FitMargin);
        graph.Zoom = Math.Min(1.0, Math.Min(size.Width / area.Width, size.Height / area.Height));
        graph.Offset = new Vector((size.Width - area.Width * graph.Zoom) / 2 - area.X * graph.Zoom, (size.Height - area.Height * graph.Zoom) / 2 - area.Y * graph.Zoom);
        fit = (graph.Zoom, graph.Offset, size);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        editor.Nodes.CollectionChanged -= OnNodesChanged;
        graph.NodesMoved -= OnNodesMoved;
        graph.NodeDoubleClicked -= OnNodeDoubleClicked;
        graph.LayoutUpdated -= OnLayoutUpdated;
        graph.PropertyChanged -= OnGraphPropertyChanged;
        foreach (var node in observed)
        {
            node.PropertyChanged -= OnNodePropertyChanged;
        }

        observed.Clear();
        graph.Nodes = null;
        graph.Links = null;
        graph.LinkResolver = null;
        graph.LinkCommand = null;
        graph.SelectedNodes = null;
    }

    private static Point ToPoint(YarnGraphPoint position) => new(position.X, position.Y);

    // A link dragged from an output of a card to the input of a card or of a stub; a link that is there cannot be moved
    // A link from a row goes under the row
    private static YarnLinkRequest? ToRequest(object? parameter)
        => parameter is NodeGraphLinkRequest { SourceSlot: YarnCardSlot { IsInput: false, Card: YarnNodeCardViewModel source } from, TargetSlot: YarnCardSlot { IsInput: true } to, ReplacedLink: null }
            ? new YarnLinkRequest(source, to.Card, ReferenceEquals(from, source.DetourSlot), from.Row)
            : null;

    private void OnNodesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Observe();
        foreach (var node in e.NewItems ?? Array.Empty<object>())
        {
            Show(node);
        }

        Changed?.Invoke();
    }

    private void Observe()
    {
        foreach (var node in observed)
        {
            node.PropertyChanged -= OnNodePropertyChanged;
        }

        observed.Clear();
        observed.AddRange(editor.Nodes.OfType<INotifyPropertyChanged>());
        foreach (var node in observed)
        {
            node.PropertyChanged += OnNodePropertyChanged;
        }
    }

    private void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not null)
        {
            Show(sender);
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Puts a node at its place, and gives its container the classes the styles of the graph know.
    /// </summary>
    private void Show(object node)
    {
        if (node is not IYarnGraphNode placed || graph.ContainerFromNode(node) is not { } container)
            return;

        var position = ToPoint(placed.Position);
        if (graph.GetNodePosition(node) != position)
        {
            graph.SetNodePosition(node, position);
        }

        var card = node as YarnNodeCardViewModel;
        var stub = node as YarnStubCardViewModel;
        container.Classes.Set("note", card?.IsNote == true);
        container.Classes.Set("filtered", card?.IsFilteredOut == true);
        container.Classes.Set("current", card?.PlayMark == YarnPlayMark.Current);
        container.Classes.Set("visited", card?.PlayMark == YarnPlayMark.Visited);
        container.Classes.Set("stub", stub is not null);
        container.Classes.Set("broken", stub?.IsBroken == true);
        container.Classes.Set("computed", stub?.IsComputed == true);
    }

    private void OnNodesMoved(object? sender, NodeGraphNodesMovedEventArgs e)
    {
        var moves = new List<YarnCardMove>();
        foreach (var move in e.Moves)
        {
            if (move.Node is YarnNodeCardViewModel card)
            {
                moves.Add(new YarnCardMove(card, new YarnGraphPoint((int)Math.Round(move.NewPosition.X), (int)Math.Round(move.NewPosition.Y))));
            }
        }

        editor.Move(moves);

        // The text has whole numbers, and the place of a stub is worked out from its card: the graph shows what the editor has
        foreach (var move in e.Moves)
        {
            Show(move.Node);
        }

        Changed?.Invoke();
    }

    private void OnNodeDoubleClicked(object? sender, NodeGraphNodeEventArgs e)
    {
        switch (e.Node)
        {
            case YarnStubCardViewModel stub:
                editor.OpenTargetCommand.Execute(stub);
                e.Handled = true;
                break;
            case YarnNodeCardViewModel card:
                RenameRequested?.Invoke(card);
                e.Handled = true;
                break;
        }
    }

    // The first time the graph has its size and its nodes theirs, it shows them all
    // A pan or a zoom can put a card out of view, or bring it back: the links whose label shows then change
    private void OnGraphPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != NodeGraphControl.ZoomProperty && e.Property != NodeGraphControl.OffsetProperty && e.Property != Visual.BoundsProperty)
            return;

        var labelled = editor.Links.Where(ShowsLabel).ToHashSet();
        if (labelled.SetEquals(Labelled))
            return;

        Labelled = labelled;
        graph.InvalidateLinks();
    }

    private Rect? BoundsOf(object node)
        => graph.GetNodePosition(node) is { } position && graph.ContainerFromNode(node) is { Bounds.Width: > 0 } container
            ? new Rect(position, container.Bounds.Size)
            : null;

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (graph.Bounds.Width <= 0 || editor.Nodes.Count == 0 || graph.ContainerFromNode(editor.Nodes[0]) is not { Bounds.Width: > 0 })
            return;

        // The first time, and when the size of the graph changes (the list of problems comes and goes) while it shows what the last fit gave
        if (fitted && (graph.Bounds.Size == fit.Size || graph.Zoom != fit.Zoom || graph.Offset != fit.Offset))
            return;

        fitted = true;
        fit = fit with { Size = graph.Bounds.Size };
        Dispatcher.UIThread.Post(FitToView, DispatcherPriority.Background);
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
