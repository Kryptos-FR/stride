// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Interactivity;
using Stride.Assets.Editor.Templates;

namespace Stride.Assets.Editor.Avalonia.Templates;

/// <summary>
/// The <em>Template packages</em> window of the start screen (<see cref="TemplatePackagesViewModel"/>): the third-party template
/// packages found, with <em>Remove</em>, and the ways to add one.
/// </summary>
public sealed partial class TemplatePackagesWindow : Window
{
    public TemplatePackagesWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Gets the packages shown.
    /// </summary>
    public TemplatePackagesViewModel? Packages => DataContext as TemplatePackagesViewModel;

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }
}
