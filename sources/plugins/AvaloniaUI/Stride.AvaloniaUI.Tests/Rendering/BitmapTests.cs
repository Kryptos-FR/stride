// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Stride.AvaloniaUI.Rendering.Bitmaps;
using Stride.AvaloniaUI.Skia.Fallback;
using Xunit;
using Image = Stride.Graphics.Image;
using ImageFileType = Stride.Graphics.ImageFileType;
using PixelFormat = Avalonia.Platform.PixelFormat;
using StridePixelFormat = Stride.Graphics.PixelFormat;

namespace Stride.AvaloniaUI.Tests.Rendering;

/// <summary>
/// Tests the bitmaps of the core, decoded by Stride, against Skia's.
/// </summary>
public class BitmapTests
{
    private static readonly Vector Dpi = new(96, 96);

    [Fact]
    public void LoadedPngHasSkiasPixels() => TestAvalonia.Run(() =>
    {
        var png = MakePng();

        using var core = StrideBitmapCodec.Load(new MemoryStream(png));
        using var skia = (IReadableBitmapImpl)SkiaOracle.RenderInterface.LoadBitmap(new MemoryStream(png));

        Assert.Equal(skia.PixelSize, core.PixelSize);
        using var skiaPixels = skia.Lock();
        using var corePixels = core.Lock();
        for (var y = 0; y < core.PixelSize.Height; y++)
        {
            for (var x = 0; x < core.PixelSize.Width; x++)
                AssertClose(ReadPremultiplied(skiaPixels, x, y), ReadPremultiplied(corePixels, x, y), 1, x, y);
        }
    });

    [Fact]
    public void SavedPngLoadsBackTheSame() => TestAvalonia.Run(() =>
    {
        using var original = StrideBitmapCodec.Load(new MemoryStream(MakePng()));
        var saved = new MemoryStream();
        original.Save(saved, PngBitmapEncoderOptions.Default);

        saved.Position = 0;
        using var loaded = StrideBitmapCodec.Load(saved);

        Assert.Equal(original.PixelSize, loaded.PixelSize);
        using var originalPixels = original.Lock();
        using var loadedPixels = loaded.Lock();
        for (var y = 0; y < original.PixelSize.Height; y++)
        {
            for (var x = 0; x < original.PixelSize.Width; x++)
                AssertClose(ReadPremultiplied(originalPixels, x, y), ReadPremultiplied(loadedPixels, x, y), 1, x, y);
        }
    });

    [Fact]
    public void ReleasingALockChangesTheVersion()
    {
        using var bitmap = new StrideBitmap(new PixelSize(2, 2), Dpi, PixelFormat.Bgra8888, AlphaFormat.Premul);
        var version = bitmap.Version;

        using (bitmap.Lock())
            Assert.Equal(version, bitmap.Version);

        Assert.NotEqual(version, bitmap.Version);
    }

    [Fact]
    public void ResizeKeepsTheSizeAndAverages()
    {
        using var checker = MakeBitmap(2, 2, [0xFFFFFFFF, 0xFF000000, 0xFF000000, 0xFFFFFFFF]);
        using var gray = StrideBitmapCodec.Resize(checker, new PixelSize(1, 1), BitmapInterpolationMode.HighQuality);
        Assert.Equal(new PixelSize(1, 1), gray.PixelSize);
        using (var pixels = gray.Lock())
        {
            var (b, g, r, a) = ReadPremultiplied(pixels, 0, 0);
            Assert.InRange(b, 127, 128);
            Assert.InRange(g, 127, 128);
            Assert.InRange(r, 127, 128);
            Assert.Equal(255, a);
        }

        using var uniform = MakeBitmap(3, 3, Enumerable.Repeat(0x80402010u, 9).ToArray());
        using var grown = StrideBitmapCodec.Resize(uniform, new PixelSize(7, 5), BitmapInterpolationMode.HighQuality);
        Assert.Equal(new PixelSize(7, 5), grown.PixelSize);
        using (var pixels = grown.Lock())
        {
            for (var y = 0; y < 5; y++)
            {
                for (var x = 0; x < 7; x++)
                    AssertClose((0x10, 0x20, 0x40, 0x80), ReadPremultiplied(pixels, x, y), 0, x, y);
            }
        }

        using var nearest = StrideBitmapCodec.Resize(checker, new PixelSize(1, 1), BitmapInterpolationMode.None);
        using (var pixels = nearest.Lock())
        {
            var pixel = ReadPremultiplied(pixels, 0, 0);
            Assert.True(pixel == (255, 255, 255, 255) || pixel == (0, 0, 0, 255), $"Nearest gave {pixel}, which is no pixel of the source.");
        }
    }

    [Theory]
    [InlineData("Bgra8888", AlphaFormat.Premul)]
    [InlineData("Bgra8888", AlphaFormat.Unpremul)]
    [InlineData("Rgba8888", AlphaFormat.Premul)]
    [InlineData("Rgba8888", AlphaFormat.Unpremul)]
    public void SkiaGetsTheChannelsAndAlphaAsStored(string format, AlphaFormat alpha) => TestAvalonia.Run(() =>
    {
        var pixelFormat = format == "Bgra8888" ? PixelFormat.Bgra8888 : PixelFormat.Rgba8888;
        using var bitmap = new StrideBitmap(new PixelSize(1, 1), Dpi, pixelFormat, alpha);
        var red = alpha == AlphaFormat.Premul ? (byte)128 : (byte)255;
        using (var pixels = bitmap.Lock())
        {
            var bytes = pixelFormat == PixelFormat.Bgra8888 ? new byte[] { 0, 0, red, 128 } : [red, 0, 0, 128];
            Marshal.Copy(bytes, 0, pixels.Address, 4);
        }

        var skia = (IReadableBitmapImpl?)CreateFallback().GetBitmap(bitmap);

        Assert.NotNull(skia);
        using var skiaPixels = skia.Lock();
        var (b, g, r, a) = ReadPremultiplied(skiaPixels, 0, 0);
        Assert.InRange(r, 127, 128);
        Assert.Equal(0, g);
        Assert.Equal(0, b);
        Assert.Equal(128, a);
    });

