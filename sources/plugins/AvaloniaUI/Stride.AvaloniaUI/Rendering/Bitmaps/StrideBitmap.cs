// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Image = Stride.Graphics.Image;
using ImageFileType = Stride.Graphics.ImageFileType;
using StridePixelFormat = Stride.Graphics.PixelFormat;

namespace Stride.AvaloniaUI.Rendering.Bitmaps;

/// <summary>
/// A bitmap of the core: pixels in memory the bitmap owns, 4 bytes a pixel, in Bgra8888 or Rgba8888.
/// </summary>
/// <remarks>
/// The memory is pinned and managed, so a bitmap nobody disposes doesn't leak it.
/// </remarks>
internal class StrideBitmap : IWriteableBitmapImpl
{
    private readonly byte[] pixels;
    private readonly Lock sync = new();

    public StrideBitmap(PixelSize size, Vector dpi, PixelFormat format, AlphaFormat alphaFormat)
    {
        if (format != PixelFormat.Bgra8888 && format != PixelFormat.Rgba8888)
            throw new NotSupportedException($"The pixel format {format} is not supported: use {PixelFormat.Bgra8888} or {PixelFormat.Rgba8888}.");
        ArgumentOutOfRangeException.ThrowIfNegative(size.Width, nameof(size));
        ArgumentOutOfRangeException.ThrowIfNegative(size.Height, nameof(size));

        PixelSize = size;
        Dpi = dpi;
        Format = format;
        AlphaFormat = alphaFormat;
        RowBytes = size.Width * 4;
        pixels = GC.AllocateArray<byte>(RowBytes * size.Height, pinned: true);
        Address = pixels.Length == 0 ? IntPtr.Zero : Marshal.UnsafeAddrOfPinnedArrayElement(pixels, 0);
    }

    public PixelSize PixelSize { get; }

    public Vector Dpi { get; }

    /// <summary>
    /// Gets the address of the first pixel, or <see cref="IntPtr.Zero"/> when the bitmap has no pixels.
    /// </summary>
    public IntPtr Address { get; }

    /// <summary>
    /// Gets the number of bytes from a row of pixels to the next.
    /// </summary>
    public int RowBytes { get; }

    /// <summary>
    /// Gets the order of the channels of a pixel.
    /// </summary>
    public PixelFormat Format { get; }

    /// <summary>
    /// Gets whether the color channels are multiplied by the alpha.
    /// </summary>
    public AlphaFormat AlphaFormat { get; }

    /// <summary>
    /// Gets a number that grows each time the pixels may have changed.
    /// </summary>
    public int Version { get; private set; } = 1;

    /// <summary>
    /// Gets or sets what the fallback made from this bitmap, disposed with it when it is <see cref="IDisposable"/>.
    /// </summary>
    internal object? FallbackObject { get; set; }

    PixelFormat? IReadableBitmapImpl.Format => Format;

    AlphaFormat? IReadableBitmapImpl.AlphaFormat => AlphaFormat;

    /// <summary>
    /// Makes a bitmap with a copy of pixels in memory.
    /// </summary>
    public static StrideBitmap Copy(PixelFormat format, AlphaFormat alphaFormat, IntPtr data, PixelSize size, Vector dpi, int stride)
    {
        var bitmap = new StrideBitmap(size, dpi, format, alphaFormat);
        for (var y = 0; y < size.Height; y++)
            Marshal.Copy(data + y * stride, bitmap.pixels, y * bitmap.RowBytes, bitmap.RowBytes);
        return bitmap;
    }

    /// <summary>
    /// Gets the pixels to change them: the version grows when the lock is released.
    /// </summary>
    public ILockedFramebuffer Lock()
    {
        sync.Enter();
        return new LockedFramebuffer(Address, PixelSize, RowBytes, Dpi, Format, AlphaFormat, () =>
        {
            Version++;
            sync.Exit();
        });
    }

    /// <summary>
    /// Keeps the pixels from changing until the scope is disposed.
    /// </summary>
    internal Lock.Scope EnterReadScope() => sync.EnterScope();

    /// <summary>
    /// Saves the bitmap in PNG or JPEG, with unpremultiplied colors as Skia saves them.
    /// </summary>
    public void Save(Stream stream, BitmapEncoderOptions? options)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var fileType = options switch
        {
            null or PngBitmapEncoderOptions => ImageFileType.Png,
            JpegBitmapEncoderOptions => ImageFileType.Jpg,
            _ => throw new ArgumentOutOfRangeException(nameof(options), options, "Unknown encoder options type"),
        };
        // Skia has no 0×0 bitmaps: it refuses to make them with the same exception
        if (Address == IntPtr.Zero)
            throw new ArgumentException("A bitmap of 0×0 pixels can't be saved.", nameof(stream));

        using var image = Image.New2D(PixelSize.Width, PixelSize.Height, 1, StridePixelFormat.R8G8B8A8_UNorm);
        var destination = image.PixelBuffer[0];
        var rgba = new byte[RowBytes];
        using (sync.EnterScope())
        {
            for (var y = 0; y < PixelSize.Height; y++)
            {
                var row = pixels.AsSpan(y * RowBytes, RowBytes);
                for (var x = 0; x < row.Length; x += 4)
                {
                    var (r, g, b, a) = Format == PixelFormat.Bgra8888 ? (row[x + 2], row[x + 1], row[x], row[x + 3]) : (row[x], row[x + 1], row[x + 2], row[x + 3]);
                    if (AlphaFormat == AlphaFormat.Premul)
                        (r, g, b) = (Unpremultiply(r, a), Unpremultiply(g, a), Unpremultiply(b, a));
                    (rgba[x], rgba[x + 1], rgba[x + 2], rgba[x + 3]) = (r, g, b, a);
                }
                Marshal.Copy(rgba, 0, destination.DataPointer + y * destination.RowStride, RowBytes);
            }
        }
        image.Save(stream, fileType);
    }

    public void Dispose()
    {
        (FallbackObject as IDisposable)?.Dispose();
        FallbackObject = null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte Unpremultiply(byte channel, byte alpha)
        => alpha == 0 ? (byte)0 : (byte)Math.Min(255, (channel * 255 + alpha / 2) / alpha);
}
