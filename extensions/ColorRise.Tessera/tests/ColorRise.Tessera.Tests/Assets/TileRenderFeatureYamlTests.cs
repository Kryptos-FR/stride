using System.Text;
using ColorRise.Tessera.Rendering;
using Stride.Assets.Rendering;
using Stride.Core.Assets;
using Stride.Core.Assets.Serializers;
using Stride.Core.IO;
using Stride.Rendering;

namespace ColorRise.Tessera.Tests.Assets;

public sealed class TileRenderFeatureYamlTests
{
    // As Stride's own features: the base class's runtime members (render data, objects, nodes, Enabled) are not saved, and
    // Enabled cannot be set back
    [Fact]
    public void A_compositor_asset_keeps_only_the_selectors_of_the_tile_feature()
    {
        var transparent = new RenderStage("Transparent", "Main");
        var compositor = new GraphicsCompositorAsset();
        compositor.RenderStages.Add(transparent);
        compositor.RenderFeatures.Add(new TileRenderFeature
        {
            RenderStageSelectors = { new TileTransparentRenderStageSelector { TransparentRenderStage = transparent } },
        });

        using var output = new MemoryStream();
        AssetFileSerializer.Save(output, compositor, null);
        var text = Encoding.UTF8.GetString(output.ToArray());
        Assert.Contains("!TileRenderFeature", text);
        Assert.All(new[] { "RenderData", "RenderObjects", "ObjectNodeReferences", "RenderNodes", "Enabled" }, member => Assert.DoesNotContain(member, text));

        using var input = new MemoryStream(Encoding.UTF8.GetBytes(text));
        var loaded = (GraphicsCompositorAsset)AssetFileSerializer.Load<Asset>(input, new UFile("Compositor.sdgfxcomp")).Asset;
        var feature = Assert.IsType<TileRenderFeature>(Assert.Single(loaded.RenderFeatures));
        var selector = Assert.IsType<TileTransparentRenderStageSelector>(Assert.Single(feature.RenderStageSelectors));
        // Saved as a reference to the stage's id, which the editor resolves
        Assert.Equal(Assert.Single(loaded.RenderStages).Id, selector.TransparentRenderStage?.Id);
    }

    [Fact]
    public void A_compositor_asset_keeps_only_the_selectors_of_the_flipbook_feature()
    {
        var transparent = new RenderStage("Transparent", "Main");
        var compositor = new GraphicsCompositorAsset();
        compositor.RenderStages.Add(transparent);
        compositor.RenderFeatures.Add(new FlipbookRenderFeature
        {
            RenderStageSelectors = { new TileTransparentRenderStageSelector { TransparentRenderStage = transparent } },
        });

        using var output = new MemoryStream();
        AssetFileSerializer.Save(output, compositor, null);
        var text = Encoding.UTF8.GetString(output.ToArray());
        Assert.Contains("!FlipbookRenderFeature", text);
        Assert.All(new[] { "RenderData", "RenderObjects", "ObjectNodeReferences", "RenderNodes", "Enabled", "DrawnFlipbooks" }, member => Assert.DoesNotContain(member, text));

        using var input = new MemoryStream(Encoding.UTF8.GetBytes(text));
        var loaded = (GraphicsCompositorAsset)AssetFileSerializer.Load<Asset>(input, new UFile("Compositor.sdgfxcomp")).Asset;
        var feature = Assert.IsType<FlipbookRenderFeature>(Assert.Single(loaded.RenderFeatures));
        var selector = Assert.IsType<TileTransparentRenderStageSelector>(Assert.Single(feature.RenderStageSelectors));
        Assert.Equal(Assert.Single(loaded.RenderStages).Id, selector.TransparentRenderStage?.Id);
    }
}
