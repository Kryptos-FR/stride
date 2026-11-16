using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using ColorRise.Skein.Assets.Compilation;
using ColorRise.Skein.Editor.Graph;
using ColorRise.Skein.Editor.ViewModels;
using Stride.Core.Assets.Editor.Annotations;
using Stride.Core.Assets.Editor.Editors;
using Stride.Core.Presentation.Avalonia.Controls;

namespace ColorRise.Skein.Editor.Avalonia.Views;

/// <summary>
/// The view of the Yarn script editor: the graph of the script's nodes, the text of the selected nodes or of the whole
/// file, and what the compiler says of the script's project.
/// </summary>
[AssetEditorView<YarnScriptEditorViewModel>]
public sealed partial class YarnScriptEditorView : UserControl, IAssetEditorView
{
    // What the choice of no header is called in the list of the headers to group by
    private const string NoGroup = "(none)";
    // Half the width of a card and about half its height: a node added from the toolbar is centred in the graph
    private static readonly Vector CardCentre = new(120, 50);

    private YarnScriptEditorViewModel? editor;
    private YarnGraphBinding? binding;
    private bool fillingGroupBy;

    // What the graph's menu was opened on, and where
    private object? menuNode;
    private Point menuPlace;

    public YarnScriptEditorView()
    {
        InitializeComponent();
        foreach (var name in YarnPalette.ColorNames)
        {
            var item = new MenuItem { Header = char.ToUpperInvariant(name[0]) + name[1..], Tag = name, Icon = new Border { Width = 12, Height = 12, CornerRadius = new CornerRadius(2), Background = YarnPalette.HeaderOf(name, false) } };
            item.Click += OnColorClick;
            ColorItem.Items.Add(item);
        }

        var none = new MenuItem { Header = "None" };
        none.Click += OnColorClick;
        ColorItem.Items.Add(none);

        // Before the graph opens its menu: the menu shows what fits the place of the click
        Graph.AddHandler(ContextRequestedEvent, OnGraphContextRequested, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
    }

    /// <summary>
    /// The graph of the nodes.
    /// </summary>
    public NodeGraphControl NodeGraph => Graph;

    /// <summary>
    /// The text of the script.
    /// </summary>
    public YarnTextPane Text => TextPane;

    /// <summary>
    /// Opens the field that renames the node of a card, at the card: Enter renames, and a title the node cannot have
    /// keeps the field open with the reason.
    /// </summary>
    /// <returns>The field, or null when the card is not on the graph.</returns>
    public TextBox? BeginRename(YarnNodeCardViewModel card)
    {
        if (editor is null || Graph.ContainerFromNode(card) is not { } container)
            return null;

        var box = new TextBox { Text = card.Title, MinWidth = 220 };
        var reason = new TextBlock { IsVisible = false, MaxWidth = 300, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0), Foreground = YarnPalette.Error };
        var flyout = new Flyout
        {
            Placement = PlacementMode.Top,
            Content = new StackPanel { Orientation = Orientation.Vertical, Children = { box, reason } },
        };
        box.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter)
                return;

