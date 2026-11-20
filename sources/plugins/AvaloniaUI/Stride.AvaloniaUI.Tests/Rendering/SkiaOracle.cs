// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Stride.AvaloniaUI.Rendering.Fallback;
using Stride.AvaloniaUI.Skia.Fallback;

namespace Stride.AvaloniaUI.Tests.Rendering;

/// <summary>
/// Skia's answers, from the render interface Avalonia has in this process (the whole-frame Skia renderer).
/// </summary>
internal static class SkiaOracle
{
    public static IPlatformRenderInterface RenderInterface => AvaloniaLocator.Current.GetRequiredService<IPlatformRenderInterface>();

    /// <summary>
    /// Creates a channel to a Skia fallback over the render interface and font manager of this process.
    /// </summary>
    public static FallbackChannel CreateChannel()
        => new(new SkiaRenderFallback(RenderInterface, AvaloniaLocator.Current.GetRequiredService<IFontManagerImpl>()));

    /// <summary>
    /// Parses path markup into a Skia stream geometry.
    /// </summary>
    public static IGeometryImpl Parse(string data)
    {
        var geometry = RenderInterface.CreateStreamGeometry();
        using (var context = geometry.Open())
        using (var parser = new PathMarkupParser(context))
            parser.Parse(data);
        return geometry;
    }
}
