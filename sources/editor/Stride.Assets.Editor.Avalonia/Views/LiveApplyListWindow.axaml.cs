// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Interactivity;
using Stride.Assets.Editor.PlayMode;

namespace Stride.Assets.Editor.Avalonia.Views;

/// <summary>
/// The window of the list of live values to apply to the assets (<see cref="LiveApplyListViewModel"/>): it closes with
/// <c>true</c> when the user applies the checked values.
/// </summary>
public sealed partial class LiveApplyListWindow : Window
{
    public LiveApplyListWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Gets the view model of the list.
    /// </summary>
    public LiveApplyListViewModel? List => DataContext as LiveApplyListViewModel;

    /// <summary>
    /// Gets whether the window closed on Apply, shown modally or not.
    /// </summary>
    public bool Applied { get; private set; }

    // A value that cannot be applied is disabled, with the reason; each row's tooltip says who changed the value
    private void OnRowPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        if ((e.Index >= 0 && e.Index < ItemList.ItemsView.Count ? ItemList.ItemsView[e.Index] : null) is not LiveApplyListItem row || e.Container is not ListBoxItem item)
            return;
        item.IsEnabled = row.CanApply;
        item.Classes.Set("byGame", !row.IsEditedLive);
        ToolTip.SetTip(item, row.ToolTip);
        ToolTip.SetShowOnDisabled(item, true);
    }

    private void OnAllClick(object? sender, RoutedEventArgs e)
    {
        List?.ToggleAll();
        AllCheckBox.IsChecked = List is { } list && list.Items.Where(x => x.CanApply).All(x => x.IsChecked) && list.CheckedCount > 0;
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        Applied = List?.CanApply == true;
        Close(Applied);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
