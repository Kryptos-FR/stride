// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Media;
using Avalonia.Platform;
using Stride.AvaloniaUI.Rendering.Geometry;
using Stride.AvaloniaUI.Rendering.Text;

namespace Stride.AvaloniaUI.Rendering.Fallback;

/// <summary>
/// What the core can't do itself yet, done by another renderer (the Skia package) from the core's own objects.
/// </summary>
internal interface IRenderFallback
{
    /// <summary>
    /// Gets the fallback's equivalent of a core geometry, kept in <see cref="StrideGeometry.FallbackObject"/>.
    /// </summary>
    IGeometryImpl GetGeometry(StrideGeometry geometry);

    /// <summary>
    /// Gets the font manager of the fallback, which makes the typefaces of the glyph runs.
    /// </summary>
    IFontManagerImpl FontManager { get; }

    /// <summary>
    /// Gets the fallback's equivalent of a core glyph run, kept in <see cref="StrideGlyphRun.FallbackObject"/>.
    /// </summary>
    IGlyphRunImpl GetGlyphRun(StrideGlyphRun glyphRun);

    /// <summary>
    /// Builds the outlines of the glyphs of a glyph run.
    /// </summary>
    IGeometryImpl BuildGlyphRunGeometry(GlyphRun glyphRun);
}
