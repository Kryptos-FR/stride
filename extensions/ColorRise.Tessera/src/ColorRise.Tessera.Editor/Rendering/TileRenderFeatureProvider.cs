using ColorRise.Tessera.Rendering;
using Stride.Assets.Rendering;
using Stride.Rendering;

namespace ColorRise.Tessera.Editor.Rendering;

/// <summary>
/// Gives the compositors Game Studio makes (previews, thumbnails, a new game's compositor, "Add package features") the
/// feature that draws tile maps, in the transparent stage.
/// </summary>
/// <remarks>
/// A provider cannot set the sort mode of that stage, which keeps flat things in order under a tilted camera
/// (<see cref="TileAxisSortMode"/>): the game sets it in its compositor (finding 10).
/// </remarks>
public sealed class TileRenderFeatureProvider : IRenderFeatureProvider
{
    /// <summary>
    /// The tile feature, whose chunks go to <paramref name="transparentStage"/>.
    /// </summary>
    public IEnumerable<RootRenderFeature> CreateRenderFeatures(RenderStage opaqueStage, RenderStage transparentStage)
    {
        yield return new TileRenderFeature
        {
            RenderStageSelectors =
            {
                new TileTransparentRenderStageSelector { TransparentRenderStage = transparentStage },
            },
        };
    }
}
