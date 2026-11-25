using System.Reflection;
using ColorRise.Tessera.Assets;
using ColorRise.Tessera.Editor.Rendering;
using ColorRise.Tessera.Editor.Scene;
using ColorRise.Tessera.Rendering;
using Stride.Assets.Editor.EntityFactories;
using Stride.Assets.Editor.ViewModels;
using Stride.Assets.Entities;
using Stride.Assets.Presentation.AssetEditors.EntityHierarchyEditor.ViewModels;
using Stride.Assets.Presentation.ViewModels;
using Stride.Assets.Rendering;
using Stride.Core;
using Stride.Core.Reflection;
using Stride.Core.Serialization;
using Stride.Rendering;

namespace ColorRise.Tessera.Editor.Tests;

/// <summary>
/// What the scene editor gets from Tessera: the feature that draws maps in its compositors, the "Tile map" entity of
/// "Add entity", and a map asset dropped in a scene.
/// </summary>
public sealed class SceneEditorTests
{
    [Fact]
    public void The_provider_gives_the_tile_feature_for_the_transparent_stage()
    {
        var (opaque, transparent) = (new RenderStage("Opaque", "Main"), new RenderStage("Transparent", "Main"));
        var feature = Assert.IsType<TileRenderFeature>(Assert.Single(new TileRenderFeatureProvider().CreateRenderFeatures(opaque, transparent)));
        var selector = Assert.IsType<TileTransparentRenderStageSelector>(Assert.Single(feature.RenderStageSelectors));
        Assert.Same(transparent, selector.TransparentRenderStage);
    }

    [Fact]
    public void The_editor_finds_the_provider()
        => Assert.Contains(RenderFeatureProviders.Enumerate(), provider => provider is TileRenderFeatureProvider);

    [Fact]
    public void The_factory_is_in_the_2D_category_after_Background()
    {
        var display = typeof(TileMapEntityFactory).GetCustomAttribute<DisplayAttribute>()!;
        Assert.Equal((40, "Tile map", "2D"), (display.Order, display.Name, display.Category));
        // Game Studio fills "Add entity" with the factories of the scan index of each asset assembly
        Assert.Contains(typeof(TileMapEntityFactory), AssemblyRegistry.GetScanTypes(typeof(TileMapEntityFactory).Assembly, typeof(IEntityFactory)));
    }

    [Fact]
    public async Task Create_entity_makes_an_entity_with_a_tile_map_component()
    {
        await using var session = await TesseraSession.OpenAsync(("MainScene", new SceneAsset()));
        var editor = new SceneEditorViewModel(Assert.IsType<SceneViewModel>(session.Session.AllAssets.Single(a => a.Name == "MainScene")));
        try
        {
            Assert.True(await editor.Initialize());
            editor.CreateEntityInRootCommand.Execute(new TileMapEntityFactory());
            var entity = Assert.Single(editor.HierarchyRoot.Children.OfType<EntityViewModel>());
            Assert.Equal("Tile map", entity.Name);
            Assert.Null(Assert.Single(entity.AssetSideEntity.Components.OfType<TileMapComponent>()).Map);
        }
        finally
        {
            editor.Destroy();
        }
    }

    [Fact]
    public async Task A_dropped_tile_map_becomes_a_component_that_names_it()
    {
        // Game Studio's editor plugin, which the kit does not load, registers the drop policies of each asset assembly (finding 11)
        AddAssetPolicyRegistry.RegisterAssembly(typeof(AddTileMapAssetPolicy).Assembly);
        await using var session = await TesseraSession.OpenAsync(("MainScene", new SceneAsset()), ("Maps/Town", new TileMapAssetFactory().New()));
        var map = session.MapAt("Maps/Town");
        var editor = new SceneEditorViewModel(Assert.IsType<SceneViewModel>(session.Session.AllAssets.Single(a => a.Name == "MainScene")));
        try
        {
            Assert.True(await editor.Initialize());

            // The scene asks the policies with the type of the dropped asset
            var policy = Assert.Single(editor.AddAssetPolicies.OfType<AddTileMapAssetPolicy>());
            Assert.True(policy.Accept(map.AssetType));
            var component = Assert.IsType<TileMapComponent>(((ICreateComponentPolicy)policy).CreateComponentFromAsset(editor.HierarchyRoot, map));
            Assert.Equal(map.Id, AttachedReferenceManager.GetAttachedReference(component.Map)?.Id);
        }
        finally
        {
            editor.Destroy();
        }
    }
}
