// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Linq.Expressions;
using System.Reflection;
using Avalonia.Media;
using Avalonia.Platform;

namespace Stride.AvaloniaUI.Skia.Fallback;

/// <summary>
/// Reads the bitmap of the source of an image brush, which Avalonia keeps internal.
/// </summary>
/// <remarks>
/// The bitmap is an internal property of <see cref="IImageBrushSource"/> whose type, a reference to the bitmap, is internal too,
/// so it is read by a compiled expression.
/// </remarks>
internal static class ImageBrushSources
{
    private static readonly Func<IImageBrushSource, IBitmapImpl?> GetBitmapOfSource = CreateGetBitmap();

    /// <summary>
    /// Gets the bitmap an image brush draws, or <c>null</c> when it has none.
    /// </summary>
    public static IBitmapImpl? GetBitmap(IImageBrushSource source) => GetBitmapOfSource(source);

    // source => source.Bitmap?.Item
    private static Func<IImageBrushSource, IBitmapImpl?> CreateGetBitmap()
    {
        var bitmapProperty = typeof(IImageBrushSource).GetProperty("Bitmap", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMemberException(typeof(IImageBrushSource).FullName, "Bitmap");
        var itemProperty = bitmapProperty.PropertyType.GetProperty("Item")
            ?? throw new MissingMemberException(bitmapProperty.PropertyType.FullName, "Item");

        var source = Expression.Parameter(typeof(IImageBrushSource), "source");
        var reference = Expression.Variable(bitmapProperty.PropertyType, "reference");
        var body = Expression.Block(
            [reference],
            Expression.Assign(reference, Expression.Property(source, bitmapProperty)),
            Expression.Condition(
                Expression.Equal(reference, Expression.Constant(null, bitmapProperty.PropertyType)),
                Expression.Constant(null, typeof(IBitmapImpl)),
                Expression.Property(reference, itemProperty),
                typeof(IBitmapImpl)));
        return Expression.Lambda<Func<IImageBrushSource, IBitmapImpl?>>(body, source).Compile();
    }
}
