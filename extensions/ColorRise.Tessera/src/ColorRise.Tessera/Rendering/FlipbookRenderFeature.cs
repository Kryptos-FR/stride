using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Graphics;
using Stride.Rendering;

namespace ColorRise.Tessera.Rendering;

/// <summary>
/// Draws the frames of flipbook components in the render stage that its selector picks, in the order of the stage: in the
/// transparent stage, by depth, so that the flipbooks go between the layers, the sprites and the other maps by their z.
/// </summary>
/// <remarks>
/// A root render feature draws one exact type of render object, so the chunks of maps and the frames of flipbooks have a
/// feature each.
/// </remarks>
// As the base class: only the members marked [DataMember] are saved, so the selectors and none of the runtime state
[DataContract("FlipbookRenderFeature", DefaultMemberMode = DataMemberMode.Never)]
[Display("Flipbooks")]
public sealed class FlipbookRenderFeature : RootRenderFeature
{
    private readonly Lock countLock = new();
    private ThreadLocal<TileBatch>? batches;

    /// <summary>
    /// The feature draws the frames of flipbooks.
    /// </summary>
    public override Type SupportedRenderObjectType => typeof(RenderFlipbook);

    /// <summary>
    /// The flipbooks drawn in the last frame, after culling, for the tests.
    /// </summary>
    internal int DrawnFlipbooks { get; private set; }

    /// <summary>
    /// Makes a batch for each thread that draws.
    /// </summary>
    protected override void InitializeCore()
    {
        base.InitializeCore();
        batches = new ThreadLocal<TileBatch>(() => new TileBatch(Context.GraphicsDevice), trackAllValues: true);
    }

    /// <summary>
    /// Disposes the batches.
    /// </summary>
    protected override void Destroy()
    {
        if (batches is not null)
        {
            foreach (var batch in batches.Values)
            {
                batch.Dispose();
            }
            batches.Dispose();
            batches = null;
        }
        base.Destroy();
    }

    /// <summary>
    /// Counts the flipbooks drawn again from 0 for the new frame.
    /// </summary>
    public override void Extract()
    {
        base.Extract();
        DrawnFlipbooks = 0;
    }

    /// <summary>
    /// Draws the frames of the stage between two indices, in the stage's order.
    /// </summary>
    public override void Draw(RenderDrawContext context, RenderView renderView, RenderViewStage renderViewStage, int startIndex, int endIndex)
    {
        base.Draw(context, renderView, renderViewStage, startIndex, endIndex);
        var stage = RenderSystem.RenderStages[renderViewStage.Index];
        var run = new TileBatchRun(batches!.Value!, context, renderView.ViewProjection, TileBatchRun.Rasterizer(stage.Output), null);

        for (var index = startIndex; index < endIndex; index++)
        {
            var render = (RenderFlipbook)GetRenderNode(renderViewStage.SortedRenderNodes[index].RenderNode).RenderObject;
            if (render.Flipbook is not { TileSet: { } set } flipbook || render.Frame is not { } frame)
            {
                continue;
            }
            for (var cell = 0; cell < frame.Cells.Count; cell++)
            {
                if (!FlipbookQuads.TryCell(flipbook, frame, cell, render.World, render.FlipX, out var quad, out var tile))
                {
                    continue;
                }
                Context.StreamingManager?.StreamResources(tile.Texture!);
                run.Draw(tile.Texture!, quad, render.Color, render.Sampler, set.PremultipliedAlpha);
            }
        }
        run.End();

        lock (countLock)
        {
            DrawnFlipbooks += endIndex - startIndex;
        }
    }
}
