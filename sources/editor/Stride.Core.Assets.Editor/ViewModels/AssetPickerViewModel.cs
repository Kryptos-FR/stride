// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.ObjectModel;
using Stride.Core.Assets.Presentation.ViewModels;
using Stride.Core.Extensions;
using Stride.Core.Presentation.ViewModels;

namespace Stride.Core.Assets.Editor.ViewModels;

/// <summary>
/// ViewModel for the asset picker dialog.
/// Exposes a filtered, searchable view of the session's assets.
/// </summary>
public sealed class AssetPickerViewModel : DispatcherViewModel
{
    private readonly SessionViewModel session;
    private string searchText = string.Empty;
    private AssetViewModel? selectedAsset;
    private DirectoryBaseViewModel? selectedFolder;

    public AssetPickerViewModel(SessionViewModel session, IEnumerable<Type> acceptedTypes, Func<AssetViewModel, bool>? filter = null)
        : base(session.SafeArgument(nameof(session)).ServiceProvider)
    {
        this.session = session;
        AcceptedTypes = acceptedTypes.ToList();
        Filter = filter;
        RefreshFilteredAssets();
    }

    public List<Type> AcceptedTypes { get; }

    public Func<AssetViewModel, bool>? Filter { get; set; }

    public string SearchText
    {
        get => searchText;
        set
        {
            if (SetValue(ref searchText, value))
                RefreshFilteredAssets();
        }
    }

    public AssetViewModel? SelectedAsset
    {
        get => selectedAsset;
        set => SetValue(ref selectedAsset, value);
    }

    public DirectoryBaseViewModel? SelectedFolder
    {
        get => selectedFolder;
        set
        {
            if (SetValue(ref selectedFolder, value))
                RefreshFilteredAssets();
        }
    }

    public ObservableCollection<AssetViewModel> FilteredAssets { get; } = [];

    public IEnumerable<PackageViewModel> AllPackages => session.LocalPackages.Concat(session.StorePackages);

    private void RefreshFilteredAssets()
    {
        FilteredAssets.Clear();

        var query = session.AllAssets.AsEnumerable();

        if (AcceptedTypes.Count > 0)
            query = query.Where(a => AcceptedTypes.Any(t => t.IsAssignableFrom(a.AssetType)));

        if (selectedFolder is not null)
            query = query.Where(a => a.Directory == selectedFolder || IsDescendantOf(a.Directory, selectedFolder));

        if (!string.IsNullOrWhiteSpace(searchText))
            query = query.Where(a => a.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase));

        if (Filter is not null)
            query = query.Where(Filter);

        foreach (var asset in query.OrderBy(a => a.Name))
            FilteredAssets.Add(asset);
    }

    private static bool IsDescendantOf(DirectoryBaseViewModel? child, DirectoryBaseViewModel parent)
    {
        var current = child;
        while (current is not null)
        {
            if (current == parent) return true;
            current = current.Parent;
        }
        return false;
    }
}
