using Stride.Core.Mathematics;
using Stride.Graphics;
using Stride.Rendering;

namespace ColorRise.Tessera.Rendering;

/// <summary>
/// One batch for each run of quads of the same colour, sampler and alpha, in a stage of the render features; with a picking
/// texture, every quad is drawn with it, and the colour is an id.
/// </summary>
internal struct TileBatchRun(TileBatch batch, RenderDrawContext context, Matrix viewProjection, RasterizerStateDescription? rasterizer,
    Texture? pickingTexture)
{
    private bool begun;
    private Color4 color;
    private TileSampler sampler;
    private bool premultipliedAlpha;

    /// <summary>
    /// The rasterizer of a stage's output: null for an output that is not multisampled, else one of its sample count, as the
    /// sprites do.
    /// </summary>
    public static RasterizerStateDescription? Rasterizer(RenderOutputDescription output)
    {
        if (output.MultisampleCount == MultisampleCount.None)
        {
            return null;
        }
        var rasterizer = RasterizerStates.CullNone;
        rasterizer.MultisampleCount = output.MultisampleCount;
        rasterizer.MultisampleAntiAliasLine = true;
        return rasterizer;
    }

    /// <summary>
    /// Draws a quad, in a new batch when the colour, the sampler or the alpha is not the one of the batch begun.
    /// </summary>
    public void Draw(Texture texture, in TileQuad quad, Color4 color, TileSampler sampler, bool premultipliedAlpha)
    {
        if (!begun || color != this.color || sampler != this.sampler || premultipliedAlpha != this.premultipliedAlpha)
        {
            End();
            (this.color, this.sampler, this.premultipliedAlpha) = (color, sampler, premultipliedAlpha);
            if (pickingTexture is not null)
            {
                batch.BeginPicking(context.GraphicsContext, viewProjection, color);
            }
            else
            {
                var states = context.GraphicsDevice.SamplerStates;
                batch.Begin(context.GraphicsContext, viewProjection, color, sampler == TileSampler.Linear ? states.LinearClamp : states.PointClamp,
                    rasterizer: rasterizer, premultipliedAlpha: premultipliedAlpha);
            }
            begun = true;
        }
        batch.Draw(pickingTexture ?? texture, quad);
    }

    /// <summary>
    /// Ends the batch begun, if there is one.
    /// </summary>
    public void End()
    {
        if (begun)
        {
            batch.End();
            begun = false;
        }
    }
}
