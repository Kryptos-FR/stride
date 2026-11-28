// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Microsoft.TemplateEngine.Abstractions;
using Stride.Core.Presentation.ViewModels;

namespace Stride.Assets.Editor.Templates;

/// <summary>
/// One asset pack (an item template of the AssetPacks package) offered by the parameters dialog of a game template. None is
/// checked by default.
/// </summary>
public sealed class AssetPackViewModel : ViewModelBase
{
    private bool isChecked;

    /// <param name="template">The item template of the pack.</param>
    /// <param name="packageId">The id of the third-party package the pack comes from, null for Stride's.</param>
    public AssetPackViewModel(ITemplateInfo template, string? packageId = null)
    {
        ArgumentNullException.ThrowIfNull(template);
        Identity = template.Identity;
        PackageId = packageId;
        Name = template.Name;
        Description = template.Description;
        Label = string.IsNullOrEmpty(template.Description) ? template.Name : $"{template.Name} — {template.Description}";
    }

    /// <summary>
    /// The identity of the pack's template, passed to the generator when the pack is checked.
    /// </summary>
    public string Identity { get; }

    /// <summary>
    /// The id of the third-party package the pack comes from, null for Stride's.
    /// </summary>
    public string? PackageId { get; }

    public string Name { get; }

    public string? Description { get; }

    public string Label { get; }

    public bool IsChecked
    {
        get => isChecked;
        set => SetValue(ref isChecked, value);
    }
}

/// <summary>
/// The asset packs of one package, in the parameters dialog of a game template.
/// </summary>
/// <param name="Header">The package id, empty for Stride's packs (shown without a header).</param>
/// <param name="Packs">The packs, by name.</param>
public sealed record AssetPackGroupViewModel(string Header, IReadOnlyList<AssetPackViewModel> Packs)
{
    /// <summary>Whether the header is shown: for the packs of a third-party package.</summary>
    public bool HasHeader => Header.Length > 0;
}
