using ColorRise.Tessera.Rendering;
using Stride.Assets.Editor.EditorGame;
using Stride.Editor.EditorGame.Game;
using Stride.Rendering.Compositing;

namespace ColorRise.Tessera.Editor.Scene;

/// <summary>
/// Lets a click in the scene view select the entity of a tile map: puts the maps in the picking stage of the scene and prefab
/// editors, which their selection service makes for meshes and sprites only.
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
    /// Gives the compositor's tile feature a selector for the picking stage, unless it has one; false when the compositor has
    /// no picking stage or no tile feature.
    /// </summary>
    internal static bool AddPickingSelector(GraphicsCompositor compositor)
    {
        // The selection service adds its stage at the end
        var stage = compositor.RenderStages.LastOrDefault(stage => stage.Name == TileRenderFeature.PickingStageName);
        var feature = compositor.RenderFeatures.OfType<TileRenderFeature>().FirstOrDefault();
        if (stage is null || feature is null)
        {
            return false;
        }
        if (!feature.RenderStageSelectors.OfType<TileTransparentRenderStageSelector>().Any(selector => selector.TransparentRenderStage == stage))
        {
            feature.RenderStageSelectors.Add(new TileTransparentRenderStageSelector { TransparentRenderStage = stage });
        }
        return true;
    }
}
