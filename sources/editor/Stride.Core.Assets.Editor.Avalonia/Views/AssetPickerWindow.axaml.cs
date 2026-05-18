// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Interactivity;
using Stride.Core.Assets.Editor.ViewModels;

namespace Stride.Core.Assets.Editor.Avalonia.Views;

public partial class AssetPickerWindow : Window
{
    public AssetPickerWindow()
    {
        InitializeComponent();
    }

    public AssetPickerViewModel? PickerViewModel => DataContext as AssetPickerViewModel;

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
