using ColorRise.Tessera.Assets;
using Stride.Core.Assets.Presentation.Annotations;
using Stride.Core.Assets.Presentation.ViewModels;

namespace ColorRise.Tessera.Editor.ViewModels;

/// <summary>
/// A tile set in the session.
/// </summary>
[AssetViewModel<TileSetAsset>]
public sealed class TileSetViewModel : AssetViewModel<TileSetAsset>
{
    /// <summary>
    /// Makes the view model of a tile set asset.
    /// </summary>
    public TileSetViewModel(AssetViewModelConstructionParameters parameters)
        : base(parameters)
    {
    }
}
