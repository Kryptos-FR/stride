// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Platform;
using Stride.AvaloniaUI.Rendering.Fallback;
using Stride.AvaloniaUI.Rendering.Geometry;

namespace Stride.AvaloniaUI.Skia.Fallback;

/// <summary>
/// The Skia fallback: Skia's objects made from the core's, and Skia's answers for them.
/// </summary>
internal sealed class SkiaRenderFallback : IRenderFallback
{
    private readonly SkiaGeometryConverter geometryConverter;

    public SkiaRenderFallback(IPlatformRenderInterface skia, IFontManagerImpl fontManager)
    {
        Skia = skia;
        FontManager = fontManager;
        geometryConverter = new SkiaGeometryConverter(this, skia);
    }

    /// <summary>
    /// Gets Skia's render interface, which makes Skia's objects.
    /// </summary>
    public IPlatformRenderInterface Skia { get; }

    /// <summary>
    /// Gets Skia's font manager.
    /// </summary>
    public IFontManagerImpl FontManager { get; }

    public IGeometryImpl GetGeometry(StrideGeometry geometry)
    {
        if (geometry is FallbackGeometry fallbackGeometry)
            return fallbackGeometry.Inner;
        if (geometry.FallbackObject is IGeometryImpl converted)
            return converted;

        converted = geometryConverter.Convert(geometry);
        geometry.FallbackObject = converted;
        return converted;
    }
}
