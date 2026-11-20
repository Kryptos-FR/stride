// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Assets.Presentation.ViewModels;
using Stride.Core.Assets.Editor.Annotations;

namespace Stride.Assets.Editor.ViewModels;

/// <summary>
/// The editor of a source code asset that has no editor of its own, such as the Avalonia XAML file of a plugin: its text is
/// highlighted by the extension of its file.
/// </summary>
[AssetEditorViewModel<SourceCodeAssetViewModel>]
public sealed class SourceCodeAssetEditorViewModel : SourceCodeEditorViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SourceCodeAssetEditorViewModel"/> class.
    /// </summary>
    /// <param name="asset">The source code asset to edit.</param>
    public SourceCodeAssetEditorViewModel(SourceCodeAssetViewModel asset)
        : base(asset)
    {
    }

    /// <inheritdoc />
    public override SourceCodeAssetViewModel Asset => (SourceCodeAssetViewModel)base.Asset;
}
