// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;
using Stride.Editor.PlayMode;

namespace Stride.GameStudio.Avalonia.Views.Panels;

public sealed partial class LiveView : UserControl
{
    /// <summary>
    /// The live entities dragged in the tree, to move them under another row.
    /// </summary>
    public static readonly DataFormat<LiveEntityItem> EntityFormat = DataFormat.CreateInProcessFormat<LiveEntityItem>("Stride.LiveEntityItem");

    private const double DragThreshold = 4.0;
    private Point? dragStartPoint;
    private PointerPressedEventArgs? pressedArgs;

    public LiveView()
    {
        InitializeComponent();
        var tree = LiveTree;
        DragDrop.SetAllowDrop(tree, true);
        tree.AddHandler(PointerPressedEvent, OnTreePointerPressed, handledEventsToo: true);
        tree.AddHandler(PointerMovedEvent, OnTreePointerMoved, handledEventsToo: true);
        tree.AddHandler(PointerReleasedEvent, (_, _) => ResetDrag(), handledEventsToo: true);
        tree.AddHandler(DragDrop.DragEnterEvent, OnTreeDragOver);
        tree.AddHandler(DragDrop.DragOverEvent, OnTreeDragOver);
        tree.AddHandler(DragDrop.DropEvent, OnTreeDrop);
    }

    /// <summary>
    /// Moves the entities under the row: under an entity, or to the root of a scene; Alt keeps their place in the world, as in the
    /// scene editor.
    /// </summary>
    /// <returns>Whether the move was sent.</returns>
    internal bool Drop(IReadOnlyCollection<LiveEntityItem> items, LiveTreeItem? target, KeyModifiers modifiers)
    {
        if (DataContext is not LivePanelViewModel viewModel || target is null || !viewModel.CanMove(items, target))
            return false;
        _ = viewModel.MoveAsync(items, target, keepWorldTransform: modifiers.HasFlag(KeyModifiers.Alt));
        return true;
    }

    /// <summary>
    /// Coming back to the tree shows its selection in the property grid again, after another selection took it.
    /// </summary>
    private void OnTreeGotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (DataContext is LivePanelViewModel { Selection: not null } viewModel)
            viewModel.ShowSelectionCommand.Execute();
    }

    private void OnTreePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // A drag starts from an entity's row, not from the expander, the scroll bar or an empty part of the tree
        var row = (e.Source as Visual)?.FindAncestorOfType<ListBoxItem>(includeSelf: true);
        var onExpander = (e.Source as Visual)?.FindAncestorOfType<ToggleButton>(includeSelf: true) is not null;
        if (row?.DataContext is LiveEntityItem && !onExpander && e.GetCurrentPoint(LiveTree).Properties.IsLeftButtonPressed)
        {
            dragStartPoint = e.GetPosition(LiveTree);
            pressedArgs = e;
        }
        else
        {
            ResetDrag();
        }
    }

    private async void OnTreePointerMoved(object? sender, PointerEventArgs e)
    {
        if (dragStartPoint is not { } start || pressedArgs is not { } savedArgs || DataContext is not LivePanelViewModel { CanChangeStructure: true } viewModel)
            return;
        var current = e.GetPosition(LiveTree);
        if (Math.Abs(current.X - start.X) < DragThreshold && Math.Abs(current.Y - start.Y) < DragThreshold)
            return;
        ResetDrag();
        var entities = viewModel.SelectedItems.OfType<LiveEntityItem>().ToList();
        if (entities.Count == 0)
            return;
        var data = new DataTransfer();
        foreach (var entity in entities)
        {
            var item = new DataTransferItem();
            item.Set(EntityFormat, entity);
            data.Add(item);
        }
        try
        {
            await DragDrop.DoDragDropAsync(savedArgs, data, DragDropEffects.Move);
        }
        catch (Exception)
        {
            // A drag the platform refuses (no drag source on a headless host) leaves the tree as it was
        }
    }

    private void OnTreeDragOver(object? sender, DragEventArgs e)
    {
        var items = DraggedEntities(e.DataTransfer);
        var target = RowAt(e);
        e.DragEffects = items.Count > 0 && target is not null && DataContext is LivePanelViewModel viewModel && viewModel.CanMove(items, target) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnTreeDrop(object? sender, DragEventArgs e)
    {
        if (Drop(DraggedEntities(e.DataTransfer), RowAt(e), e.KeyModifiers))
        {
            e.DragEffects = DragDropEffects.Move;
            e.Handled = true;
        }
    }

    private static LiveTreeItem? RowAt(DragEventArgs e) => (e.Source as Visual)?.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.DataContext as LiveTreeItem;

    private static List<LiveEntityItem> DraggedEntities(IDataTransfer? data) => data?.TryGetValues(EntityFormat) is { Length: > 0 } items ? [.. items.OfType<LiveEntityItem>()] : [];

    private void ResetDrag()
    {
        dragStartPoint = null;
        pressedArgs = null;
    }
}
