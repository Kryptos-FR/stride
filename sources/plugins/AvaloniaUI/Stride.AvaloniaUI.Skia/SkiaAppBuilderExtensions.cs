// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Stride.AvaloniaUI.Rendering;

namespace Stride.AvaloniaUI.Skia;

/// <summary>
/// Sets up the Skia package on an Avalonia <see cref="AppBuilder"/>.
/// </summary>
public static class SkiaAppBuilderExtensions
{
    /// <summary>
    /// Gives the Stride platform the Skia package, for a game that sets Avalonia up itself.
    /// </summary>
    /// <remarks>A game that references the package finds it without this call; it makes the choice explicit.</remarks>
    public static AppBuilder UseSkiaFallback(this AppBuilder builder)
        => builder.With<IAvaloniaUISkia>(new SkiaPackage());
}
