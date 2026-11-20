// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.TextFormatting;
using Avalonia.Platform;
using Stride.AvaloniaUI.Rendering.Bitmaps;
using Stride.AvaloniaUI.Rendering.Fallback;
using Stride.AvaloniaUI.Rendering.Geometry;
using Stride.AvaloniaUI.Rendering.Text;

namespace Stride.AvaloniaUI.Rendering;

/// <summary>
/// The render interface of the native renderer: it makes the core's geometry, bitmaps and glyph runs.
/// </summary>
internal sealed class StrideRenderInterface(FallbackChannel channel) : IPlatformRenderInterface
{
    /// <summary>
    /// Gets the channel to the fallback, which does what the core can't do yet.
    /// </summary>
    public FallbackChannel Channel { get; } = channel;

    public bool SupportsIndividualRoundRects => true;

    public AlphaFormat DefaultAlphaFormat => AlphaFormat.Premul;

    public PixelFormat DefaultPixelFormat => PixelFormat.Bgra8888;

    public bool SupportsRegions => false;

    public IGeometryImpl CreateEllipseGeometry(Rect rect) => new StrideEllipseGeometry(Channel, rect);

    public IGeometryImpl CreateLineGeometry(Point p1, Point p2) => new StrideLineGeometry(Channel, p1, p2);

    public IGeometryImpl CreateRectangleGeometry(Rect rect) => new StrideRectangleGeometry(Channel, rect);

    public IStreamGeometryImpl CreateStreamGeometry() => new StrideStreamGeometry(Channel);

    public IGeometryImpl CreateGeometryGroup(FillRule fillRule, IReadOnlyList<IGeometryImpl> children)
        => new StrideGeometryGroup(Channel, fillRule, children.Select(child => (StrideGeometry)child).ToArray());

    public IGeometryImpl CreateCombinedGeometry(GeometryCombineMode combineMode, IGeometryImpl g1, IGeometryImpl g2)
        => new StrideCombinedGeometry(Channel, combineMode, (StrideGeometry)g1, (StrideGeometry)g2);

    public IGeometryImpl BuildGlyphRunGeometry(GlyphRun glyphRun)
    {
        Channel.Count(DrawCallKind.GlyphRunGeometry);
        return new FallbackGeometry(Channel, Channel.Fallback.BuildGlyphRunGeometry(glyphRun));
    }

    public IRenderTargetBitmapImpl CreateRenderTargetBitmap(PixelSize size, Vector dpi) => new StrideRenderTargetBitmap(Channel, size, dpi);

    public IWriteableBitmapImpl CreateWriteableBitmap(PixelSize size, Vector dpi, PixelFormat format, AlphaFormat alphaFormat)
        => new StrideBitmap(size, dpi, format, alphaFormat);

    public IBitmapImpl LoadBitmap(string fileName) => LoadWriteableBitmap(fileName);

    public IBitmapImpl LoadBitmap(Stream stream) => StrideBitmapCodec.Load(stream);

    public IBitmapImpl LoadBitmap(PixelFormat format, AlphaFormat alphaFormat, IntPtr data, PixelSize size, Vector dpi, int stride)
        => StrideBitmap.Copy(format, alphaFormat, data, size, dpi, stride);

    public IBitmapImpl LoadBitmapToWidth(Stream stream, int width, BitmapInterpolationMode interpolationMode = BitmapInterpolationMode.HighQuality)
        => LoadWriteableBitmapToWidth(stream, width, interpolationMode);

    public IBitmapImpl LoadBitmapToHeight(Stream stream, int height, BitmapInterpolationMode interpolationMode = BitmapInterpolationMode.HighQuality)
        => LoadWriteableBitmapToHeight(stream, height, interpolationMode);

    public IWriteableBitmapImpl LoadWriteableBitmap(string fileName)
    {
        using var stream = File.OpenRead(fileName);
        return StrideBitmapCodec.Load(stream);
    }

    public IWriteableBitmapImpl LoadWriteableBitmap(Stream stream) => StrideBitmapCodec.Load(stream);

    public IWriteableBitmapImpl LoadWriteableBitmapToWidth(Stream stream, int width, BitmapInterpolationMode interpolationMode = BitmapInterpolationMode.HighQuality)
    {
        using var bitmap = StrideBitmapCodec.Load(stream);
        var height = (int)((double)bitmap.PixelSize.Height / bitmap.PixelSize.Width * width);
        return StrideBitmapCodec.Resize(bitmap, new PixelSize(width, height), interpolationMode);
    }

    public IWriteableBitmapImpl LoadWriteableBitmapToHeight(Stream stream, int height, BitmapInterpolationMode interpolationMode = BitmapInterpolationMode.HighQuality)
    {
        using var bitmap = StrideBitmapCodec.Load(stream);
        var width = (int)((double)bitmap.PixelSize.Width / bitmap.PixelSize.Height * height);
        return StrideBitmapCodec.Resize(bitmap, new PixelSize(width, height), interpolationMode);
    }

    public IBitmapImpl ResizeBitmap(IBitmapImpl bitmapImpl, PixelSize destinationSize, BitmapInterpolationMode interpolationMode = BitmapInterpolationMode.HighQuality)
        => bitmapImpl is StrideBitmap bitmap
            ? StrideBitmapCodec.Resize(bitmap, destinationSize, interpolationMode)
            : throw new ArgumentException("The bitmap was not made by the native renderer of the Avalonia UI.", nameof(bitmapImpl));

    public IGlyphRunImpl CreateGlyphRun(GlyphTypeface glyphTypeface, double fontRenderingEmSize, IReadOnlyList<GlyphInfo> glyphInfos, Point baselineOrigin)
        => new StrideGlyphRun(Channel, glyphTypeface, fontRenderingEmSize, glyphInfos, baselineOrigin);

    // Any graphics context: the native renderer draws on the CPU, through the fallback
    public IPlatformRenderInterfaceContext CreateBackendContext(IPlatformGraphicsContext? graphicsApiContext) => new StrideRenderInterfaceContext(Channel);

    public bool IsSupportedBitmapPixelFormat(PixelFormat format) => format == PixelFormat.Bgra8888 || format == PixelFormat.Rgba8888;

    public IPlatformRenderInterfaceRegion CreateRegion() => throw new NotSupportedException("The native renderer of the Avalonia UI has no regions.");
}