    [Fact]
    public void EmptyBitmapHasNoSkiaBitmap() => TestAvalonia.Run(() =>
    {
        using var bitmap = new StrideBitmap(new PixelSize(0, 0), Dpi, PixelFormat.Bgra8888, AlphaFormat.Premul);

        Assert.Equal(IntPtr.Zero, bitmap.Address);
        Assert.Null(CreateFallback().GetBitmap(bitmap));

        // Skia refuses to make a 0×0 bitmap; the core makes one, and refuses to save it the same way
        var skiaSave = SaveOutcome(() => SkiaOracle.RenderInterface.CreateWriteableBitmap(new PixelSize(0, 0), Dpi, PixelFormat.Bgra8888, AlphaFormat.Premul));
        var coreSave = SaveOutcome(() => new StrideBitmap(new PixelSize(0, 0), Dpi, PixelFormat.Bgra8888, AlphaFormat.Premul));
        Assert.Equal(skiaSave, coreSave);
    });

    [Fact]
    public void SkiaBitmapIsMadeAgainOnlyWhenTheVersionChanges() => TestAvalonia.Run(() =>
    {
        var fallback = CreateFallback();
        using var bitmap = new StrideBitmap(new PixelSize(2, 2), Dpi, PixelFormat.Bgra8888, AlphaFormat.Premul);

        var first = fallback.GetBitmap(bitmap);
        Assert.NotNull(first);
        Assert.Same(first, fallback.GetBitmap(bitmap));

        using (bitmap.Lock())
        {
        }

        var second = fallback.GetBitmap(bitmap);
        Assert.NotNull(second);
        Assert.NotSame(first, second);
        Assert.Same(second, fallback.GetBitmap(bitmap));
    });

    private static SkiaRenderFallback CreateFallback() => (SkiaRenderFallback)SkiaOracle.CreateChannel().Fallback;

    // What making and saving a bitmap gives: the type of the exception thrown, or the number of bytes written
    private static string SaveOutcome(Func<IBitmapImpl> create)
    {
        try
        {
            using var bitmap = create();
            var stream = new MemoryStream();
            bitmap.Save(stream, PngBitmapEncoderOptions.Default);
            return $"{stream.Length} bytes";
        }
        catch (Exception exception)
        {
            return exception.GetType().Name;
        }
    }

    // A PNG made with Stride: opaque red, 50% green and transparent blue in the first row, a 16-step alpha ramp of white in the second
    internal static byte[] MakePng()
    {
        using var image = Image.New2D(16, 2, 1, StridePixelFormat.R8G8B8A8_UNorm);
        var pixels = image.PixelBuffer[0];
        byte[][] colors = [[255, 0, 0, 255], [0, 255, 0, 128], [0, 0, 255, 0]];
        for (var x = 0; x < 16; x++)
        {
            pixels.SetPixel(x, 0, BitConverter.ToUInt32(colors[x % 3]));
            pixels.SetPixel(x, 1, BitConverter.ToUInt32([255, 255, 255, (byte)(x * 17)]));
        }
        var stream = new MemoryStream();
        image.Save(stream, ImageFileType.Png);
        return stream.ToArray();
    }

    // A premultiplied Bgra8888 bitmap from pixels written as 0xAARRGGBB
    private static StrideBitmap MakeBitmap(int width, int height, uint[] pixels)
    {
        var bitmap = new StrideBitmap(new PixelSize(width, height), Dpi, PixelFormat.Bgra8888, AlphaFormat.Premul);
        using var locked = bitmap.Lock();
        for (var y = 0; y < height; y++)
            Marshal.Copy(MemoryMarshal.AsBytes(pixels.AsSpan(y * width, width)).ToArray(), 0, locked.Address + y * locked.RowBytes, width * 4);
        return bitmap;
    }

    // The premultiplied blue, green, red and alpha of a pixel, whatever the layout of the memory
    private static (int B, int G, int R, int A) ReadPremultiplied(ILockedFramebuffer pixels, int x, int y)
    {
        var bytes = new byte[4];
        Marshal.Copy(pixels.Address + y * pixels.RowBytes + x * 4, bytes, 0, 4);
        var (r, g, b, a) = pixels.Format == PixelFormat.Bgra8888 ? (bytes[2], bytes[1], bytes[0], bytes[3])
            : pixels.Format == PixelFormat.Rgba8888 ? (bytes[0], bytes[1], bytes[2], bytes[3])
            : throw new NotSupportedException($"Pixel format {pixels.Format}");
        if (pixels.AlphaFormat == AlphaFormat.Unpremul)
            return (Premultiply(b, a), Premultiply(g, a), Premultiply(r, a), a);
        return (b, g, r, a);
    }

    private static int Premultiply(int channel, int alpha) => (channel * alpha + 127) / 255;

    private static void AssertClose((int B, int G, int R, int A) expected, (int B, int G, int R, int A) actual, int tolerance, int x, int y)
    {
        Assert.True(Math.Abs(expected.B - actual.B) <= tolerance && Math.Abs(expected.G - actual.G) <= tolerance
            && Math.Abs(expected.R - actual.R) <= tolerance && Math.Abs(expected.A - actual.A) <= tolerance,
            $"Pixel {x}, {y}: expected {expected}, got {actual}.");
    }
}
