// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core;
using Stride.Core.Assets;

namespace Stride.AvaloniaUI.Assets;

/// <summary>
/// An Avalonia XAML file of a game's project, such as a view next to its code-behind: Avalonia's XAML compiler builds it into
/// the game's assembly, so the asset has no compiled content of its own.
/// </summary>
[DataContract("AvaloniaXamlAsset")]
[AssetDescription(FileExtension, AlwaysMarkAsRoot = true, AllowArchetype = false, Referenceable = false)]
[Display("Avalonia XAML")]
public sealed class AvaloniaXamlAsset : ProjectSourceCodeAsset
{
    /// <summary>
    /// The extension of Avalonia XAML files.
    /// </summary>
    public const string FileExtension = ".axaml";
}
