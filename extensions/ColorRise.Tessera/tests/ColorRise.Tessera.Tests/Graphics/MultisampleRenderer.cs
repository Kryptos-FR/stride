using Stride.Core.Mathematics;
using Stride.Graphics;
using Stride.Rendering;
using Stride.Rendering.Compositing;

namespace ColorRise.Tessera.Tests.Graphics;

/// <summary>
/// Draws the opaque then the transparent stage into multisampled targets, and resolves them into the output with the GPU's
/// own resolve.
/// </summary>
/// <remarks>
/// The forward renderer resolves its samples with a shader it compiles at run time, from sources that a test process does
/// not have; a resolve copy needs none.
/// </remarks>
internal sealed class MultisampleRenderer : SceneRendererBase
{
    private const PixelFormat DepthFormat = PixelFormat.D32_Float;

    public required RenderStage OpaqueStage { get; init; }

    public required RenderStage TransparentStage { get; init; }

    public required MultisampleCount Samples { get; init; }

    public Color Background { get; init; }

    protected override void CollectCore(RenderContext context)
    {
        base.CollectCore(context);
        SceneCameraRenderer.UpdateCameraToRenderView(context, context.RenderView, context.GetCurrentCamera());
        using (context.SaveRenderOutputAndRestore())
        {
            context.RenderOutput = new RenderOutputDescription(context.RenderOutput.RenderTargetFormat0, DepthFormat, Samples);
            OpaqueStage.OutputValidator.Validate(ref context.RenderOutput);
            TransparentStage.OutputValidator.Validate(ref context.RenderOutput);
        }
        context.RenderView.RenderStages.Add(OpaqueStage);
        context.RenderView.RenderStages.Add(TransparentStage);
    }

    protected override void DrawCore(RenderContext context, RenderDrawContext drawContext)
    {
        var commandList = drawContext.CommandList;
        var output = commandList.RenderTarget;
        var allocator = drawContext.GraphicsContext.Allocator;
        var color = PushScopedResource(allocator.GetTemporaryTexture2D(TextureDescription.New2D(output.Width, output.Height, 1, output.Format,
            TextureFlags.RenderTarget | TextureFlags.ShaderResource, 1, GraphicsResourceUsage.Default, Samples)));
        var depth = PushScopedResource(allocator.GetTemporaryTexture2D(TextureDescription.New2D(output.Width, output.Height, 1, DepthFormat,
            TextureFlags.DepthStencil, 1, GraphicsResourceUsage.Default, Samples)));

        using (drawContext.PushRenderTargetsAndRestore())
        {
            commandList.ResourceBarrierTransition(color, BarrierLayout.RenderTarget);
            commandList.ResourceBarrierTransition(depth, BarrierLayout.DepthStencilWrite);
            commandList.SetRenderTargetAndViewport(depth, color);
            commandList.Clear(color, Background);
            commandList.Clear(depth, DepthStencilClearOptions.DepthBuffer);
            context.RenderSystem.Draw(drawContext, context.RenderView, OpaqueStage);
            context.RenderSystem.Draw(drawContext, context.RenderView, TransparentStage);
        }
        commandList.CopyMultisample(color, 0, output, 0);
    }
}
