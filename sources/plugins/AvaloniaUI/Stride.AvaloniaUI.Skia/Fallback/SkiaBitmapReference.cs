// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Stride.AvaloniaUI.Skia.Fallback;

/// <summary>
/// A bitmap of Avalonia over a bitmap of Skia, so that the Skia bitmap can be the source of an image brush.
/// </summary>
/// <remarks>
/// Disposing it disposes the Skia bitmap, and so does the finalizer of what it holds once nothing refers to it.
/// </remarks>
internal sealed class SkiaBitmapReference(IBitmapImpl skiaBitmap) : Bitmap(skiaBitmap)
{
    /// <summary>
    /// Gets the Skia bitmap.
    /// </summary>
    public IBitmapImpl SkiaBitmap { get; } = skiaBitmap;
}
