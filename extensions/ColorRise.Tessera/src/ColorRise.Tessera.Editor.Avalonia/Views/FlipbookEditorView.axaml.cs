using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Selection;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ColorRise.Tessera.Editor.FlipbookEditor;
using Stride.Core.Assets.Editor.Annotations;
using Stride.Core.Assets.Editor.Editors;

namespace ColorRise.Tessera.Editor.Avalonia.Views;

/// <summary>
/// The view of the flipbook editor: the tree of the animations on the left, the preview in the middle with the Sheet bar and
/// the strip of frames under it, the palette on the right, and the problems at the bottom.
/// </summary>
[AssetEditorView<FlipbookEditorViewModel>]
public sealed partial class FlipbookEditorView : UserControl, IAssetEditorView
{
    // How far the pointer goes, in pixels, before a press on the strip is a drag
    private const double DragDistance = 4;

    // The editor whose changes the view follows
    private FlipbookEditorViewModel? editor;
    // The selection of the strip and the editor's are being made the same
    private bool syncingSelection;
    // The strip's selection is made the editor's again once its frames have changed
    private bool selectionPosted;
    // A press on a frame of the strip, until its release
    private StripPress? press;

    /// <summary>
    /// Makes the view of a flipbook editor.
    /// </summary>
    public FlipbookEditorView()
    {
        InitializeComponent();
        // A click on a button of the strip gives the focus to the strip, where Delete removes the selected frames; a click on
        // another button gives it back to the preview; the menu of directions keeps it while it is open
        AddHandler(Button.ClickEvent, (_, e) =>
        {
            if (e.Source is Visual source && StripBar.IsVisualAncestorOf(source))
            {
                FrameStrip.Focus();
            }
            else if (e.Source != AddClipButton)
            {
                Canvas.Focus();
            }
        }, RoutingStrategies.Bubble, handledEventsToo: true);
        FrameStrip.SelectionChanged += OnStripSelectionChanged;
        FrameStrip.AddHandler(PointerPressedEvent, OnStripPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        FrameStrip.AddHandler(PointerMovedEvent, OnStripPointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        FrameStrip.AddHandler(PointerReleasedEvent, OnStripPointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        FrameStrip.AddHandler(PointerCaptureLostEvent, (_, _) => press = null, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    /// <summary>
    /// Moves the selected frames to where a drag along the strip is let go: before the frame under the pointer when that frame
    /// is left of them, after it when it is right of them, and to the end past the last frame.
    /// </summary>
    /// <param name="x">Where the drag is let go, in pixels from the left of the strip.</param>
    internal void MoveSelectedFramesTo(double x)
    {
        if (editor is not { SelectedFrames.Count: > 0 } current || current.Frames.Count == 0)
        {
            return;
        }
        var first = current.SelectedFrames.Min(frame => frame.Index);
        for (var index = 0; index < current.Frames.Count; index++)
        {
            if (FrameStrip.ContainerFromIndex(index) is not { } container || container.TranslatePoint(default, FrameStrip) is not { } corner)
            {
                continue;
            }
            if (x < corner.X + container.Bounds.Width || index == current.Frames.Count - 1)
            {
                var over = current.Frames[index].Index;
                current.MoveSelectedFrames(over > first || x >= corner.X + container.Bounds.Width ? over + 1 : over);
                return;
            }
        }
    }

    /// <inheritdoc/>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (editor is not null)
        {
            editor.PropertyChanged -= EditorPropertyChanged;
            ((INotifyCollectionChanged)editor.Frames).CollectionChanged -= FramesChanged;
        }
        editor = DataContext as FlipbookEditorViewModel;
        if (editor is not null)
        {
            editor.PropertyChanged += EditorPropertyChanged;
            ((INotifyCollectionChanged)editor.Frames).CollectionChanged += FramesChanged;
        }
        UpdateAddClip();
        SelectionToStrip();
    }

    /// <inheritdoc/>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        Canvas.Focus();
    }

    private void EditorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(FlipbookEditorViewModel.SelectedFrames):
                SelectionToStrip();
                break;
            case nameof(FlipbookEditorViewModel.SelectedAnimation):
            case nameof(FlipbookEditorViewModel.FreeDirections):
                UpdateAddClip();
                break;
            case nameof(FlipbookEditorViewModel.SelectedClip):
                // Another clip shows whole, once the preview has it
                Dispatcher.UIThread.Post(() => Canvas.Fit());
                break;
        }
    }

    private void UpdateAddClip() => AddClipButton.IsEnabled = editor is { SelectedAnimation: not null, FreeDirections.Count: > 0 };

    // The menu of the directions the animation lacks
    private void OnAddClip(object? sender, RoutedEventArgs e)
    {
        if (editor is null)
        {
            return;
        }
        var menu = new MenuFlyout();
        foreach (var direction in editor.FreeDirections)
        {
            menu.Items.Add(new MenuItem { Header = FlipbookClipItemViewModel.LabelOf(direction), Command = editor.AddClipCommand, CommandParameter = direction });
        }
        menu.ShowAt(AddClipButton);
    }

    // A double click on an animation's name types it in place
    private void OnAnimationNameDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: FlipbookAnimationItemViewModel animation })
        {
            animation.IsEditing = true;
            e.Handled = true;
        }
    }

    private void OnStripSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (syncingSelection || editor is null)
        {
            return;
        }
        // Frames that leave the strip leave the editor's selection by themselves
        if (e.AddedItems.Count == 0 && e.RemovedItems.Cast<object>().All(item => item is FlipbookFrameItemViewModel frame && !editor.Frames.Contains(frame)))
        {
            return;
        }
        syncingSelection = true;
        try
        {
            editor.SelectedFrames = [.. FrameStrip.SelectedItems?.OfType<FlipbookFrameItemViewModel>() ?? []];
        }
        finally
        {
            syncingSelection = false;
        }
    }

    // A move takes frames out of the strip before it puts them back: the strip selects them again once it is over
    private void FramesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (selectionPosted)
        {
            return;
        }
        selectionPosted = true;
        Dispatcher.UIThread.Post(() =>
        {
            selectionPosted = false;
            SelectionToStrip();
        });
    }

    private void SelectionToStrip()
    {
        if (syncingSelection || editor is null)
        {
            return;
        }
        var frames = editor.Frames.ToList();
        var wanted = editor.SelectedFrames.Select(frame => frames.IndexOf(frame)).Where(index => index >= 0).Order().ToList();
        var selection = FrameStrip.Selection;
        if (selection.SelectedIndexes.Order().SequenceEqual(wanted))
        {
            return;
        }
        syncingSelection = true;
        try
        {
            using (selection.BatchUpdate())
            {
                selection.Clear();
                foreach (var index in wanted)
                {
                    selection.Select(index);
                }
            }
        }
        finally
        {
            syncingSelection = false;
        }
    }

    // A press on a selected frame among several keeps them selected, so that they can be dragged together
    private void OnStripPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        press = null;
        if (editor is null || !e.GetCurrentPoint(FrameStrip).Properties.IsLeftButtonPressed
            || (e.Source as Visual)?.FindAncestorOfType<ListBoxItem>(includeSelf: true) is not { DataContext: FlipbookFrameItemViewModel frame } container)
        {
            return;
        }
        var keeps = e.KeyModifiers == KeyModifiers.None && editor.SelectedFrames.Count > 1 && editor.SelectedFrames.Contains(frame);
        if (keeps)
        {
            e.Handled = true;
            container.Focus();
        }
        press = new StripPress(frame, e.GetPosition(FrameStrip), keeps);
    }

    private void OnStripPointerMoved(object? sender, PointerEventArgs e)
    {
        if (press is not { Moved: false } held)
        {
            return;
        }
        var position = e.GetPosition(FrameStrip);
        if (Math.Abs(position.X - held.Start.X) > DragDistance || Math.Abs(position.Y - held.Start.Y) > DragDistance)
        {
            press = held with { Moved = true };
        }
    }

    private void OnStripPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (press is not { } held || e.InitialPressMouseButton != MouseButton.Left)
        {
            return;
        }
        press = null;
        if (held.Moved)
        {
            MoveSelectedFramesTo(e.GetPosition(FrameStrip).X);
            e.Handled = true;
        }
        else if (held.KeptSelection && editor is not null)
        {
            // A click without a drag selects that frame only
            editor.SelectedFrames = [held.Frame];
        }
    }

    // Where a press on the strip started, on which frame, whether it kept a selection of several, and whether it became a drag
    private sealed record StripPress(FlipbookFrameItemViewModel Frame, Point Start, bool KeptSelection, bool Moved = false);
}