            e.Handled = true;
            if (editor.Rename(card, box.Text ?? ""))
            {
                flyout.Hide();
            }
            else
            {
                reason.Text = editor.Problem;
                reason.IsVisible = true;
            }
        };
        flyout.Opened += (_, _) =>
        {
            box.Focus();
            box.SelectAll();
        };
        flyout.ShowAt(container);
        return box;
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (editor is not null)
        {
            editor.PropertyChanged -= OnEditorPropertyChanged;
        }

        if (binding is not null)
        {
            binding.RenameRequested -= OnRenameRequested;
            binding.Changed -= Decorations.InvalidateVisual;
            binding.Dispose();
        }

        editor = DataContext as YarnScriptEditorViewModel;
        binding = editor is null ? null : new YarnGraphBinding(Graph, editor);
        Decorations.Attach(editor is null ? null : Graph, editor);
        if (editor is not null && binding is not null)
        {
            editor.PropertyChanged += OnEditorPropertyChanged;
            binding.RenameRequested += OnRenameRequested;
            binding.Changed += Decorations.InvalidateVisual;
        }

        FillGroupBy();
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        // The editor takes the focus so that Delete and F2 work
        if (!IsKeyboardFocusWithin)
        {
            Focus();
        }
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key == Key.F2 && e.KeyModifiers == KeyModifiers.None && !TextPane.IsKeyboardFocusWithin
            && editor?.SelectedCards is [YarnNodeCardViewModel card])
        {
            BeginRename(card);
            e.Handled = true;
        }
    }

    private void OnEditorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(YarnScriptEditorViewModel.GroupableHeaders):
            case nameof(YarnScriptEditorViewModel.GroupBy):
                FillGroupBy();
                Decorations.InvalidateVisual();
                break;
        }
    }

    private void OnRenameRequested(YarnNodeCardViewModel card) => BeginRename(card);

    // The headers of the script to group by, after the choice of none
    private void FillGroupBy()
    {
        fillingGroupBy = true;
        try
        {
            GroupByBox.ItemsSource = new List<string>([NoGroup, .. editor?.GroupableHeaders ?? []]);
            GroupByBox.SelectedItem = editor?.GroupBy ?? NoGroup;
        }
        finally
        {
            fillingGroupBy = false;
        }
    }

    private void OnGroupBySelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!fillingGroupBy && editor is not null)
        {
            editor.GroupBy = GroupByBox.SelectedItem is string header && header != NoGroup ? header : null;
        }
    }

    private YarnGraphPoint GraphCentre()
    {
        var centre = Graph.ViewToGraph(new Point(Graph.Bounds.Width / 2, Graph.Bounds.Height / 2)) - CardCentre;
        return new YarnGraphPoint((int)Math.Round(centre.X), (int)Math.Round(centre.Y));
    }

    private void OnAddNodeClick(object? sender, RoutedEventArgs e) => editor?.AddNode(GraphCentre());

    private void OnAddNoteClick(object? sender, RoutedEventArgs e) => editor?.AddNote(GraphCentre());

    private void OnGraphContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        var container = (e.Source as Visual)?.GetSelfAndVisualAncestors().OfType<NodeGraphNode>().FirstOrDefault();
        menuNode = container?.Node;
        menuPlace = e.TryGetPosition(Graph, out var point) ? Graph.ViewToGraph(point) : Graph.ViewToGraph(new Point(Graph.Bounds.Width / 2, Graph.Bounds.Height / 2));
    }

    // On a card: what is done to the selected nodes. On a stub: its script. Elsewhere: what is done to the graph
    private void OnGraphMenuOpening(object? sender, CancelEventArgs e)
    {
        var onCard = menuNode is YarnNodeCardViewModel;
        var onStub = menuNode is YarnStubCardViewModel { IsBroken: false };
        var onGraph = menuNode is null;
        OpenItem.IsVisible = onStub;
        RenameItem.IsVisible = onCard;
        DuplicateItem.IsVisible = onCard;
        DeleteItem.IsVisible = onCard;
        ColorItem.IsVisible = onCard;
        AddToGroupItem.IsVisible = onCard;
        AddNodeItem.IsVisible = onGraph;
        AddNoteItem.IsVisible = onGraph;
        LayoutItem.IsVisible = onGraph;
        FitItem.IsVisible = onGraph;
        // A stub of a node that is nowhere has nothing to offer
        e.Cancel = !onCard && !onStub && !onGraph;
    }

    private YarnGraphPoint MenuPlace() => new((int)Math.Round(menuPlace.X), (int)Math.Round(menuPlace.Y));

    private void OnOpenClick(object? sender, RoutedEventArgs e)
    {
        if (menuNode is YarnStubCardViewModel stub)
        {
            editor?.OpenTargetCommand.Execute(stub);
        }
    }

    private void OnRenameClick(object? sender, RoutedEventArgs e)
    {
        if (menuNode is YarnNodeCardViewModel card)
        {
            BeginRename(card);
        }
    }

    private void OnColorClick(object? sender, RoutedEventArgs e) => editor?.SetColor((sender as MenuItem)?.Tag as string);

    private void OnAddToGroupClick(object? sender, RoutedEventArgs e)
    {
        if (menuNode is YarnNodeCardViewModel card)
        {
            editor?.AddToGroup(card);
        }
    }

    private void OnAddNodeHereClick(object? sender, RoutedEventArgs e) => editor?.AddNode(MenuPlace());

    private void OnAddNoteHereClick(object? sender, RoutedEventArgs e) => editor?.AddNote(MenuPlace());

    private void OnFitClick(object? sender, RoutedEventArgs e) => Graph.FitToView();

    private void OnProblemDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (Problems.SelectedItem is YarnDiagnostic diagnostic)
        {
            _ = editor?.GoTo(diagnostic);
        }
    }
}
