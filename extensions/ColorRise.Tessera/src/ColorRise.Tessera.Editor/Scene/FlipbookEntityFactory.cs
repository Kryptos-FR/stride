using Stride.Assets.Editor.EntityFactories;
using Stride.Assets.Presentation.AssetEditors.EntityHierarchyEditor.ViewModels;
using Stride.Core;
using Stride.Engine;

namespace ColorRise.Tessera.Editor.Scene;

/// <summary>
/// "Flipbook" in the 2D category of "Add entity", after "Tile map": an entity with a flipbook component, whose flipbook and
/// animation are then picked in the property grid.
/// </summary>
[Display(50, "Flipbook", "2D")]
public sealed class FlipbookEntityFactory : EntityFactory
{
    /// <summary>
    /// Makes the entity, named "Flipbook" or the first free name after it among its siblings.
    /// </summary>
    public override Task<Entity> CreateEntity(IEntityHierarchyItemViewModel parent)
        => CreateEntityWithComponent(ComputeNewName(parent, "Flipbook"), new FlipbookComponent());
}
