// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.IO;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using AlphaLoadMode = Stride.Graphics.AlphaLoadMode;
using Image = Stride.Graphics.Image;
using StridePixelFormat = Stride.Graphics.PixelFormat;

namespace Stride.AvaloniaUI.Rendering.Bitmaps;

/// <summary>
/// Decodes the bitmaps of the core with Stride's <see cref="Image"/>, and resizes them.
/// </summary>
internal static class StrideBitmapCodec
{
    private static readonly Vector DefaultDpi = new(96, 96);

    /// <summary>
    /// Decodes an image file into a premultiplied bitmap.
    /// </summary>
    /// <exception cref="NotSupportedException">The image decodes to a pixel format other than 8-bit BGRA or RGBA.</exception>
    public static StrideBitmap Load(Stream stream)
    {
        using var image = Image.Load(stream, loadAsSRGB: false, AlphaLoadMode.EnsurePremultiplied);
        var format = image.Description.Format switch
        {
            StridePixelFormat.B8G8R8A8_UNorm => PixelFormat.Bgra8888,
            StridePixelFormat.R8G8B8A8_UNorm => PixelFormat.Rgba8888,
            var other => throw new NotSupportedException($"Images decoded to the pixel format {other} are not supported."),
        };
        var pixels = image.PixelBuffer[0];
        return StrideBitmap.Copy(format, AlphaFormat.Premul, pixels.DataPointer, new PixelSize(pixels.Width, pixels.Height), DefaultDpi, pixels.RowStride);
    }

    /// <summary>
    /// Makes a copy of a bitmap at another size.
    /// </summary>
    /// <remarks>
    /// <see cref="BitmapInterpolationMode.None"/> takes the nearest pixel; the other modes average the pixels a destination pixel
    /// covers when shrinking, and interpolate between the nearest four when growing. Colors are filtered premultiplied.
    /// </remarks>
    public static unsafe StrideBitmap Resize(StrideBitmap source, PixelSize size, BitmapInterpolationMode mode)
    {
        var result = new StrideBitmap(size, source.Dpi, source.Format, source.AlphaFormat);
        if (size.Width == 0 || size.Height == 0 || source.PixelSize.Width == 0 || source.PixelSize.Height == 0)
            return result;

        var columns = Weights(source.PixelSize.Width, size.Width, mode);
        var rows = Weights(source.PixelSize.Height, size.Height, mode);
        var premultiplied = source.AlphaFormat == AlphaFormat.Premul;

        // Rows of the source filtered horizontally, as premultiplied floats
        var horizontal = new float[source.PixelSize.Height * size.Width * 4];
        using (source.EnterReadScope())
        {
            for (var y = 0; y < source.PixelSize.Height; y++)
            {
                var sourceRow = (byte*)source.Address + y * source.RowBytes;
                for (var x = 0; x < size.Width; x++)
                {
                    float c0 = 0, c1 = 0, c2 = 0, alpha = 0;
                    foreach (var (index, weight) in columns[x])
                    {
                        var pixel = sourceRow + index * 4;
                        var a = pixel[3];
                        var scale = premultiplied ? weight : weight * a / 255f;
                        c0 += pixel[0] * scale;
                        c1 += pixel[1] * scale;
                        c2 += pixel[2] * scale;
                        alpha += a * weight;
                    }
                    var offset = (y * size.Width + x) * 4;
                    (horizontal[offset], horizontal[offset + 1], horizontal[offset + 2], horizontal[offset + 3]) = (c0, c1, c2, alpha);
                }
            }
        }

        for (var y = 0; y < size.Height; y++)
        {
            var destinationRow = (byte*)result.Address + y * result.RowBytes;
            for (var x = 0; x < size.Width; x++)
            {
                float c0 = 0, c1 = 0, c2 = 0, alpha = 0;
                foreach (var (index, weight) in rows[y])
                {
                    var offset = (index * size.Width + x) * 4;
                    c0 += horizontal[offset] * weight;
                    c1 += horizontal[offset + 1] * weight;
                    c2 += horizontal[offset + 2] * weight;
                    alpha += horizontal[offset + 3] * weight;
                }
                var scale = premultiplied || alpha <= 0 ? 1 : 255 / alpha;
                var pixel = destinationRow + x * 4;
                pixel[0] = ToByte(c0 * scale);
                pixel[1] = ToByte(c1 * scale);
                pixel[2] = ToByte(c2 * scale);
                pixel[3] = ToByte(alpha);
            }
        }
        return result;
    }

    // For each destination pixel along an axis, the source pixels it takes and their weights, which add up to 1
    private static (int Index, float Weight)[][] Weights(int sourceLength, int destinationLength, BitmapInterpolationMode mode)
    {
        var weights = new (int, float)[destinationLength][];
        var scale = (double)sourceLength / destinationLength;
        for (var i = 0; i < destinationLength; i++)
        {
            if (mode == BitmapInterpolationMode.None)
            {
                weights[i] = [(Math.Min(sourceLength - 1, (int)((i + 0.5) * scale)), 1)];
            }
            else if (scale > 1)
            {
                // Box filter: each source pixel weighs the length of it the destination pixel covers
                var start = i * scale;
                var end = (i + 1) * scale;
                var first = (int)start;
                var last = Math.Min(sourceLength - 1, (int)Math.Ceiling(end) - 1);
                var taps = new (int, float)[last - first + 1];
                for (var index = first; index <= last; index++)
                    taps[index - first] = (index, (float)((Math.Min(end, index + 1) - Math.Max(start, index)) / scale));
                weights[i] = taps;
            }
            else
            {
                // Bilinear: the two source pixels around the center of the destination pixel
                var center = Math.Max(0, (i + 0.5) * scale - 0.5);
                var index = Math.Min(sourceLength - 1, (int)center);
                var next = Math.Min(sourceLength - 1, index + 1);
                var t = (float)(center - index);
                weights[i] = [(index, 1 - t), (next, t)];
            }
        }
        return weights;
    }

    private static byte ToByte(float value) => (byte)Math.Clamp(MathF.Round(value), 0, 255);
}
