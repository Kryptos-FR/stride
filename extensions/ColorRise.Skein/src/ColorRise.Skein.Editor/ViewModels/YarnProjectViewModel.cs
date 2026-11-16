using ColorRise.Skein.Assets;
using ColorRise.Skein.Editor.Services;
using Stride.Core.Assets.Presentation.Annotations;
using Stride.Core.Assets.Presentation.ViewModels;
using Stride.Core.IO;
using Stride.Core.Quantum;

namespace ColorRise.Skein.Editor.ViewModels;

/// <summary>
/// A Yarn project in the session.
/// </summary>
[AssetViewModel<YarnProjectAsset>]
public sealed class YarnProjectViewModel : AssetViewModel<YarnProjectAsset>
{
    /// <summary>
    /// Makes the view model of a Yarn project asset.
    /// </summary>
    public YarnProjectViewModel(AssetViewModelConstructionParameters parameters)
        : base(parameters)
    {
        // A new, renamed or moved project changes which scripts are whose
        PropertyChanged += (_, _) => ServiceProvider.TryGet<YarnAnalysisService>()?.Invalidate();
        ServiceProvider.TryGet<YarnAnalysisService>()?.Invalidate();
    }

    /// <summary>
    /// Sets the strings file of a language of the project, as the property grid would.
    /// </summary>
    /// <param name="index">The place of the language in the localisations of the asset.</param>
    /// <param name="file">The strings file.</param>
    internal void SetStringsFile(int index, UFile file)
    {
        var localisation = AssetRootNode[nameof(YarnProjectAsset.Localisations)].Target!.IndexedTarget(new NodeIndex(index));
        localisation![nameof(YarnLocalisation.Strings)].Update(file);
    }

    /// <inheritdoc/>
    protected override void OnSessionSaved()
    {
        base.OnSessionSaved();

        // Yarn Spinner's own tools read the project from the files beside the asset
        ServiceProvider.TryGet<YarnLocalisationService>()?.WriteProjectFiles(this);
    }

    /// <inheritdoc/>
    public override void Destroy()
    {
        ServiceProvider.TryGet<YarnAnalysisService>()?.Invalidate();
        base.Destroy();
    }
}
