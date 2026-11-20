// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using Avalonia.Platform;
using Stride.Graphics;

namespace Stride.AvaloniaUI.Rendering;

/// <summary>
/// The renderer of the Avalonia views, chosen once per process when Avalonia is set up.
/// </summary>
internal abstract class RendererBackend
{
    /// <summary>
    /// Creates a renderer and binds what Avalonia needs from it.
    /// </summary>
    /// <exception cref="InvalidOperationException">The Skia package, which every renderer needs for now, is missing.</exception>
    public static RendererBackend Create(AvaloniaUIRenderer renderer, IAvaloniaUISkia? skia)
    {
        // Fonts come from Skia whatever the renderer
        if (skia == null)
            throw new InvalidOperationException($"The {renderer} renderer of the Avalonia UI needs the Stride.AvaloniaUI.Skia package: reference it from the game.");

        return renderer switch
        {
            AvaloniaUIRenderer.Skia => skia.CreateWholeFrameRenderer(),
            _ => throw new ArgumentOutOfRangeException(nameof(renderer), renderer, "Unknown Avalonia UI renderer."),
        };
    }

    /// <summary>
    /// Gets the graphics Avalonia's compositor renders with, or <c>null</c> to render on the CPU.
    /// </summary>
    public virtual IPlatformGraphics? PlatformGraphics => null;

    /// <summary>
    /// Makes the renderer draw on a game's graphics device from the next frame on, if it can.
    /// </summary>
    /// <returns><c>true</c> if it draws on the device, <c>false</c> if it draws on the CPU.</returns>
    public virtual bool AttachGraphicsDevice(GraphicsDevice device) => false;

    /// <summary>
    /// Makes the renderer draw on the CPU from the next frame on, if it draws on a graphics device that goes.
    /// </summary>
    public virtual void DetachGraphicsDevice(GraphicsDevice device)
    {
    }

    /// <summary>
    /// Called before Avalonia renders the views of a frame.
    /// </summary>
    public virtual void BeginRender()
    {
    }

    /// <summary>
    /// Called after Avalonia rendered the views of a frame.
    /// </summary>
    public virtual void EndRender()
    {
    }

    /// <summary>
    /// Gets a function that says when a CPU render target created now is corrupted, so Avalonia creates it again.
    /// </summary>
    public virtual Func<bool> CaptureRenderTargetLoss() => static () => false;
}
