// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Assets.Presentation.ViewModels;
using Stride.Core.Assets.Editor.Annotations;

namespace Stride.Assets.Editor.ViewModels;

/// <summary>
/// The editor of a script.
/// </summary>
[AssetEditorViewModel<ScriptSourceFileViewModel>]
public sealed class ScriptSourceFileEditorViewModel : SourceCodeEditorViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ScriptSourceFileEditorViewModel"/> class.
    /// </summary>
    /// <param name="asset">The script to edit.</param>
    public ScriptSourceFileEditorViewModel(ScriptSourceFileViewModel asset)
        : base(asset)
    {
    }

    /// <inheritdoc />
    public override ScriptSourceFileViewModel Asset => (ScriptSourceFileViewModel)base.Asset;
}
