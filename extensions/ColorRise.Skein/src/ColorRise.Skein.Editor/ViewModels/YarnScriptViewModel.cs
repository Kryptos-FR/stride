using ColorRise.Skein.Assets;
using Stride.Core.Assets.Presentation.Annotations;
using Stride.Core.Assets.Presentation.ViewModels;

namespace ColorRise.Skein.Editor.ViewModels;

/// <summary>
/// A Yarn script in the session.
/// </summary>
[AssetViewModel<YarnScriptAsset>]
public sealed class YarnScriptViewModel(AssetViewModelConstructionParameters parameters) : AssetViewModel<YarnScriptAsset>(parameters)
{
}
