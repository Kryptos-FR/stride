using ColorRise.Tessera.Assets;
using Stride.Core.Assets.Presentation.Annotations;
using Stride.Core.Assets.Presentation.ViewModels;

namespace ColorRise.Tessera.Editor.ViewModels;

/// <summary>
/// A flipbook in the session.
/// </summary>
[AssetViewModel<FlipbookAsset>]
public sealed class FlipbookViewModel : AssetViewModel<FlipbookAsset>
{
    /// <summary>
    /// Makes the view model of a flipbook asset.
    /// </summary>
    public FlipbookViewModel(AssetViewModelConstructionParameters parameters)
        : base(parameters)
    {
    }
}
