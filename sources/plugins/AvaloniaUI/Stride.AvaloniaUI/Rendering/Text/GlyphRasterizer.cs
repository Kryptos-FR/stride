// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Buffers;
using System.Runtime.InteropServices;
using Avalonia.Media;
using Stride.Graphics.Font;

namespace Stride.AvaloniaUI.Rendering.Text;

/// <summary>
/// How the edges of a glyph are drawn.
/// </summary>
internal enum GlyphRendering
{
    /// <summary>Grayscale coverage, with Skia's mask gamma.</summary>
    Antialias,

    /// <summary>Coverage of 0 or 255, without gamma.</summary>
    Alias,
}

/// <summary>
/// A glyph of a face at a pixel size, as Skia's FreeType scaler makes it.
/// </summary>
/// <param name="Size26Dot6">The size of the em in pixels, in 26.6 fixed point (<see cref="GlyphRasterizer.ToSize26Dot6"/>).</param>
/// <param name="SubpixelX">The quarter of a pixel the glyph's origin is past its whole pixel, 0 to 3; 0 when aliased.</param>
/// <param name="Luminance">The luminance table of the text color, 0 to 7 (<see cref="TextGamma.GetLuminance"/>); 0 when aliased.</param>
internal readonly record struct GlyphKey(NativeFont Font, ushort Glyph, int Size26Dot6, GlyphRendering Rendering,
    TextHintingMode Hinting, byte SubpixelX, byte Luminance);

/// <summary>
/// The bitmap of a glyph: its top left pixel from the glyph's origin, y down, and its size; no ink when the width or the
/// height is 0.
/// </summary>
internal readonly record struct GlyphImage(int Left, int Top, int Width, int Height)
{
    public bool HasInk => Width > 0 && Height > 0;
}

/// <summary>
/// Rasterizes glyph masks with FreeType the way Skia's FreeType scaler does on the CPU, for a translation and a uniform scale.
/// </summary>
internal static unsafe class GlyphRasterizer
{
    private const uint OutlineFormat = 0x6F75746C; // 'outl'

    // Skia's skew of a simulated oblique typeface, in 16.16: x += 0.3 y in FreeType's y-up space
    private static readonly CLong ObliqueSkew = new((int)(0.3f * 65536));

    /// <summary>
    /// Gets the 26.6 size FreeType is given for a pixel size, as Skia converts it: truncated.
    /// </summary>
    public static int ToSize26Dot6(float pixelSize) => (int)(pixelSize * 64);

