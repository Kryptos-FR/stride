// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using Avalonia.Platform;
using Avalonia.Platform.Surfaces;
using Stride.AvaloniaUI.Rendering.Drawing;
using Stride.AvaloniaUI.Rendering.Fallback;

namespace Stride.AvaloniaUI.Rendering;

/// <summary>
/// The render target of a top level for the native renderer: each frame draws into the memory the top level locks.
/// </summary>
internal sealed class NativeFramebufferRenderTarget(FallbackChannel channel, IFramebufferRenderTarget framebuffer) : IRenderTarget
{
    // Drawn directly, without a layer, as Skia's framebuffer render target
    public RenderTargetProperties Properties => new()
    {
        RetainsPreviousFrameContents = framebuffer.RetainsFrameContents,
        IsSuitableForDirectRendering = true,
    };

    public PlatformRenderTargetState PlatformRenderTargetState => ((IPlatformRenderSurfaceRenderTarget)framebuffer).State;

    public IDrawingContextImpl CreateDrawingContext(IRenderTarget.RenderTargetSceneInfo sceneInfo, out RenderTargetDrawingContextProperties properties)
    {
        var locked = framebuffer.Lock(sceneInfo, out var lockProperties);
        if (locked.AlphaFormat != AlphaFormat.Premul)
        {
            locked.Dispose();
            throw new NotSupportedException($"The native renderer of the Avalonia UI draws premultiplied frames, not {locked.AlphaFormat}.");
        }

        properties = new RenderTargetDrawingContextProperties { PreviousFrameIsRetained = lockProperties.PreviousFrameIsRetained };
        IFallbackCanvas canvas;
        try
        {
            canvas = channel.Fallback.CreateCanvas(new FallbackTarget(locked.Address, locked.Size, locked.RowBytes, locked.Format, locked.Dpi, lockProperties.PreviousFrameIsRetained));
        }
        catch
        {
            locked.Dispose();
            throw;
        }
        return new RecordingDrawingContext(channel, canvas, locked.Dpi, locked.Dispose);
    }

    public void Dispose() => framebuffer.Dispose();
}
