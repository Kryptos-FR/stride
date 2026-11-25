using System.Text;
using ColorRise.Tessera.Rendering;
using Stride.Assets.Rendering;
using Stride.Core.Assets;
using Stride.Core.Assets.Serializers;
using Stride.Core.IO;
using Stride.Core.Mathematics;
using Stride.Rendering;

namespace ColorRise.Tessera.Tests.Assets;

public sealed class SortModeYamlTests
{
    [Fact]
    public void A_compositor_asset_keeps_the_sort_mode_of_a_stage()
    {
        var compositor = new GraphicsCompositorAsset();
        compositor.RenderStages.Add(new RenderStage("Transparent", "Main") { SortMode = new TileAxisSortMode { Axis = Vector3.UnitY } });

        using var output = new MemoryStream();
        AssetFileSerializer.Save(output, compositor, null);
        var text = Encoding.UTF8.GetString(output.ToArray());
        Assert.Contains("!TileAxisSortMode", text);

        using var input = new MemoryStream(Encoding.UTF8.GetBytes(text));
        var loaded = (GraphicsCompositorAsset)AssetFileSerializer.Load<Asset>(input, new UFile("Compositor.sdgfxcomp")).Asset;
        var sortMode = Assert.IsType<TileAxisSortMode>(Assert.Single(loaded.RenderStages).SortMode);
        Assert.Equal(new Vector3(0, 1, 0), sortMode.Axis);
    }
}
