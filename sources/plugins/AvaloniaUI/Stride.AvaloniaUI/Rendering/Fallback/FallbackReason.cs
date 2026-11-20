// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

namespace Stride.AvaloniaUI.Rendering.Fallback;

/// <summary>
/// Why the native canvas handed a call or a group of calls to the fallback; the comment of each member names the step of
/// the native renderer that draws it natively.
/// </summary>
internal enum FallbackReason
{
    /// <summary>A brush or a pen's brush that isn't a solid color (step 4; scene brushes, step 6).</summary>
    Brush,

    /// <summary>Box shadows (step 4).</summary>
    BoxShadow,

    /// <summary>Bitmaps (step 4).</summary>
    Bitmap,

    /// <summary>A geometry other than a rectangle, an ellipse or a line, such as a border of uneven thickness (step 5).</summary>
    Path,

    /// <summary>Dashes, or a join other than a miter on a rectangle's sharp corners (step 5).</summary>
    Stroke,

    /// <summary>A clip under another clip drawn by the shader (step 6).</summary>
    ClipDepth,

    /// <summary>An opacity group (step 6).</summary>
    OpacityGroup,

    /// <summary>An opacity mask (step 6).</summary>
    OpacityMask,

    /// <summary>A geometry clip (step 6).</summary>
    GeometryClip,

    /// <summary>A layer (step 6).</summary>
    Layer,

    /// <summary>An effect (step 6).</summary>
    Effect,

    /// <summary>A transform with perspective (step 6).</summary>
    Perspective,

    /// <summary>A feature of the fallback, such as a lease of Skia's canvas: always drawn by the fallback.</summary>
    CustomDrawing,

    /// <summary>A rectangle of an acrylic material.</summary>
    Acrylic,

    /// <summary>A typeface with colour glyphs (COLR, CBDT, sbix or SVG tables): always drawn by the fallback for now.</summary>
    ColorFont,

    /// <summary>
    /// A font the core can't draw from: its data can't be read, no face of its file is the typeface's, it is a variable
    /// font, or FreeType fails on one of its glyphs (step 8).
    /// </summary>
    FontData,

    /// <summary>
    /// A typeface that simulates bold: Skia strokes the outline of each glyph and fills it with its own rasterizer, which
    /// FreeType's doesn't match (step 8).
    /// </summary>
    TextSimulation,

    /// <summary>A glyph run under a transform other than a translation and a uniform positive scale (step 7).</summary>
    TextTransform,

    /// <summary>Glyphs larger than 256 pixels, which Skia draws as paths (step 7).</summary>
    LargeText,

    /// <summary>A glyph run with more glyphs in a frame than the glyph atlas holds, or a glyph larger than its pages.</summary>
    GlyphAtlasFull,
}
