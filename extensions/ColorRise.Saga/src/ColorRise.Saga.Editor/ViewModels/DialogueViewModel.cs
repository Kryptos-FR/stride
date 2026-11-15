using ColorRise.Saga.Assets;
using Stride.Core.Assets.Presentation.Annotations;
using Stride.Core.Assets.Presentation.ViewModels;

namespace ColorRise.Saga.Editor.ViewModels;

/// <summary>
/// The view model of a dialogue asset in the session.
/// </summary>
[AssetViewModel<DialogueAsset>]
public sealed class DialogueViewModel(AssetViewModelConstructionParameters parameters) : AssetViewModel<DialogueAsset>(parameters)
{
}
