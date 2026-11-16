// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using Avalonia.Platform;
using Stride.AvaloniaUI.Rendering.Bitmaps;

namespace Stride.AvaloniaUI.Skia.Fallback;

/// <summary>
/// Skia's copy of a bitmap of the core, made again when the bitmap's version changes.
/// </summary>
internal sealed class SkiaBitmapCache : IDisposable
{
    private IBitmapImpl? bitmap;
    private int version;

    /// <summary>
    /// Gets Skia's copy of the pixels the bitmap has now, or <c>null</c> when it has none (0×0).
    /// </summary>
    public IBitmapImpl? Get(IPlatformRenderInterface skia, StrideBitmap source)
    {
        using (source.EnterReadScope())
        {
            if (bitmap != null && version == source.Version)
                return bitmap;

            Dispose();
            if (source.Address == IntPtr.Zero)
                return null;

            bitmap = skia.LoadBitmap(source.Format, source.AlphaFormat, source.Address, source.PixelSize, source.Dpi, source.RowBytes);
            version = source.Version;
            return bitmap;
        }
    }

    public void Dispose()
    {
        bitmap?.Dispose();
        bitmap = null;
    }
}
