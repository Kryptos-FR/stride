// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Platform;
using Stride.AvaloniaUI.Platform;
using Stride.AvaloniaUI.Rendering.Fallback;
using Stride.Core.Mathematics;
using Stride.Graphics;
using PixelFormat = Stride.Graphics.PixelFormat;

namespace Stride.AvaloniaUI.Rendering.Native;

/// <summary>
/// The render target of a top level on the game's graphics device: each frame the canvas draws into a texture the target
/// keeps, which the game draws.
/// </summary>
/// <remarks>
/// The texture has the scene's size and is made again when it changes, so the next frame is drawn whole; a scene of no
/// pixels draws nothing and hands no frame. The target is corrupted once its graphics are disposed, so Avalonia makes the
/// one of the CPU.
/// </remarks>
internal sealed class NativeTextureRenderTarget : IRenderTarget
{
    private readonly NativeGraphics graphics;
    private readonly FallbackChannel channel;
    private readonly StrideTopLevelImpl topLevel;
    private Texture? texture;

    public NativeTextureRenderTarget(NativeGraphics graphics, FallbackChannel channel, StrideTopLevelImpl topLevel)
    {
        this.graphics = graphics;
        this.channel = channel;
        this.topLevel = topLevel;
        graphics.AddRenderTarget(this);
    }

    public RenderTargetProperties Properties => new()
    {
        RetainsPreviousFrameContents = true,
        IsSuitableForDirectRendering = true,
    };

    public PlatformRenderTargetState PlatformRenderTargetState
        => graphics.IsDisposed ? PlatformRenderTargetState.Corrupted : PlatformRenderTargetState.Ready;

    public IDrawingContextImpl CreateDrawingContext(IRenderTarget.RenderTargetSceneInfo sceneInfo, out RenderTargetDrawingContextProperties properties)
    {
        var size = sceneInfo.Size;
        var dpi = new Vector(96 * sceneInfo.Scaling, 96 * sceneInfo.Scaling);
        if (size.Width <= 0 || size.Height <= 0 || graphics.IsDisposed)
        {
            properties = new RenderTargetDrawingContextProperties { PreviousFrameIsRetained = false };
            return new NativeDrawingContext(graphics, channel, null, dpi, null);
        }

        var retained = texture != null && texture.Width == size.Width && texture.Height == size.Height;
        if (!retained)
        {
            ReleaseTexture();
            texture = Texture.New2D(graphics.Device, size.Width, size.Height, PixelFormat.R8G8B8A8_UNorm, TextureFlags.RenderTarget | TextureFlags.ShaderResource);
            // Transparent where the first frame draws nothing, as the memory of the CPU frames
            graphics.Context?.CommandList.Clear(texture, new Color4(0, 0, 0, 0));
        }

        properties = new RenderTargetDrawingContextProperties { PreviousFrameIsRetained = retained };
        var frame = texture!;
        return new NativeDrawingContext(graphics, channel, frame, dpi, () => topLevel.OnGpuFrameRendered(frame, canViewAsSrgb: false));
    }

    /// <summary>
    /// Releases the texture, which the top level then forgets; the next frame makes another.
    /// </summary>
    public void ReleaseTexture()
    {
        if (texture == null)
            return;
        topLevel.OnGpuFrameReleased(texture);
        texture.Dispose();
        texture = null;
    }

    public void Dispose()
    {
        ReleaseTexture();
        graphics.RemoveRenderTarget(this);
    }
}
