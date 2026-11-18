// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Stride.Core.Assets.Editor.ViewModels;
using Stride.Core.Assets.Presentation.ViewModels;

namespace Stride.Core.Assets.Editor.Avalonia.Views;

/// <summary>
/// The window of the asset picker (<see cref="AssetPickerViewModel"/>): it closes with <c>true</c> when the user picks an asset.
/// </summary>
public sealed partial class AssetPickerWindow : Window
{
    public AssetPickerWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Gets the view model of the picker.
    /// </summary>
    public AssetPickerViewModel? Picker => DataContext as AssetPickerViewModel;

    /// <inheritdoc/>
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        // Show the initial asset, and let the user type a search at once
        if (Picker?.SelectedAsset is { } selectedAsset)
            Assets.ScrollIntoView(selectedAsset);
        SearchBox.Focus();
    }

    private void OnAssetDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (Picker?.CanPick == true)
            Close(true);
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        Close(Picker?.CanPick == true);
    }

    // An asset that cannot be picked is listed disabled, with the reason in its tooltip; one with a note is marked
    private void OnAssetContainerPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        if (Picker is not { } picker || (e.Index >= 0 && e.Index < Assets.ItemsView.Count ? Assets.ItemsView[e.Index] : null) is not AssetViewModel asset)
            return;
        var reason = picker.GetDisabledReason(asset);
        var note = picker.GetNote(asset);
        e.Container.IsEnabled = reason is null;
        e.Container.Classes.Set("noted", note is not null);
        ToolTip.SetTip(e.Container, reason is not null ? $"{asset.Url}: {reason}" : note is not null ? $"{asset.Url}: {note}" : asset.Url);
        ToolTip.SetShowOnDisabled(e.Container, true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
