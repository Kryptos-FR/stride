// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using Avalonia.Media;

namespace Stride.AvaloniaUI.Rendering.Text;

/// <summary>
/// Skia's mask gamma of grayscale glyphs (<c>SkTMaskGamma</c> with a contrast of 0.5, sRGB paint and device gamma, and 3
/// bits of luminance): a table per luminance of the text color that maps FreeType's coverage to the coverage Skia blends.
/// </summary>
/// <remarks>
/// The table boosts the coverage of dark text and lightens that of light text, so that blended in sRGB both look as
/// heavy as they would blended in linear light.
/// </remarks>
internal static class TextGamma
{
    /// <summary>
    /// The number of luminance tables.
    /// </summary>
    public const int TableCount = 8;

    // 0.5, kept in 8 bits as SkScalerContextRec keeps it
    private const float Contrast = 128 / 255f;

    private static readonly byte[] Tables = CreateTables();

    /// <summary>
    /// Gets the luminance of a text color, its alpha left out: 0 for black to 7 for white.
    /// </summary>
    /// <remarks>
    /// The weights are Skia's <c>SkComputeLuminance</c>, on the sRGB channels.
    /// </remarks>
    public static byte GetLuminance(Color color) => (byte)(((color.R * 54 + color.G * 183 + color.B * 19) >> 8) >> 5);

    /// <summary>
    /// Gets the table of a luminance: 256 coverages, from FreeType's to Skia's.
    /// </summary>
    public static ReadOnlySpan<byte> GetTable(byte luminance) => Tables.AsSpan(luminance * 256, 256);

    private static byte[] CreateTables()
    {
        var tables = new byte[TableCount * 256];
        for (var i = 0; i < TableCount; i++)
        {
            // 3 bits scaled to 255, as sk_t_scale255 does
            var lum = (i << 5) | (i << 2) | (i >> 1);
            float src = lum / 255f, dst = 1 - src, linSrc = ToLinear(src), linDst = ToLinear(dst);
            var contrast = Contrast * linDst;
            for (var a = 0; a < 256; a++)
            {
                var raw = a / 255f;
                var srca = raw + (1 - raw) * contrast * raw;
                var result = MathF.Abs(src - dst) < 1 / 256f ? srca : (ToSrgb(linSrc * srca + (1 - srca) * linDst) - dst) / (src - dst);
                tables[i * 256 + a] = (byte)Math.Clamp((int)MathF.Floor(255 * result + 0.5f), 0, 255);
            }
        }
        return tables;
    }

    // sRGB both ways, as Skia's SkSRGBColorSpaceLuminance
    private static float ToLinear(float v) => v <= 0.04045f ? v / 12.92f : MathF.Pow((v + 0.055f) / 1.055f, 2.4f);

    private static float ToSrgb(float l) => l <= 0.0031308f ? l * 12.92f : 1.055f * MathF.Pow(l, 1 / 2.4f) - 0.055f;
}
