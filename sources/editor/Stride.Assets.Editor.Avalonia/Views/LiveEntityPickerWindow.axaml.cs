// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Stride.Assets.Editor.PlayMode;

namespace Stride.Assets.Editor.Avalonia.Views;

/// <summary>
/// The window of the picker of a live reference (<see cref="LiveEntityPickerViewModel"/>): it closes with <c>true</c> when the
/// user picks an entity, or a component.
/// </summary>
public sealed partial class LiveEntityPickerWindow : Window
{
    public LiveEntityPickerWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Gets the view model of the picker.
    /// </summary>
    public LiveEntityPickerViewModel? Picker => DataContext as LiveEntityPickerViewModel;

    /// <summary>
    /// Gets whether the window closed on a pick, shown modally or not.
    /// </summary>
    public bool Picked { get; private set; }

    /// <inheritdoc/>
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (Picker?.SelectedRow is { } selected)
            EntityList.ScrollIntoView(selected);
        SearchBox.Focus();
    }

    // The rows are indented by their depth; scenes, and entities that cannot be picked, are disabled, with the reason
    private void OnRowPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        if (EntityList.ItemFromContainer(e.Container) is not LivePickerRow row || e.Container is not ListBoxItem item)
            return;
        item.Padding = new Thickness(6 + 14 * row.Depth, 2, 6, 2);
        item.IsEnabled = row.IsPickable;
        item.Classes.Set("scene", row.IsScene);
        ToolTip.SetTip(item, row.DisabledReason);
        ToolTip.SetShowOnDisabled(item, true);
    }

    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (Picker?.CanPick == true)
            ClosePicked();
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        if (Picker?.CanPick == true)
            ClosePicked();
        else
            Close(false);
    }

    private void ClosePicked()
    {
        Picked = true;
        Close(true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
