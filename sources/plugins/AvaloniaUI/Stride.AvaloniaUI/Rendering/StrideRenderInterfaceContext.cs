// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Platform.Surfaces;
using Stride.AvaloniaUI.Rendering.Bitmaps;
using Stride.AvaloniaUI.Rendering.Fallback;

namespace Stride.AvaloniaUI.Rendering;

/// <summary>
/// The render context of the native renderer, which makes its render targets.
/// </summary>
internal sealed class StrideRenderInterfaceContext(FallbackChannel channel) : IPlatformRenderInterfaceContext
{
    public bool IsLost => false;

    public IReadOnlyDictionary<Type, object> PublicFeatures { get; } = new Dictionary<Type, object>();

    public PixelSize? MaxOffscreenRenderTargetPixelSize => null;

    /// <summary>
    /// Makes a render target over the first framebuffer surface, the memory of a top level.
    /// </summary>
    public IRenderTarget CreateRenderTarget(IEnumerable<IPlatformRenderSurface> surfaces)
    {
        var surface = surfaces.OfType<IFramebufferPlatformSurface>().FirstOrDefault()
            ?? throw new NotSupportedException("The native renderer of the Avalonia UI draws only into framebuffer surfaces.");
        return new NativeFramebufferRenderTarget(channel, surface.CreateFramebufferRenderTarget());
    }

    public bool IsReadyToCreateRenderTarget(IEnumerable<IPlatformRenderSurface> surfaces) => surfaces.OfType<IFramebufferPlatformSurface>().Any();

    /// <summary>
    /// Makes a bitmap to draw into, such as the cache of a visual, at a DPI of 96 times the scaling.
    /// </summary>
    public IDrawingContextLayerImpl CreateOffscreenRenderTarget(PixelSize pixelSize, Vector scaling, bool enableTextAntialiasing)
        => new StrideRenderTargetBitmap(channel, pixelSize, scaling * 96);

    public object? TryGetFeature(Type featureType) => null;

    public void Dispose()
    {
    }
}
