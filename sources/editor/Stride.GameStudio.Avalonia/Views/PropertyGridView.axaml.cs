// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.VisualTree;
using Stride.Assets.Editor.PlayMode;
using Stride.Core.Assets.Editor.Components.Properties;
using Stride.Core.Assets.Editor.Quantum.NodePresenters.Commands;
using Stride.Core.Assets.Editor.ViewModels;
using Stride.Core.Presentation.Avalonia.Commands;
using Stride.Core.Presentation.Avalonia.Controls;
using Stride.Core.Presentation.Quantum.Presenters;
using Stride.Core.Presentation.Quantum.ViewModels;
using Stride.Core.Quantum;

namespace Stride.GameStudio.Avalonia.Views;

public partial class PropertyGridView : UserControl
{
    public PropertyGridView()
    {
        InitializeComponent();
        // The event is declared as a non-generic RoutedEvent, which the XAML compiler cannot wire to a typed handler
        Properties.PrepareItem += OnPrepareItem;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        ShowLive(DataContext as LivePropertiesViewModel);
    }

    /// <summary>
    /// The live grid of a running game gets its header ("Live", the lock of its edits, the refresh status, what the edits do),
    /// and asks the view which values not to overwrite: the one under the pointer and the one with the keyboard focus, so that
    /// the user can read, copy or type them.
    /// </summary>
    private void ShowLive(LivePropertiesViewModel? live)
    {
        LiveBadge.DataContext = live;
        LiveEditsToggle.DataContext = live;
        LiveStatus.DataContext = live;
        LiveNotice.DataContext = live;
        if (live is not null)
            live.ProtectedValues = GetProtectedValues;
    }

    /// <summary>
    /// The value nodes of the property under the pointer and of the property with the keyboard focus, with the nodes their row
    /// shows without a row of their own (an inlined value, the X, Y and Z of a vector).
    /// </summary>
    private IReadOnlyCollection<(IGraphNode Node, NodeIndex Index)> GetProtectedValues()
    {
        var nodes = new HashSet<(IGraphNode, NodeIndex)>();
        Add(Properties.HoveredItem);
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
        if (focused is not null && focused.FindAncestorOfType<PropertyView>(includeSelf: true) == Properties)
            Add(focused.FindAncestorOfType<PropertyViewItem>(includeSelf: true));
        return nodes;

        void Add(PropertyViewItem? item)
        {
            if (item?.DataContext is NodeViewModel viewModel)
                AddPresenters(viewModel.NodePresenters);
        }

        void AddPresenters(IEnumerable<INodePresenter> presenters)
        {
            foreach (var presenter in presenters)
            {
                var accessor = presenter.GetNodeAccessor();
                if (accessor.Node is not null)
                    nodes.Add((accessor.Node, accessor.Index));
                AddPresenters(presenter.Children.Where(x => !x.IsVisible));
            }
        }
    }

    /// <summary>
    /// Enter in the "Add new tag" box adds the tag to the selected assets and clears the box.
    /// </summary>
    private void OnNewTagKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not TextBox textBox)
            return;

        if (DataContext is SessionObjectPropertiesViewModel { Session: SessionViewModel { AssetTags.AddTagCommand: { } addTag } } && addTag.CanExecute(textBox.Text))
        {
            addTag.Execute(textBox.Text);
        }
        textBox.Clear();
        e.Handled = true;
    }

    /// <summary>
    /// Copy and Paste from the Edit menu, or their shortcuts, work on the property that has the focus: each property gets the
    /// bindings of its own commands, which follow the property it shows.
    /// </summary>
    private void OnPrepareItem(object? sender, PropertyViewItemEventArgs e)
    {
        var bindings = EditCommands.GetBindings(e.Container);
        if (bindings.Count > 0)
            return;

        bindings.Add(NodeCommandBinding(e.Container, EditCommandKind.Copy, CopyPropertyCommand.CommandName));
        bindings.Add(NodeCommandBinding(e.Container, EditCommandKind.Paste, PastePropertyCommand.CommandName));
        return;

        static EditCommandBinding NodeCommandBinding(PropertyViewItem item, EditCommandKind kind, string commandName)
        {
            var binding = new EditCommandBinding { Kind = kind };
            binding.Bind(EditCommandBinding.CommandProperty, new Binding($"{nameof(DataContext)}[{commandName}]") { Source = item });
            return binding;
        }
    }
}
