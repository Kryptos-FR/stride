using ColorRise.Tessera.Assets;
using ColorRise.Tessera.Editor.ViewModels;
using Stride.Assets.Editor.AssetEditors.EntityHierarchyEditor.ViewModels;
using Stride.Assets.Presentation.AssetEditors.EntityHierarchyEditor.ViewModels;
using Stride.Core.Assets.Presentation.Services;
using Stride.Engine;

namespace ColorRise.Tessera.Editor.Scene;

/// <summary>
/// A flipbook asset dropped in a scene becomes an entity whose flipbook component names the flipbook and its first animation.
/// </summary>
public sealed class AddFlipbookAssetPolicy : CreateComponentPolicyBase<FlipbookAsset, FlipbookViewModel>
{
    /// <summary>
    /// A flipbook component whose flipbook is the dropped asset, and whose animation is the asset's first, if it has one.
    /// </summary>
    protected override EntityComponent CreateComponentFromAsset(IEntityHierarchyItemViewModel parent, FlipbookViewModel asset)
        => new FlipbookComponent
        {
            Flipbook = ContentReferenceHelper.CreateReference<Flipbook>(asset),
            Animation = asset.Asset.Animations.FirstOrDefault(animation => animation is not null)?.Name,
        };
}
