// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Platform.Surfaces;
using Stride.AvaloniaUI.Platform;
using Stride.AvaloniaUI.Rendering.Bitmaps;
using Stride.AvaloniaUI.Rendering.Fallback;
using Stride.AvaloniaUI.Rendering.Native;

namespace Stride.AvaloniaUI.Rendering;

/// <summary>
/// The render context of the native renderer, which makes its render targets.
/// </summary>
/// <param name="graphics">Gets what the native canvas keeps on the game's graphics device, or <c>null</c> to draw on the CPU.</param>
internal sealed class StrideRenderInterfaceContext(FallbackChannel channel, Func<NativeGraphics?>? graphics = null) : IPlatformRenderInterfaceContext
{
    public bool IsLost => false;

    public IReadOnlyDictionary<Type, object> PublicFeatures { get; } = new Dictionary<Type, object>();

    public PixelSize? MaxOffscreenRenderTargetPixelSize => null;

    /// <summary>
    /// Makes a render target for a top level: a texture on the game's graphics device when the canvas draws there, otherwise
    /// the memory of the first framebuffer surface.
    /// </summary>
    public IRenderTarget CreateRenderTarget(IEnumerable<IPlatformRenderSurface> surfaces)
    {
        if (graphics?.Invoke() is { IsDisposed: false } nativeGraphics && surfaces.OfType<StrideTopLevelImpl>().FirstOrDefault() is { } topLevel)
            return new NativeTextureRenderTarget(nativeGraphics, channel, topLevel);

        var surface = surfaces.OfType<IFramebufferPlatformSurface>().FirstOrDefault()
            ?? throw new NotSupportedException("The native renderer of the Avalonia UI draws only into framebuffer surfaces.");
        return new NativeFramebufferRenderTarget(channel, surface.CreateFramebufferRenderTarget());
    }

    public bool IsReadyToCreateRenderTarget(IEnumerable<IPlatformRenderSurface> surfaces) => surfaces.OfType<IFramebufferPlatformSurface>().Any();

    /// <summary>
    /// Makes a bitmap to draw into, such as the cache of a visual, at a DPI of 96 times the scaling.
    /// </summary>
    /// <remarks>
    /// Without <paramref name="enableTextAntialiasing"/>, text is drawn without subpixel antialiasing, as the bitmap is drawn over other content later.
    /// </remarks>
    public IDrawingContextLayerImpl CreateOffscreenRenderTarget(PixelSize pixelSize, Vector scaling, bool enableTextAntialiasing)
        => new StrideRenderTargetBitmap(channel, pixelSize, scaling * 96, grayscaleText: !enableTextAntialiasing);

    public object? TryGetFeature(Type featureType) => null;

    public void Dispose()
    {
    }
}
