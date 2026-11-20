// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using Avalonia.Platform;
using Stride.Graphics;

namespace Stride.AvaloniaUI.Platform.Gpu;

/// <summary>
/// The graphics of the Stride platform: Skia on the graphics device of the game that runs, through one shared
/// <see cref="StrideSkiaGpu"/>, or on the CPU when there is no device Skia can share.
/// </summary>
/// <remarks>
/// Avalonia has one GPU per process; it follows the device of the last game that attached one.
/// </remarks>
internal sealed class StridePlatformGraphics : IPlatformGraphics
{
    private StrideSkiaGpu? current;
    private IPlatformGraphicsContext? currentProxy;

    public bool UsesSharedContext => true;

    /// <summary>
    /// Gets the current GPU.
    /// </summary>
    public StrideSkiaGpu? Current => current;

    public IPlatformGraphicsContext CreateContext() => throw new NotSupportedException("The Stride platform only has a shared graphics context.");

    public IPlatformGraphicsContext GetSharedContext()
    {
        if (current == null || current.IsLost)
            SetCurrent(StrideSkiaGpu.CreateWithoutDevice());
        return currentProxy!;
    }

    /// <summary>
    /// Makes Skia render on a graphics device from Avalonia's next frame on, if Skia can share it.
    /// </summary>
    /// <returns><c>true</c> if Skia renders on the device, <c>false</c> if it renders on the CPU.</returns>
    public bool Attach(GraphicsDevice device)
    {
        if (current?.Device == device && !current.IsLost)
            return true;

        current?.Lose();
        var gpu = StrideSkiaGpu.TryCreate(device);
        SetCurrent(gpu ?? StrideSkiaGpu.CreateWithoutDevice());
        return gpu != null;
    }

    /// <summary>
    /// Makes Skia render on the CPU from Avalonia's next frame on, if it renders on a graphics device that goes.
    /// </summary>
    /// <returns><c>true</c> if Skia rendered on the device.</returns>
    public bool Detach(GraphicsDevice device)
    {
        if (current?.Device != device)
            return false;

        current.Lose();
        current = null;
        currentProxy = null;
        return true;
    }

    private void SetCurrent(StrideSkiaGpu gpu)
    {
        // The previous one is lost, so Avalonia comes back for this one
        if (current is { IsLost: false } previous && previous != gpu)
            previous.Lose();
        current = gpu;
        currentProxy = SkiaGpuProxy.Create(gpu);
    }
}
