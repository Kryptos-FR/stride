using ColorRise.Tessera.Rendering;
using Stride.Assets.Rendering;
using Stride.Rendering;

namespace ColorRise.Tessera.Editor.Rendering;

/// <summary>
/// Gives the compositors Game Studio makes (previews, thumbnails, a new game's compositor, "Add package features") the
/// features that draw tile maps and flipbooks, in the transparent stage.
/// </summary>
/// <remarks>
/// A provider cannot set the sort mode of that stage, which keeps flat things in order under a tilted camera
/// (<see cref="TileAxisSortMode"/>): the game sets it in its compositor (finding 10).
/// </remarks>
public sealed class TileRenderFeatureProvider : IRenderFeatureProvider
{
    /// <summary>
    /// The tile feature and the flipbook feature, whose chunks and frames go to <paramref name="transparentStage"/>.
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
        // A root render feature draws one type of render object: flipbooks have a feature of their own (finding 18)
        yield return new FlipbookRenderFeature
        {
            RenderStageSelectors =
            {
                new TileTransparentRenderStageSelector { TransparentRenderStage = transparentStage },
            },
        };
    }
}
