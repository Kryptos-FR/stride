using ColorRise.Tessera.Rendering;
using Stride.Assets.Editor.EditorGame;
using Stride.Editor.EditorGame.Game;
using Stride.Rendering.Compositing;

namespace ColorRise.Tessera.Editor.Scene;

/// <summary>
/// Lets a click in the scene view select the entity of a tile map or a flipbook: puts the maps and the flipbooks in the
/// picking stage of the scene and prefab editors, which their selection service makes for meshes and sprites only.
/// </summary>
[EditorGameService(typeof(EntityHierarchyEditorController), Order = 210)]
public sealed class TilePickingService : EditorGameServiceBase
{
    /// <summary>
    /// Adds the picking selector to each compositor the editor's game takes, after the selection service made its stage.
    /// </summary>
    public override void UpdateGraphicsCompositor(EditorServiceGame editorGame)
    {
        base.UpdateGraphicsCompositor(editorGame);
        AddPickingSelector(editorGame.SceneSystem.GraphicsCompositor);
    }

    /// <summary>
    /// Has nothing to set up: the work is done when the compositor changes.
    /// </summary>
    protected override Task<bool> Initialize(EditorServiceGame editorGame) => Task.FromResult(true);

    /// <summary>
    /// Gives the compositor's tile feature and flipbook feature a selector for the picking stage, each unless it has one;
    /// false when the compositor has no picking stage or neither feature.
    /// </summary>
    internal static bool AddPickingSelector(GraphicsCompositor compositor)
    {
        // The selection service adds its stage at the end
        var stage = compositor.RenderStages.LastOrDefault(stage => stage.Name == TileRenderFeature.PickingStageName);
        if (stage is null)
        {
            return false;
        }
        var features = compositor.RenderFeatures.Where(feature => feature is TileRenderFeature or FlipbookRenderFeature)
            .GroupBy(feature => feature.GetType()).Select(group => group.First()).ToList();
        foreach (var feature in features)
        {
            if (!feature.RenderStageSelectors.OfType<TileTransparentRenderStageSelector>().Any(selector => selector.TransparentRenderStage == stage))
            {
                feature.RenderStageSelectors.Add(new TileTransparentRenderStageSelector { TransparentRenderStage = stage });
            }
        }
        return features.Count > 0;
    }
}
