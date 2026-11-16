// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.IO;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Stride.AvaloniaUI.Rendering;
using Stride.AvaloniaUI.Rendering.Bitmaps;
using Stride.AvaloniaUI.Rendering.Geometry;
using Stride.AvaloniaUI.Rendering.Text;
using Xunit;

namespace Stride.AvaloniaUI.Tests.Rendering;

/// <summary>
/// Tests the render interface of the core.
/// </summary>
public class RenderInterfaceTests
{
    [Fact]
    public void RenderInterfaceMakesCoreObjects() => TestAvalonia.Run(() =>
    {
        var renderInterface = new StrideRenderInterface(SkiaOracle.CreateChannel());

        Assert.IsType<StrideStreamGeometry>(renderInterface.CreateStreamGeometry());

        using var writeable = renderInterface.CreateWriteableBitmap(new PixelSize(2, 3), new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul);
        var bitmap = Assert.IsType<StrideBitmap>(writeable);
        Assert.Equal(new PixelSize(2, 3), bitmap.PixelSize);
        Assert.Equal(PixelFormat.Rgba8888, bitmap.Format);
        Assert.Equal(AlphaFormat.Unpremul, bitmap.AlphaFormat);

        using var loaded = renderInterface.LoadBitmap(new MemoryStream(BitmapTests.MakePng()));
        Assert.IsType<StrideBitmap>(loaded);

        var (typeface, glyphs) = GlyphRunTests.Shape("Text", 12, FontWeight.Normal);
        using var glyphRun = renderInterface.CreateGlyphRun(typeface, 12, glyphs, default);
        Assert.IsType<StrideGlyphRun>(glyphRun);

        Assert.False(renderInterface.SupportsRegions);
        Assert.Throws<NotSupportedException>(() => renderInterface.CreateRegion());
        Assert.Equal(PixelFormat.Bgra8888, renderInterface.DefaultPixelFormat);
        Assert.Equal(AlphaFormat.Premul, renderInterface.DefaultAlphaFormat);
        Assert.True(renderInterface.IsSupportedBitmapPixelFormat(PixelFormat.Bgra8888));
        Assert.True(renderInterface.IsSupportedBitmapPixelFormat(PixelFormat.Rgba8888));
        Assert.False(renderInterface.IsSupportedBitmapPixelFormat(PixelFormat.Rgb565));
    });
}
