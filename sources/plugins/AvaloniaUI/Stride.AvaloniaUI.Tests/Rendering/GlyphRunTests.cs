// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Stride.AvaloniaUI.Rendering;
using Stride.AvaloniaUI.Rendering.Fallback;
using Stride.AvaloniaUI.Rendering.Text;
using Xunit;

namespace Stride.AvaloniaUI.Tests.Rendering;

/// <summary>
/// Tests the glyph runs of the core against Skia's.
/// </summary>
public class GlyphRunTests
{
    private static readonly Point Origin = new(10.25, 40.5);

    [Theory]
    [InlineData("Hamburgefonstiv", 9, FontWeight.Normal)]
    [InlineData("Hamburgefonstiv", 14, FontWeight.Bold)]
    [InlineData("j_gy(Q)", 24, FontWeight.Normal)]
    [InlineData("Ёлка Жук", 12, FontWeight.Normal)]
    [InlineData("WAVE", 72, FontWeight.Bold)]
    public void GlyphRunBoundsAreSkiasWithinAPixel(string text, double size, FontWeight weight) => TestAvalonia.Run(() =>
    {
        var (typeface, glyphs) = Shape(text, size, weight);

        using var core = new StrideGlyphRun(SkiaOracle.CreateChannel(), typeface, size, glyphs, Origin);
        using var skia = SkiaOracle.RenderInterface.CreateGlyphRun(typeface, size, glyphs, Origin);

        // Skia's bounds are those of the hinted glyphs
        AssertClose(skia.Bounds, core.Bounds, 1);
    });

    [Fact]
    public void GlyphRunWithNoGlyphsHasNoBounds() => TestAvalonia.Run(() =>
    {
        var (typeface, _) = Shape("a", 12, FontWeight.Normal);

        using var glyphRun = new StrideGlyphRun(SkiaOracle.CreateChannel(), typeface, 12, [], Origin);

        Assert.Equal(default, glyphRun.Bounds);
        Assert.Empty(glyphRun.GlyphInfos);
    });

    [Fact]
    public void GlyphGeometryAndIntersectionsAreSkiasAndCounted() => TestAvalonia.Run(() =>
    {
        const string text = "Hamburgefonstiv";
        var (typeface, glyphs) = Shape(text, 24, FontWeight.Normal);
        var channel = SkiaOracle.CreateChannel();
        var renderInterface = new StrideRenderInterface(channel);
        var glyphRun = new GlyphRun(typeface, 24, text.AsMemory(), glyphs, Origin);

        Assert.Equal(SkiaOracle.RenderInterface.BuildGlyphRunGeometry(glyphRun).Bounds, renderInterface.BuildGlyphRunGeometry(glyphRun).Bounds);

        using var core = renderInterface.CreateGlyphRun(typeface, 24, glyphs, Origin);
        using var skia = SkiaOracle.RenderInterface.CreateGlyphRun(typeface, 24, glyphs, Origin);
        Assert.Equal(skia.GetIntersections(-2, 2), core.GetIntersections(-2, 2));

        Assert.Equal(1, channel.GetCount(DrawCallKind.GlyphRunGeometry));
        Assert.Equal(1, channel.GetCount(DrawCallKind.GlyphRunIntersections));
    });

    /// <summary>
    /// Shapes text in Inter with Avalonia's text shaper.
    /// </summary>
    internal static (GlyphTypeface Typeface, GlyphInfo[] Glyphs) Shape(string text, double size, FontWeight weight)
    {
        var typeface = new Typeface("fonts:Inter#Inter", FontStyle.Normal, weight).GlyphTypeface;
        using var shaped = TextShaper.Current.ShapeText(text, new TextShaperOptions(typeface, size));
        return (typeface, shaped.ToArray());
    }

    private static void AssertClose(Rect expected, Rect actual, double tolerance)
    {
        Assert.True(Math.Abs(expected.Left - actual.Left) <= tolerance && Math.Abs(expected.Top - actual.Top) <= tolerance
            && Math.Abs(expected.Right - actual.Right) <= tolerance && Math.Abs(expected.Bottom - actual.Bottom) <= tolerance,
            $"Expected bounds {expected}, got {actual}.");
    }
}
