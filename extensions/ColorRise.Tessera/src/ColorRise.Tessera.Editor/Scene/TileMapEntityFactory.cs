using Stride.Assets.Editor.EntityFactories;
using Stride.Assets.Presentation.AssetEditors.EntityHierarchyEditor.ViewModels;
using Stride.Core;
using Stride.Engine;

namespace ColorRise.Tessera.Editor.Scene;

/// <summary>
/// "Tile map" in the 2D category of "Add entity": an entity with a tile map component, whose map is then picked in the
/// property grid.
/// </summary>
[Display(40, "Tile map", "2D")]
public sealed class TileMapEntityFactory : EntityFactory
{
    /// <summary>
    /// Makes the entity, named "Tile map" or the first free name after it among its siblings.
    /// </summary>
    public override Task<Entity> CreateEntity(IEntityHierarchyItemViewModel parent)
        => CreateEntityWithComponent(ComputeNewName(parent, "Tile map"), new TileMapComponent());
}
