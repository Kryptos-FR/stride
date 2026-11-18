// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core.Extensions;
using Stride.Core.Presentation.Collections;
using Stride.Core.Presentation.ViewModels;
using Stride.Core.Assets.Presentation.ViewModels;
using Stride.Core.Assets.Editor.Services;

namespace Stride.Core.Assets.Editor.ViewModels;

/// <summary>
/// The asset picker: the assets of the session that the caller accepts, narrowed to a location (a package or one of its folders)
/// and to the ones whose name contains a search text.
/// </summary>
public sealed class AssetPickerViewModel : DispatcherViewModel
{
    private readonly ISessionViewModel session;
    private string searchText = string.Empty;
    private AssetViewModel? selectedAsset;
    private object? selectedLocation;

    /// <param name="session">The session whose assets are listed.</param>
    /// <param name="acceptedTypes">The asset types to list (and their derived types); empty lists every type.</param>
    /// <param name="filter">An additional condition on the listed assets, or <c>null</c>.</param>
    /// <param name="initialAsset">The asset selected when the picker opens (the current value of a reference), or <c>null</c>.</param>
    /// <param name="initialLocation">The folder selected when the picker opens and there is no initial asset, or <c>null</c> for every location.</param>
    public AssetPickerViewModel(ISessionViewModel session, IEnumerable<Type> acceptedTypes, Func<AssetViewModel, bool>? filter = null, AssetViewModel? initialAsset = null, DirectoryBaseViewModel? initialLocation = null)
        : this(session, new AssetPickerOptions(acceptedTypes) { Filter = filter, InitialAsset = initialAsset, InitialLocation = initialLocation })
    {
    }

    /// <param name="session">The session whose assets are listed.</param>
    /// <param name="options">What the picker lists, and how: the accepted types, the filter, the assets listed but not
    /// pickable, the notes, the initial asset or location and the message.</param>
    [System.Diagnostics.CodeAnalysis.Experimental("STREXP012")]
    public AssetPickerViewModel(ISessionViewModel session, AssetPickerOptions options)
        : base(session.SafeArgument(nameof(session)).ServiceProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.session = session;
        AcceptedTypes = options.AcceptedTypes.ToList();
        Filter = options.Filter;
        DisabledReason = options.DisabledReason;
        Note = options.Note;
        Message = options.Message;
        var initialAsset = options.InitialAsset;
        selectedLocation = initialAsset?.Directory ?? options.InitialLocation;
        RefreshFilteredAssets();
        if (initialAsset is not null && FilteredAssets.Contains(initialAsset) && GetDisabledReason(initialAsset) is null)
            selectedAsset = initialAsset;
    }

    /// <summary>
    /// Gets the asset types the picker lists (and their derived types); empty lists every type.
    /// </summary>
    public IReadOnlyList<Type> AcceptedTypes { get; }

    /// <summary>
    /// Gets or sets a text shown above the assets (what the asset is picked for), or <c>null</c>.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Gets the additional condition on the listed assets, or <c>null</c>.
    /// </summary>
    public Func<AssetViewModel, bool>? Filter { get; }

    /// <summary>
    /// Gets why a listed asset cannot be picked, or <c>null</c> for none disabled.
    /// </summary>
    public Func<AssetViewModel, string?>? DisabledReason { get; }

    /// <summary>
    /// Gets the note of a listed asset, or <c>null</c> for none.
    /// </summary>
    public Func<AssetViewModel, string?>? Note { get; }

    /// <summary>
    /// Gets whether the selected asset can be picked (the picker can be validated).
    /// </summary>
    public bool CanPick => selectedAsset is not null && GetDisabledReason(selectedAsset) is null;

    /// <summary>
    /// Gets the packages of the session, the roots of the location tree: their children are the folders of their assets.
    /// </summary>
    public IEnumerable<PackageViewModel> Packages => session.LocalPackages.Concat(session.StorePackages);

    /// <summary>
    /// Gets or sets the text that the name of a listed asset contains (case-insensitive); empty lists every name.
    /// </summary>
    public string SearchText
    {
        get => searchText;
        set
        {
            if (SetValue(ref searchText, value ?? string.Empty))
                RefreshFilteredAssets();
        }
    }

    /// <summary>
    /// Gets or sets the selected location: a <see cref="PackageViewModel"/> lists the assets of the package, a
    /// <see cref="DirectoryBaseViewModel"/> the assets of the folder and its sub-folders, <c>null</c> the assets of the session.
    /// </summary>
    public object? SelectedLocation
    {
        get => selectedLocation;
        set
        {
            if (SetValue(ref selectedLocation, value))
                RefreshFilteredAssets();
        }
    }

    /// <summary>
    /// Gets or sets the selected asset, the result of the picker when it is validated.
    /// </summary>
    public AssetViewModel? SelectedAsset
    {
        get => selectedAsset;
        set
        {
            if (SetValue(ref selectedAsset, value))
                OnPropertyChanged(nameof(CanPick));
        }
    }

    /// <summary>
    /// Gets the assets that match the accepted types, the filter, the location and the search text, sorted by name.
    /// </summary>
    public ObservableList<AssetViewModel> FilteredAssets { get; } = [];

    /// <summary>
    /// Why a listed asset cannot be picked, or <c>null</c> when it can.
    /// </summary>
    public string? GetDisabledReason(AssetViewModel asset) => DisabledReason?.Invoke(asset);

    /// <summary>
    /// The note of a listed asset that can be picked, or <c>null</c>.
    /// </summary>
    public string? GetNote(AssetViewModel asset) => GetDisabledReason(asset) is null ? Note?.Invoke(asset) : null;

    /// <summary>
    /// Whether the picker accepts an asset, regardless of the location and the search text.
    /// </summary>
    public bool IsAccepted(AssetViewModel asset)
    {
        if (AcceptedTypes.Count > 0 && !AcceptedTypes.Any(x => x.IsAssignableFrom(asset.AssetType)))
            return false;
        return Filter?.Invoke(asset) ?? true;
    }

    private void RefreshFilteredAssets()
    {
        var assets = session.AllAssets.Where(x => IsAccepted(x) && IsInLocation(x));
        if (!string.IsNullOrWhiteSpace(searchText))
            assets = assets.Where(x => x.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase));

        FilteredAssets.Clear();
        // The assets that can be picked first, then those listed disabled
        FilteredAssets.AddRange(assets.OrderBy(x => GetDisabledReason(x) is not null).ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase));
        if (selectedAsset is not null && !FilteredAssets.Contains(selectedAsset))
            SelectedAsset = null;
    }

    private bool IsInLocation(AssetViewModel asset)
    {
        switch (selectedLocation)
        {
            case PackageViewModel package:
                return asset.Directory?.Package == package;
            case DirectoryBaseViewModel directory:
                for (var current = asset.Directory; current is not null; current = current.Parent)
                {
                    if (current == directory)
                        return true;
                }
                return false;
            default:
                return true;
        }
    }
}
