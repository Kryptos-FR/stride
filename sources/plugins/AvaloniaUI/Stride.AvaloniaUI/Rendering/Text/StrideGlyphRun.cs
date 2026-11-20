// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Platform;
using Stride.AvaloniaUI.Rendering.Fallback;

namespace Stride.AvaloniaUI.Rendering.Text;

/// <summary>
/// A glyph run of the core: a typeface, glyph ids and advances, and the ink bounds of the glyphs.
/// </summary>
internal sealed class StrideGlyphRun : IGlyphRunImpl
{
    private readonly FallbackChannel channel;

    public StrideGlyphRun(FallbackChannel channel, GlyphTypeface glyphTypeface, double fontRenderingEmSize, IReadOnlyList<GlyphInfo> glyphInfos, Point baselineOrigin)
    {
        ArgumentNullException.ThrowIfNull(glyphTypeface);
        ArgumentNullException.ThrowIfNull(glyphInfos);

        this.channel = channel;
        GlyphTypeface = glyphTypeface;
        FontRenderingEmSize = fontRenderingEmSize;
        GlyphInfos = glyphInfos.ToArray();
        BaselineOrigin = baselineOrigin;
        Bounds = GlyphInfos.Count == 0 ? default : MeasureInk(glyphTypeface, fontRenderingEmSize, GlyphInfos).Translate(baselineOrigin);
    }

    public GlyphTypeface GlyphTypeface { get; }

    public double FontRenderingEmSize { get; }

    /// <summary>
    /// Gets the glyphs, a copy of those the run was made with.
    /// </summary>
    public IReadOnlyList<GlyphInfo> GlyphInfos { get; }

    public Point BaselineOrigin { get; }

    public Rect Bounds { get; }

    /// <summary>
    /// Gets what the fallback made from this glyph run, disposed with it when it is <see cref="IDisposable"/>.
    /// </summary>
    internal object? FallbackObject { get; set; }

    public IReadOnlyList<float> GetIntersections(float lowerLimit, float upperLimit)
    {
        channel.Count(DrawCallKind.GlyphRunIntersections);
        return channel.Fallback.GetGlyphRun(this).GetIntersections(lowerLimit, upperLimit);
    }

    public void Dispose()
    {
        (FallbackObject as IDisposable)?.Dispose();
        FallbackObject = null;
    }

    // The union of the ink boxes of the glyphs along their advances, relative to the baseline origin, as Skia measures them:
    // each box rounded out to whole pixels, glyph offsets left out, and glyphs without ink taking no room
    private static Rect MeasureInk(GlyphTypeface glyphTypeface, double emSize, IReadOnlyList<GlyphInfo> glyphInfos)
    {
        var extents = GlyphInkExtents.Get(glyphTypeface);
        var scale = emSize / glyphTypeface.Metrics.DesignEmHeight;
        var simulations = glyphTypeface.FontSimulations;
        var advance = 0.0;
        var bounds = default(Rect);
        foreach (var glyph in glyphInfos)
        {
            var box = default(Rect);
            if (extents.TryGet(glyph.GlyphIndex, out var ink) && ink.Width != 0 && ink.Height != 0)
            {
                // Design units are y up, the run is y down
                box = new Rect(ink.XBearing * scale, -ink.YBearing * scale, ink.Width * scale, -ink.Height * scale);
                box = Simulate(box, emSize, simulations);
                box = new Rect(new Point(Math.Floor(box.Left), Math.Floor(box.Top)), new Point(Math.Ceiling(box.Right), Math.Ceiling(box.Bottom)));
            }
            bounds = bounds.Union(box.Translate(new Vector(advance, 0)));
            advance += glyph.GlyphAdvance;
        }
        return bounds;
    }

    // Skia's simulations of the faces a font family lacks: oblique skews x by -0.3 y, bold outlines the glyphs
    private static Rect Simulate(Rect box, double emSize, FontSimulations simulations)
    {
        if ((simulations & FontSimulations.Oblique) != 0)
            box = new Rect(new Point(box.Left - 0.3 * box.Bottom, box.Top), new Point(box.Right - 0.3 * box.Top, box.Bottom));
        if ((simulations & FontSimulations.Bold) != 0)
        {
            // Skia's outline width goes from 1/24 of the size at 9 to 1/32 at 36
            var ratio = 1 / 24.0 + (1 / 32.0 - 1 / 24.0) * Math.Clamp((emSize - 9) / (36 - 9), 0, 1);
            box = box.Inflate(emSize * ratio / 2);
        }
        return box;
    }
}
