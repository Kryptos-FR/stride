using ColorRise.Tessera.Assets;
using ColorRise.Tessera.Editor.ViewModels;
using Stride.Assets.Editor.AssetEditors.EntityHierarchyEditor.ViewModels;
using Stride.Assets.Presentation.AssetEditors.EntityHierarchyEditor.ViewModels;
using Stride.Core.Assets.Presentation.Services;
using Stride.Engine;

namespace ColorRise.Tessera.Editor.Scene;

/// <summary>
/// A tile map asset dropped in a scene becomes an entity with a tile map component that names it.
/// </summary>
public sealed class AddTileMapAssetPolicy : CreateComponentPolicyBase<TileMapAsset, TileMapViewModel>
{
    /// <summary>
    /// A tile map component whose map is the dropped asset.
    /// </summary>
    protected override EntityComponent CreateComponentFromAsset(IEntityHierarchyItemViewModel parent, TileMapViewModel asset)
        => new TileMapComponent { Map = ContentReferenceHelper.CreateReference<TileMap>(asset) };
}
