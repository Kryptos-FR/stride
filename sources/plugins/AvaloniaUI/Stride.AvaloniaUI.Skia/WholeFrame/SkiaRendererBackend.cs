// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using Avalonia.Platform;
using Stride.AvaloniaUI.Platform;
using Stride.AvaloniaUI.Rendering;
using Stride.Graphics;

namespace Stride.AvaloniaUI.Skia.WholeFrame;

/// <summary>
/// The whole-frame Skia renderer: Skia draws each view on the game's Vulkan device when it can share it, otherwise on the CPU.
/// </summary>
internal sealed class SkiaRendererBackend : RendererBackend
{
    private readonly StridePlatformGraphics graphics = new();
    private StrideSkiaGpu.QueueScope queueScope;

    public override IPlatformGraphics? PlatformGraphics => SkiaGpuProxy.IsSupported ? graphics : null;

    public override bool AttachGraphicsDevice(GraphicsDevice device)
    {
        if (!SkiaGpuProxy.IsSupported)
            return false;

        var previous = graphics.Current;
        var rendersOnDevice = graphics.Attach(device);
        if (graphics.Current != previous)
            StrideAvaloniaPlatform.RedrawTopLevels();
        return rendersOnDevice;
    }

    public override void DetachGraphicsDevice(GraphicsDevice device)
    {
        if (graphics.Detach(device))
            StrideAvaloniaPlatform.RedrawTopLevels();
    }

    // Skia submits its commands to the device's queue at various points of the rendering
    public override void BeginRender(GraphicsContext graphicsContext) => queueScope = graphics.Current?.LockQueue() ?? default;

    public override void EndRender()
    {
        queueScope.Dispose();
        queueScope = default;
    }

    public override Func<bool> CaptureRenderTargetLoss()
    {
        var gpu = graphics.Current;
        return gpu == null ? static () => false : () => gpu.IsLost;
    }
}
