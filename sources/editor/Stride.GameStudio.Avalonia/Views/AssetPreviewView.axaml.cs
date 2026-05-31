// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;

namespace Stride.GameStudio.Avalonia.Views;

/// <summary>
/// Hosts the asset preview produced by the <see cref="ViewModels.PreviewViewModel"/> exposed on the
/// <see cref="ViewModels.MainViewModel"/>. The content and placeholder are bound declaratively, so this
/// view has no behavior of its own.
/// </summary>
public partial class AssetPreviewView : UserControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AssetPreviewView"/> class.
    /// </summary>
    public AssetPreviewView()
    {
        InitializeComponent();
    }
}
