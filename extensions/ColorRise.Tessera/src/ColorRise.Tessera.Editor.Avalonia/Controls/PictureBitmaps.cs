using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ColorRise.Tessera.Editor.Pictures;

namespace ColorRise.Tessera.Editor.Avalonia.Controls;

/// <summary>
/// One bitmap per picture, kept while the picture lives, and written again when the picture's version changed. The bitmaps
/// of pictures that are gone are disposed by <see cref="Sweep"/>, which also runs after each 64 bitmaps made. Used on the UI
/// thread.
/// </summary>
internal static class PictureBitmaps
{
    private const int SweepEvery = 64;

    private static readonly ConditionalWeakTable<TilePicture, Entry> Entries = new();

    // Every entry made and not disposed, with its picture held weakly: the table forgets an entry with its picture, this list
    // is how its bitmap is found again to be disposed
    private static readonly List<Entry> Live = [];

    private static int madeSinceSweep;

    /// <summary>
    /// The bitmaps made and not disposed.
    /// </summary>
    internal static int LiveBitmaps => Live.Count;

    /// <summary>
    /// The bitmap of a picture, as its pixels are now; null for a picture without pixels.
    /// </summary>
    public static Bitmap? Get(TilePicture picture)
    {
        if (picture.Width == 0 || picture.Height == 0)
        {
            return null;
        }
        if (!Entries.TryGetValue(picture, out var entry))
        {
            if (++madeSinceSweep >= SweepEvery)
            {
                Sweep();
            }
            entry = new Entry(picture, new WriteableBitmap(new PixelSize(picture.Width, picture.Height), new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul));
            Entries.Add(picture, entry);
            Live.Add(entry);
        }
        if (entry.Version != picture.Version)
        {
            Write(entry.Bitmap, picture);
            entry.Version = picture.Version;
        }
        return entry.Bitmap;
    }

    /// <summary>
    /// Disposes the bitmaps whose picture was collected.
    /// </summary>
    internal static void Sweep()
    {
        madeSinceSweep = 0;
        Live.RemoveAll(entry =>
        {
            if (entry.Picture.TryGetTarget(out _))
            {
                return false;
            }
            entry.Bitmap.Dispose();
            return true;
        });
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

    private sealed class Entry(TilePicture picture, WriteableBitmap bitmap)
    {
        public WeakReference<TilePicture> Picture { get; } = new(picture);

        public WriteableBitmap Bitmap { get; } = bitmap;

        // Null until the pixels were written once
        public int? Version { get; set; }
    }
}
