using System.Runtime.InteropServices;
using Stride.Core.Mathematics;
using Stride.Graphics;
using Stride.Rendering;

namespace ColorRise.Tessera.Rendering;

/// <summary>
/// Draws quads given by their corners, in the order they are given, with one colour for all of them, and one draw call for
/// each run of quads of the same texture.
/// </summary>
/// <remarks>
/// The corners reach the GPU as they were given: the batch computes nothing from a centre and a size, so two quads that
/// share corners leave no gap between them, at any position of the camera.
/// </remarks>
public sealed class TileBatch : BatchBase<TileBatch.DrawInfo>
{
    private static readonly VertexDeclaration VertexLayout = new(VertexElement.Position<Vector4>(), VertexElement.TextureCoordinate<Vector2>());

    private Matrix viewProjection;
    private Color4 color;

    public TileBatch(GraphicsDevice device, int bufferElementCount = 1024, int batchCapacity = 64)
        : base(device, SpriteEffect.Bytecode, SpriteEffect.Bytecode,
            StaticQuadBufferInfo.CreateQuadBufferInfo("ColorRise.Tessera.TileBatch", false, bufferElementCount, batchCapacity), VertexLayout)
    {
    }

    /// <summary>
    /// Starts a batch whose quads all take one colour.
    /// </summary>
    /// <param name="color">The colour each texel is multiplied by, its alpha not premultiplied.</param>
    /// <param name="sampler">Linear and clamped when null.</param>
    /// <param name="blend">When null, blending for the alpha that <paramref name="premultipliedAlpha"/> says the textures have.</param>
    /// <param name="depthStencil">Depth read and not written when null.</param>
    /// <param name="rasterizer">No culling when null; a multisampled output takes one of its sample count.</param>
    /// <param name="premultipliedAlpha">Whether the colours of the textures are multiplied by their alpha.</param>
    public void Begin(GraphicsContext context, in Matrix viewProjection, Color4 color, SamplerState? sampler = null,
        BlendStateDescription? blend = null, DepthStencilStateDescription? depthStencil = null, RasterizerStateDescription? rasterizer = null,
        bool premultipliedAlpha = true)
    {
        CheckEndHasBeenCalled("begin");
        this.viewProjection = viewProjection;
        var linear = graphicsDevice.ColorSpace == ColorSpace.Linear ? color.ToLinear() : color;
        this.color = premultipliedAlpha ? Color4.PremultiplyAlpha(linear) : linear;
        Begin(context, null, SpriteSortMode.Deferred, blend ?? (premultipliedAlpha ? BlendStates.AlphaBlend : BlendStates.NonPremultiplied), sampler,
            depthStencil ?? DepthStencilStates.DepthRead, rasterizer ?? RasterizerStates.CullNone, 0);
    }

    /// <summary>
    /// Adds a quad to the batch, drawn at <see cref="BatchBase{TDrawInfo}.End"/>.
    /// </summary>
    public void Draw(Texture texture, in TileQuad quad)
    {
        ArgumentNullException.ThrowIfNull(texture);
        var element = new ElementInfo(StaticQuadBufferInfo.VertexByElement, StaticQuadBufferInfo.IndicesByElement, new DrawInfo { Quad = quad });
        Draw(texture, in element);
    }

    protected override void PrepareForRendering()
    {
        Parameters.Set(SpriteBaseKeys.MatrixTransform, viewProjection);
        Parameters.Set(SpriteEffectKeys.Color, color);
        base.PrepareForRendering();
    }

    protected override unsafe void UpdateBufferValuesFromElementInfo(ref ElementInfo elementInfo, IntPtr vertexPointer, IntPtr indexPointer, int vexterStartOffset)
    {
        // The order of the batch's static indices: top-left, top-right, bottom-left, bottom-right
        var vertex = (Vertex*)vertexPointer;
        ref var quad = ref elementInfo.DrawInfo.Quad;
        vertex[0] = new Vertex(quad.TopLeft, quad.UvTopLeft);
        vertex[1] = new Vertex(quad.TopRight, quad.UvTopRight);
        vertex[2] = new Vertex(quad.BottomLeft, quad.UvBottomLeft);
        vertex[3] = new Vertex(quad.BottomRight, quad.UvBottomRight);
    }

    /// <summary>
    /// What the batch keeps of a quad until it draws it.
    /// </summary>
    public struct DrawInfo
    {
        public TileQuad Quad;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct Vertex(Vector3 position, Vector2 textureCoordinate)
    {
        public readonly Vector4 Position = new(position, 1);
        public readonly Vector2 TextureCoordinate = textureCoordinate;
    }
}
