// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using Avalonia.Media;
using Avalonia.Platform;
using Stride.AvaloniaUI.Rendering.Bitmaps;
using Stride.AvaloniaUI.Rendering.Fallback;
using Stride.AvaloniaUI.Rendering.Geometry;
using Stride.AvaloniaUI.Rendering.Text;

namespace Stride.AvaloniaUI.Skia.Fallback;

/// <summary>
/// The Skia fallback: Skia's objects made from the core's, and Skia's answers for them.
/// </summary>
internal sealed class SkiaRenderFallback : IRenderFallback
{
    private readonly SkiaGeometryConverter geometryConverter;
    private readonly Lazy<IPlatformRenderInterfaceContext> cpuContext;

    public SkiaRenderFallback(IPlatformRenderInterface skia, IFontManagerImpl fontManager)
    {
        Skia = skia;
        FontManager = fontManager;
        geometryConverter = new SkiaGeometryConverter(this, skia);
        cpuContext = new Lazy<IPlatformRenderInterfaceContext>(() => skia.CreateBackendContext(null));
    }

    /// <summary>
    /// Gets Skia's render interface, which makes Skia's objects.
    /// </summary>
    public IPlatformRenderInterface Skia { get; }

    /// <summary>
    /// Gets Skia's font manager.
    /// </summary>
    public IFontManagerImpl FontManager { get; }

    /// <summary>
    /// Gets Skia's context that draws on the CPU, which makes the render targets of the canvases and the layers of scene brushes.
    /// </summary>
    internal IPlatformRenderInterfaceContext CpuContext => cpuContext.Value;

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

    /// <summary>
    /// Gets Skia's copy of the pixels a bitmap of the core has now, or <c>null</c> when it has none (0×0).
    /// </summary>
    internal IBitmapImpl? GetBitmap(StrideBitmap bitmap) => GetBitmapReference(bitmap)?.SkiaBitmap;

    /// <summary>
    /// Gets Skia's copy of the pixels a bitmap of the core has now as the source of an image brush, or <c>null</c> when it has
    /// none (0×0).
    /// </summary>
    internal SkiaBitmapReference? GetBitmapReference(StrideBitmap bitmap)
    {
        if (bitmap.FallbackObject is not SkiaBitmapCache cache)
        {
            cache = new SkiaBitmapCache();
            bitmap.FallbackObject = cache;
        }
        return cache.Get(Skia, bitmap);
    }

    public IGlyphRunImpl GetGlyphRun(StrideGlyphRun glyphRun)
    {
        if (glyphRun.FallbackObject is IGlyphRunImpl converted)
            return converted;

        converted = Skia.CreateGlyphRun(glyphRun.GlyphTypeface, glyphRun.FontRenderingEmSize, glyphRun.GlyphInfos, glyphRun.BaselineOrigin);
        glyphRun.FallbackObject = converted;
        return converted;
    }

    public IGeometryImpl BuildGlyphRunGeometry(GlyphRun glyphRun) => Skia.BuildGlyphRunGeometry(glyphRun);

    /// <summary>
    /// Makes a canvas that draws into the target's memory, or that draws nothing when the target has no pixels.
    /// </summary>
    public IFallbackCanvas CreateCanvas(FallbackTarget target)
    {
        if (target.Address == IntPtr.Zero || target.Size.Width <= 0 || target.Size.Height <= 0)
            return new EmptyFallbackCanvas();
        return new SkiaFallbackCanvas(this, target);
    }
}
