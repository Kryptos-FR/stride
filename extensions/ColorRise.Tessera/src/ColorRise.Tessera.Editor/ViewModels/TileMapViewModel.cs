using ColorRise.Tessera.Assets;
using Stride.Core.Assets.Presentation.Annotations;
using Stride.Core.Assets.Presentation.ViewModels;

namespace ColorRise.Tessera.Editor.ViewModels;

/// <summary>
/// A tile map in the session.
/// </summary>
[AssetViewModel<TileMapAsset>]
public sealed class TileMapViewModel : AssetViewModel<TileMapAsset>
{
    /// <summary>
    /// Makes the view model of a tile map asset.
    /// </summary>
    public TileMapViewModel(AssetViewModelConstructionParameters parameters)
        : base(parameters)
    {
    }
}
