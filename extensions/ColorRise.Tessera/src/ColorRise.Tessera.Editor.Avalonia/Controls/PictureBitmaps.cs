using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ColorRise.Tessera.Editor.Pictures;

namespace ColorRise.Tessera.Editor.Avalonia.Controls;

/// <summary>
/// One bitmap per picture, kept while the picture lives, and written again when the picture's version changed. Used on
/// the UI thread.
/// </summary>
internal static class PictureBitmaps
{
    private static readonly ConditionalWeakTable<TilePicture, Entry> Entries = new();

    /// <summary>
    /// The bitmap of a picture, as its pixels are now; null for a picture without pixels.
    /// </summary>
    public static Bitmap? Get(TilePicture picture)
    {
        if (picture.Width == 0 || picture.Height == 0)
        {
            return null;
        }
        var entry = Entries.GetValue(picture, made => new Entry(new WriteableBitmap(new PixelSize(made.Width, made.Height), new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul)));
        if (entry.Version != picture.Version)
        {
            Write(entry.Bitmap, picture);
            entry.Version = picture.Version;
        }
        return entry.Bitmap;
    }

    private static void Write(WriteableBitmap bitmap, TilePicture picture)
    {
        using var buffer = bitmap.Lock();
        var row = new byte[picture.Width * 4];
        for (var y = 0; y < picture.Height; y++)
        {
            // A Stride colour is its R, G, B and A bytes, in that order
            MemoryMarshal.AsBytes(picture.Pixels.AsSpan(y * picture.Width, picture.Width)).CopyTo(row);
            Marshal.Copy(row, 0, buffer.Address + y * buffer.RowBytes, row.Length);
        }
    }

    private sealed class Entry(WriteableBitmap bitmap)
    {
        public WriteableBitmap Bitmap { get; } = bitmap;

        // Null until the pixels were written once
        public int? Version { get; set; }
    }
}
