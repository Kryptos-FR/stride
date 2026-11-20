// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core.Assets;

namespace Stride.Assets.Presentation.ViewModels;

/// <summary>
/// A source code asset that a text editor edits: the editor writes the text to the file itself, and tells the asset whether
/// it has edits that are not saved yet.
/// </summary>
public interface ISourceCodeViewModel
{
    /// <summary>
    /// Gets the source code asset.
    /// </summary>
    SourceCodeAsset SourceCode { get; }

    /// <summary>
    /// Gets whether the editor of the file has edits of its text that are not saved to the file.
    /// </summary>
    bool HasUnsavedText { get; }

    /// <summary>
    /// Records whether the editor of the file has edits of its text that are not saved to the file.
    /// </summary>
    /// <param name="value"><c>true</c> when the text has unsaved edits.</param>
    void SetHasUnsavedText(bool value);
}
