// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using Stride.AvaloniaUI.Rendering.Drawing;
using Stride.AvaloniaUI.Rendering.Fallback;
using Stride.AvaloniaUI.Rendering.Text;
using Color4 = Stride.Core.Mathematics.Color4;
using Rectangle = Stride.Core.Mathematics.Rectangle;

namespace Stride.AvaloniaUI.Rendering.Native;

/// <summary>
/// Turns glyph runs into glyph quads of the atlas, placed as Skia places glyph masks on the CPU.
/// </summary>
/// <remarks>
/// <para>
/// A run is drawn natively with a solid brush, under a translation and a uniform positive scale, with glyphs of at most 256
/// pixels, in a typeface the atlas can open. Its text is grayscale: a request for subpixel text gets grayscale text, as the
/// fallback draws it.
/// </para>
/// <para>
/// Skia maps each glyph's position, its run's origin added, to the target's pixels in floats, adds a bias, and takes the
/// floor as the glyph's pixel and the quarter of a pixel past it as the glyph's subpixel position. Antialiased text is
/// biased by an eighth of a pixel along x, where it has four subpixel positions, and by half a pixel along y, where its
/// baseline snaps to whole pixels; without that snap, y is as x. Aliased text is biased by half a pixel both ways.
/// </para>
/// </remarks>
internal static class NativeText
{
    // Skia draws glyphs whose text matrix scales an axis beyond this many pixels as paths
    private const float LargestMask = 256;

    // Skia's skew of an oblique simulation
    private const float ObliqueSkew = -0.3f;

    // Past this many pixels from the origin, a glyph is on no target
    private const float FarthestPixel = 1 << 24;

    /// <summary>
    /// Gets the glyph quads of a glyph run, or the reason it goes to the fallback.
    /// </summary>
    /// <param name="glyphs">The list the quads are added to; a run without ink adds none.</param>
    /// <returns><c>null</c> when the run is drawn natively, otherwise why it isn't.</returns>
    public static FallbackReason? TryGet(DrawGlyphRunCommand command, Matrix transform, RenderOptions renderOptions,
        TextOptions textOptions, GlyphAtlas atlas, List<CanvasGlyph> glyphs)
    {
        // Skia draws nothing without a brush
        if (command.Foreground == null)
            return null;
        if (command.Foreground is not ISolidColorBrush brush)
            return FallbackReason.Brush;
        if (transform.ContainsPerspective())
            return FallbackReason.Perspective;
        if (!IsUniformScale(transform))
            return FallbackReason.TextTransform;

        var run = (StrideGlyphRun)command.GlyphRun;
        var scale = (float)transform.M11;
        if (IsLarge((float)run.FontRenderingEmSize, scale, run.GlyphTypeface.FontSimulations))
            return FallbackReason.LargeText;
        if (!atlas.TryGetFont(run.GlyphTypeface, out var font, out var fontReason))
            return fontReason;

        var color = brush.Color;
        var alpha = (byte)(color.A * brush.Opacity);
        if (alpha == 0 || run.GlyphInfos.Count == 0)
            return null;

        var aliased = GetRenderingMode(renderOptions, textOptions) == TextRenderingMode.Alias;
        var snapped = textOptions.BaselinePixelAlignment != BaselinePixelAlignment.Unaligned;
        var rendering = aliased ? GlyphRendering.Alias : GlyphRendering.Antialias;
        var size26Dot6 = GlyphRasterizer.ToSize26Dot6((float)run.FontRenderingEmSize * scale);
        var luminance = aliased ? (byte)0 : TextGamma.GetLuminance(color);
        var hinting = textOptions.TextHintingMode == TextHintingMode.Unspecified ? TextHintingMode.Strong : textOptions.TextHintingMode;

        // The draw matrix of Skia: the transform in floats, moved to the run's origin, then by the bias
        var biasX = aliased ? 0.5f : 0.125f;
        var biasY = aliased || snapped ? 0.5f : 0.125f;
        var scaleY = (float)transform.M22;
        var originX = (float)transform.M31 + scale * (float)run.BaselineOrigin.X + biasX;
        var originY = (float)transform.M32 + scaleY * (float)run.BaselineOrigin.Y + biasY;
        var premultiplied = new Color4(color.R / 255f, color.G / 255f, color.B / 255f, 1) * (alpha / 255f);

        var start = glyphs.Count;
        var advance = 0.0;
        foreach (var info in run.GlyphInfos)
        {
            var x = (float)(advance + info.GlyphOffset.X) * scale + originX;
            var y = (float)info.GlyphOffset.Y * scaleY + originY;
            advance += info.GlyphAdvance;
            if (!(Math.Abs(x) < FarthestPixel && Math.Abs(y) < FarthestPixel))
                continue;

            var subpixelX = aliased ? (byte)0 : GetSubpixel(x);
            var subpixelY = aliased || snapped ? (byte)0 : GetSubpixel(y);
            var key = new GlyphKey(font, info.GlyphIndex, size26Dot6, rendering, hinting, subpixelX, luminance, subpixelY);
            if (!atlas.TryGet(key, out var glyph, out var reason))
            {
                glyphs.RemoveRange(start, glyphs.Count - start);
                return reason;
            }
            if (glyph.Page is not { } page)
                continue;

            var image = glyph.Image;
            var pixels = new Rectangle((int)MathF.Floor(x) + image.Left, (int)MathF.Floor(y) + image.Top, image.Width, image.Height);
            glyphs.Add(new CanvasGlyph(pixels, page, glyph.Texel, premultiplied));
        }
        return null;
    }

