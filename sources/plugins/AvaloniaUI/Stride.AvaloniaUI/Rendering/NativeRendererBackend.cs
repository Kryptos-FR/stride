// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using Avalonia;
using Avalonia.Platform;
using Stride.AvaloniaUI.Platform;
using Stride.AvaloniaUI.Rendering.Fallback;
using Stride.AvaloniaUI.Rendering.Native;
using Stride.Core.Diagnostics;
using Stride.Graphics;

namespace Stride.AvaloniaUI.Rendering;

/// <summary>
/// The native renderer: Avalonia's objects are the core's. On a graphics device that can, the native canvas draws the frames
/// into textures and hands what it doesn't draw to the fallback in runs; otherwise the fallback draws whole frames on the CPU.
/// </summary>
internal sealed class NativeRendererBackend : RendererBackend
{
    private static readonly Logger Log = GlobalLogger.GetLogger("AvaloniaUI");

    private bool declineLogged;

    /// <summary>
    /// Creates the renderer and binds the core's render interface, and the fallback's font manager.
    /// </summary>
    public NativeRendererBackend(IRenderFallback fallback)
    {
        Channel = new FallbackChannel(fallback);
        AvaloniaLocator.CurrentMutable
            .Bind<IPlatformRenderInterface>().ToConstant(new StrideRenderInterface(Channel, () => Graphics))
            .Bind<IFontManagerImpl>().ToConstant(fallback.FontManager);
    }

    /// <summary>
    /// Gets the channel to the fallback, which counts the calls that go through it.
    /// </summary>
    public FallbackChannel Channel { get; }

    /// <summary>
    /// Gets what the native canvas keeps on the graphics device it draws on, or <c>null</c> when it draws on the CPU.
    /// </summary>
    public NativeGraphics? Graphics { get; private set; }

    /// <summary>
    /// Gets or sets the width and height of a page of the glyph atlas of the next device attached.
    /// </summary>
    internal int AtlasPageSize { get; set; } = GlyphAtlas.DefaultPageSize;

    /// <summary>
    /// Gets or sets the most pages of the glyph atlas of the next device attached.
    /// </summary>
    internal int AtlasMaxPages { get; set; } = GlyphAtlas.DefaultMaxPages;

    /// <summary>
    /// Draws the views on a device from the next frame on, when the canvas can draw on it (<see cref="NativeGraphics.GetReasonNotToDraw(GraphicsDevice)"/>).
    /// </summary>
    public override bool AttachGraphicsDevice(GraphicsDevice device) => AttachGraphicsDevice(device, NativeGraphics.GetReasonNotToDraw(device));

    /// <summary>
    /// Draws the views on a device from the next frame on, unless there is a reason not to, which is logged once.
    /// </summary>
    /// <param name="reasonNotToDraw">Why the canvas can't draw on the device, or <c>null</c> when it can.</param>
    internal bool AttachGraphicsDevice(GraphicsDevice device, string? reasonNotToDraw)
    {
        if (Graphics is { } current)
            return current.Device == device;
        if (reasonNotToDraw != null)
        {
            if (!declineLogged)
            {
                declineLogged = true;
                Log.Info($"The native canvas of the Avalonia UI can't draw on the graphics device ({reasonNotToDraw}); the views are drawn on the CPU.");
            }
            return false;
        }

        try
        {
            Graphics = new NativeGraphics(device, AtlasPageSize, AtlasMaxPages);
        }
        catch (Exception exception)
        {
            Log.Error("The native canvas of the Avalonia UI can't draw on the graphics device; the views are drawn on the CPU.", exception);
            return false;
        }
        // The render targets of the CPU report themselves corrupted, and Avalonia makes those of the device
        StrideAvaloniaPlatform.RedrawTopLevels();
        return true;
    }

    public override void DetachGraphicsDevice(GraphicsDevice device)
    {
        if (Graphics is not { } graphics || graphics.Device != device)
            return;
        Graphics = null;
        graphics.Dispose();
        StrideAvaloniaPlatform.RedrawTopLevels();
    }

    // Without a graphics context, a frame on the device draws nothing
    public override void BeginRender(GraphicsContext? graphicsContext)
    {
        if (Graphics is not { } graphics)
            return;
        graphics.Context = graphicsContext;
        graphics.BeginFrame();
    }

    public override void EndRender()
    {
        if (Graphics != null)
            Graphics.Context = null;
    }

    // A render target of the CPU made now is corrupted once a device is attached; one of a device, once it is detached
    public override Func<bool> CaptureRenderTargetLoss()
    {
        var created = Graphics;
        return () => !ReferenceEquals(Graphics, created);
    }
}
