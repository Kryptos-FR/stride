using System.Reflection;
using ColorRise.Tessera.Editor.Scene;
using ColorRise.Tessera.Rendering;
using Stride.Assets.Editor.EditorGame;
using Stride.Core.Reflection;
using Stride.Editor.EditorGame.Game;
using Stride.Rendering;
using Stride.Rendering.Compositing;

namespace ColorRise.Tessera.Editor.Tests;

/// <summary>
/// The service that lets a click in the scene view pick a tile map: it puts the maps in the picking stage of the scene editor.
/// </summary>
public sealed class TilePickingServiceTests
{
    [Fact]
    public void The_selector_is_added_once_for_the_picking_stage()
    {
        var transparent = new RenderStage("Transparent", "Main");
        var picking = new RenderStage("Picking", "Picking");
        var feature = new TileRenderFeature { RenderStageSelectors = { new TileTransparentRenderStageSelector { TransparentRenderStage = transparent } } };
        var compositor = new GraphicsCompositor { RenderStages = { transparent, picking }, RenderFeatures = { feature } };

        Assert.True(TilePickingService.AddPickingSelector(compositor));
        Assert.True(TilePickingService.AddPickingSelector(compositor));

        var stages = feature.RenderStageSelectors.Cast<TileTransparentRenderStageSelector>().Select(selector => selector.TransparentRenderStage);
        Assert.Equal([transparent, picking], stages);
        Assert.Equal(TileRenderFeature.PickingStageName, picking.Name);
    }

    [Fact]
    public void Both_features_get_their_selector_once()
    {
        var transparent = new RenderStage("Transparent", "Main");
        var picking = new RenderStage("Picking", "Picking");
        var tiles = new TileRenderFeature { RenderStageSelectors = { new TileTransparentRenderStageSelector { TransparentRenderStage = transparent } } };
        var flipbooks = new FlipbookRenderFeature { RenderStageSelectors = { new TileTransparentRenderStageSelector { TransparentRenderStage = transparent } } };
        var compositor = new GraphicsCompositor { RenderStages = { transparent, picking }, RenderFeatures = { tiles, flipbooks } };

        Assert.True(TilePickingService.AddPickingSelector(compositor));
        Assert.True(TilePickingService.AddPickingSelector(compositor));

        foreach (var feature in new RootRenderFeature[] { tiles, flipbooks })
        {
            var stages = feature.RenderStageSelectors.Cast<TileTransparentRenderStageSelector>().Select(selector => selector.TransparentRenderStage);
            Assert.Equal([transparent, picking], stages);
        }
    }

    [Fact]
    public void A_compositor_with_the_flipbook_feature_alone_gets_its_selector()
    {
        var picking = new RenderStage("Picking", "Picking");
        var flipbooks = new FlipbookRenderFeature();
        var compositor = new GraphicsCompositor { RenderStages = { picking }, RenderFeatures = { flipbooks } };

        Assert.True(TilePickingService.AddPickingSelector(compositor));

        Assert.Same(picking, Assert.IsType<TileTransparentRenderStageSelector>(Assert.Single(flipbooks.RenderStageSelectors)).TransparentRenderStage);
    }

    [Fact]
    public void Without_a_picking_stage_or_a_feature_nothing_is_added()
    {
        var transparent = new RenderStage("Transparent", "Main");
        var feature = new TileRenderFeature { RenderStageSelectors = { new TileTransparentRenderStageSelector { TransparentRenderStage = transparent } } };
        var withoutStage = new GraphicsCompositor { RenderStages = { transparent }, RenderFeatures = { feature } };
        var withoutFeature = new GraphicsCompositor { RenderStages = { transparent, new RenderStage("Picking", "Picking") } };

        Assert.False(TilePickingService.AddPickingSelector(withoutStage));
        Assert.False(TilePickingService.AddPickingSelector(withoutFeature));
        Assert.Single(feature.RenderStageSelectors);
    }

    [Fact]
    public void The_service_is_found_as_the_editor_finds_its_game_services()
    {
        // The editor's controller creates the services of the scan index of each asset assembly, by their order
        Assert.Contains(typeof(TilePickingService), AssemblyRegistry.GetScanTypes(typeof(TilePickingService).Assembly, typeof(EditorGameServiceAttribute)));
        var declaration = Assert.Single(typeof(TilePickingService).GetCustomAttributes<EditorGameServiceAttribute>());
        Assert.Equal(typeof(EntityHierarchyEditorController), declaration.ControllerType);
        // After the selection service, which makes the picking stage
        var selection = typeof(EditorGameEntitySelectionService).GetCustomAttribute<EditorGameServiceAttribute>()!;
        Assert.True(declaration.Order > selection.Order);
    }
}