    /// <summary>
    /// Gets the rendering mode of text, as Avalonia's Skia context without subpixel text picks it: the text options' mode,
    /// else antialiased unless the edges are aliased, else the render options' obsolete mode, else aliased; subpixel text
    /// is antialiased.
    /// </summary>
    internal static TextRenderingMode GetRenderingMode(RenderOptions renderOptions, TextOptions textOptions)
    {
        var mode = textOptions.TextRenderingMode;
        if (mode == TextRenderingMode.Unspecified && renderOptions.EdgeMode != EdgeMode.Aliased)
            mode = TextRenderingMode.Antialias;
#pragma warning disable CS0618 // The render options' text mode is obsolete, and Skia's context still reads it
        if (mode == TextRenderingMode.Unspecified)
            mode = renderOptions.TextRenderingMode;
#pragma warning restore CS0618
        return mode is TextRenderingMode.Alias or TextRenderingMode.Unspecified ? TextRenderingMode.Alias : TextRenderingMode.Antialias;
    }

    // A translation and a uniform positive scale, in the floats of Skia's matrix: Skia snaps the baseline and makes square
    // glyphs only for those
    private static bool IsUniformScale(Matrix m)
        => (float)m.M12 == 0 && (float)m.M21 == 0 && (float)m.M11 > 0 && (float)m.M11 == (float)m.M22;

    // Skia's test for glyphs drawn as paths: an axis of the text matrix (the size, and the oblique skew) under the scale
    // longer than the largest mask
    private static bool IsLarge(float size, float scale, FontSimulations simulations)
    {
        var scaled = size * scale;
        var skew = (simulations & FontSimulations.Oblique) != 0 ? ObliqueSkew * size * scale : 0;
        const float LargestSquared = LargestMask * LargestMask;
        return scaled * scaled > LargestSquared || skew * skew + scaled * scaled > LargestSquared;
    }

    // The quarter of a pixel a position is past its whole pixel, from its 16.16 fixed-point value as Skia packs it,
    // saturated to the ints a float holds
    private static byte GetSubpixel(float position)
    {
        const float Largest = 2147483520f;
        var value = position * 65536f;
        var fixedPoint = value >= Largest ? (int)Largest : value <= -Largest ? -(int)Largest : (int)value;
        return (byte)((fixedPoint >> 14) & 3);
    }
}
