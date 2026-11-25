using Stride.Core.Mathematics;
using Stride.Graphics;
using Stride.Rendering;
using Stride.Rendering.Compositing;

namespace ColorRise.Tessera.Tests.Graphics;

/// <summary>
/// The scene editor's picking pass (<c>PickingSceneRenderer</c>) over the whole frame: the stage named "Picking" drawn into a
/// target of two floats cleared to 0, with a depth buffer of its own, then read back.
/// </summary>
internal sealed class PickingRenderer : SceneRendererBase
{
    public required RenderStage Stage { get; init; }

    /// <summary>
    /// The red channel of each pixel of the last frame, row by row.
    /// </summary>
    public float[]? Ids { get; private set; }

    protected override void CollectCore(RenderContext context)
    {
        base.CollectCore(context);
        // The formats of the editor's pass
        Stage.Output = new RenderOutputDescription(PixelFormat.R32G32_Float, PixelFormat.D32_Float);
        context.RenderView.RenderStages.Add(Stage);
    }

    protected override void DrawCore(RenderContext context, RenderDrawContext drawContext)
    {
        var commandList = drawContext.CommandList;
        var (width, height) = (commandList.RenderTarget.Width, commandList.RenderTarget.Height);
        var allocator = drawContext.GraphicsContext.Allocator;
        var target = PushScopedResource(allocator.GetTemporaryTexture2D(width, height, Stage.Output.RenderTargetFormat0));
        var depth = PushScopedResource(allocator.GetTemporaryTexture2D(width, height, Stage.Output.DepthStencilFormat, TextureFlags.DepthStencil));

        using (drawContext.PushRenderTargetsAndRestore())
        {
            commandList.ResourceBarrierTransition(target, BarrierLayout.RenderTarget);
            commandList.ResourceBarrierTransition(depth, BarrierLayout.DepthStencilWrite);
            commandList.Clear(target, Color.Transparent);
            commandList.Clear(depth, DepthStencilClearOptions.DepthBuffer);
            commandList.SetRenderTargetAndViewport(depth, target);
            context.RenderSystem.Draw(drawContext, context.RenderView, Stage);
        }

        using var image = target.GetDataAsImage(commandList);
        var pixels = image.PixelBuffer[0];
        var ids = new float[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                ids[y * width + x] = pixels.GetPixel<Vector2>(x, y).X;
            }
        }
        Ids = ids;
    }
}