    /// <summary>
    /// Writes the coverage of a glyph, Skia's mask gamma applied, in rows of <see cref="GlyphImage.Width"/> bytes.
    /// </summary>
    /// <returns><c>false</c> when FreeType fails on the glyph, or the glyph isn't an outline.</returns>
    public static bool TryRasterize(nint library, in GlyphKey key, ArrayBufferWriter<byte> coverage, out GlyphImage image)
    {
        image = default;
        var face = key.Font.Face;
        if (face == null || FreeTypeNative.FT_Set_Char_Size(face, new CLong(key.Size26Dot6), new CLong(key.Size26Dot6), 72, 72) != 0)
            return false;

        var oblique = (key.Font.Simulations & FontSimulations.Oblique) != 0;
        if (oblique)
        {
            var skew = new FT_Matrix { xx = new CLong(0x10000), xy = ObliqueSkew, yx = new CLong(0), yy = new CLong(0x10000) };
            FreeTypeNative.FT_Set_Transform(face, &skew, null);
        }
        else
        {
            FreeTypeNative.FT_Set_Transform(face, null, null);
        }

        // Skia doesn't hint glyphs that aren't axis-aligned, as skewed ones
        var hinting = oblique ? TextHintingMode.None : key.Hinting;
        if (FreeTypeNative.FT_Load_Glyph(face, key.Glyph, GetLoadFlags(key.Rendering, hinting)) != 0)
            return false;
        var slot = face->glyph;
        if (slot->format != OutlineFormat)
            return false;
        var outline = &slot->outline;

        // The subpixel position moves the outline, then its box is rounded out to whole pixels
        var subpixel = key.Rendering == GlyphRendering.Antialias ? key.SubpixelX * 16 : 0;
        FreeTypeNative.FT_Outline_Translate(outline, new CLong(subpixel), new CLong(0));
        FT_BBox box;
        FreeTypeNative.FT_Outline_Get_CBox(outline, &box);
        var xMin = (long)box.xMin.Value & ~63L;
        var yMin = (long)box.yMin.Value & ~63L;
        var xMax = ((long)box.xMax.Value + 63) & ~63L;
        var yMax = ((long)box.yMax.Value + 63) & ~63L;
        var width = (int)((xMax - xMin) >> 6);
        var height = (int)((yMax - yMin) >> 6);
        if (width <= 0 || height <= 0)
            return true;
        image = new GlyphImage((int)(xMin >> 6), -(int)(yMax >> 6), width, height);
        FreeTypeNative.FT_Outline_Translate(outline, new CLong((nint)(-xMin)), new CLong((nint)(-yMin)));

        var pixels = coverage.GetSpan(width * height)[..(width * height)];
        if (key.Rendering == GlyphRendering.Alias)
        {
            if (!RenderMono(library, outline, width, height, pixels))
                return false;
        }
        else
        {
            pixels.Clear();
            fixed (byte* buffer = pixels)
            {
                var bitmap = new FT_Bitmap { rows = (uint)height, width = (uint)width, pitch = width, buffer = buffer, num_grays = 256, pixel_mode = (byte)FreeTypePixelMode.Gray };
                if (FreeTypeNative.FT_Outline_Get_Bitmap(library, outline, &bitmap) != 0)
                    return false;
            }
            var table = TextGamma.GetTable(key.Luminance);
            foreach (ref var value in pixels)
                value = table[value];
        }
        coverage.Advance(width * height);
        return true;
    }

    private static int GetLoadFlags(GlyphRendering rendering, TextHintingMode hinting)
    {
        var flags = FreeTypeLoadFlags.NoBitmap | FreeTypeLoadFlags.IgnoreGlobalAdvanceWidth;
        if (rendering == GlyphRendering.Alias)
            return (int)flags | (int)FreeTypeLoadTarget.Mono | (hinting == TextHintingMode.None ? (int)FreeTypeLoadFlags.NoHinting : 0);
        return hinting switch
        {
            TextHintingMode.None => (int)(flags | FreeTypeLoadFlags.NoHinting),
            TextHintingMode.Light => (int)(flags | FreeTypeLoadFlags.ForceAutohint) | (int)FreeTypeLoadTarget.Light,
            _ => (int)flags | (int)FreeTypeLoadTarget.Normal,
        };
    }

    // A 1-bit bitmap, its bits expanded to 0 and 255
    private static bool RenderMono(nint library, FT_Outline* outline, int width, int height, Span<byte> pixels)
    {
        var pitch = (width + 7) >> 3;
        var bits = ArrayPool<byte>.Shared.Rent(pitch * height);
        try
        {
            Array.Clear(bits, 0, pitch * height);
            fixed (byte* buffer = bits)
            {
                var bitmap = new FT_Bitmap { rows = (uint)height, width = (uint)width, pitch = pitch, buffer = buffer, num_grays = 2, pixel_mode = (byte)FreeTypePixelMode.Mono };
                if (FreeTypeNative.FT_Outline_Get_Bitmap(library, outline, &bitmap) != 0)
                    return false;
            }
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                    pixels[y * width + x] = (bits[y * pitch + (x >> 3)] & (0x80 >> (x & 7))) != 0 ? (byte)255 : (byte)0;
            }
            return true;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(bits);
        }
    }
}
