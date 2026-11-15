using ColorRise.Saga.Assets;
using Stride.Core.Assets.Presentation.Annotations;
using Stride.Core.Assets.Presentation.ViewModels;

namespace ColorRise.Saga.Editor.ViewModels;

/// <summary>
/// The view model of a story asset in the session.
/// </summary>
[AssetViewModel<StoryAsset>]
public sealed class StoryViewModel(AssetViewModelConstructionParameters parameters) : AssetViewModel<StoryAsset>(parameters)
{
}
