// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Avalonia.Media;
using Avalonia.Media.Fonts;
using HarfBuzzSharp;

namespace Stride.AvaloniaUI.Rendering.Text;

/// <summary>
/// The ink extents of the glyphs of a typeface, which HarfBuzz reads from the font's outlines (TrueType or CFF), without hinting.
/// </summary>
internal sealed class GlyphInkExtents
{
    private static readonly ConditionalWeakTable<GlyphTypeface, GlyphInkExtents> Cache = new();

    private readonly IPlatformTypeface typeface;
    private readonly Font font;

    private GlyphInkExtents(GlyphTypeface glyphTypeface)
    {
        typeface = glyphTypeface.PlatformTypeface;
        // The font keeps the face
        font = new Font(new Face(GetTable) { UnitsPerEm = glyphTypeface.Metrics.DesignEmHeight });
        font.SetFunctionsOpenType();
    }

    /// <summary>
    /// Gets the ink extents of the glyphs of a typeface.
    /// </summary>
    public static GlyphInkExtents Get(GlyphTypeface glyphTypeface) => Cache.GetValue(glyphTypeface, static typeface => new GlyphInkExtents(typeface));

    /// <summary>
    /// Gets the ink extents of a glyph in design units, y up: <c>false</c> when the font doesn't say.
    /// </summary>
    public bool TryGet(ushort glyph, out GlyphExtents extents) => font.TryGetGlyphExtents(glyph, out extents);

    private unsafe Blob? GetTable(Face _, Tag tag)
    {
        if (!typeface.TryGetTable((uint)tag, out var table))
            return null;

        if (MemoryMarshal.TryGetArray(table, out var segment) && segment.Array != null)
        {
            var handle = GCHandle.Alloc(segment.Array, GCHandleType.Pinned);
            return new Blob(handle.AddrOfPinnedObject() + segment.Offset, segment.Count, MemoryMode.ReadOnly, () => handle.Free());
        }

        var copy = Marshal.AllocHGlobal(table.Length);
        table.Span.CopyTo(new Span<byte>((void*)copy, table.Length));
        return new Blob(copy, table.Length, MemoryMode.ReadOnly, () => Marshal.FreeHGlobal(copy));
    }
}
