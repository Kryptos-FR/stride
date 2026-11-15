using ColorRise.Skein.Assets;
using ColorRise.Skein.Editor.Services;
using Stride.Core.Assets.Presentation.Annotations;
using Stride.Core.Assets.Presentation.ViewModels;

namespace ColorRise.Skein.Editor.ViewModels;

/// <summary>
/// A Yarn project in the session.
/// </summary>
[AssetViewModel<YarnProjectAsset>]
public sealed class YarnProjectViewModel : AssetViewModel<YarnProjectAsset>
{
    public YarnProjectViewModel(AssetViewModelConstructionParameters parameters)
        : base(parameters)
    {
        // A new, renamed or moved project changes which scripts are whose
        PropertyChanged += (_, _) => ServiceProvider.TryGet<YarnAnalysisService>()?.Invalidate();
        ServiceProvider.TryGet<YarnAnalysisService>()?.Invalidate();
    }

    /// <inheritdoc/>
    public override void Destroy()
    {
        ServiceProvider.TryGet<YarnAnalysisService>()?.Invalidate();
        base.Destroy();
    }
}
