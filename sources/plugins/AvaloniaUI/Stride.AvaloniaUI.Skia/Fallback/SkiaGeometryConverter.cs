// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Linq;
using Avalonia.Platform;
using Stride.AvaloniaUI.Rendering.Geometry;

namespace Stride.AvaloniaUI.Skia.Fallback;

/// <summary>
/// Makes Skia's geometry from a core geometry, the way the whole-frame renderer would have made it.
/// </summary>
internal sealed class SkiaGeometryConverter(SkiaRenderFallback fallback, IPlatformRenderInterface skia)
{
    public IGeometryImpl Convert(StrideGeometry geometry)
    {
        switch (geometry)
        {
            case StrideStreamGeometry stream:
            {
                var converted = skia.CreateStreamGeometry();
                using (var context = converted.Open())
                    stream.Path!.Replay(context);
                return converted;
            }
            case StrideRectangleGeometry rectangle:
                return skia.CreateRectangleGeometry(rectangle.Rect);
            case StrideEllipseGeometry ellipse:
                return skia.CreateEllipseGeometry(ellipse.Rect);
            case StrideLineGeometry line:
                return skia.CreateLineGeometry(line.P1, line.P2);
            case StrideGeometryGroup group:
                return skia.CreateGeometryGroup(group.FillRule, group.Children.Select(fallback.GetGeometry).ToList());
            case StrideCombinedGeometry combined:
                return skia.CreateCombinedGeometry(combined.Mode, fallback.GetGeometry(combined.First), fallback.GetGeometry(combined.Second));
            case StrideTransformedGeometry transformed:
                return fallback.GetGeometry(transformed.Source).WithTransform(transformed.Transform);
            case FallbackGeometry fallbackGeometry:
                return fallbackGeometry.Inner;
            default:
                throw new NotSupportedException($"The Skia fallback can't convert a {geometry.GetType().Name}.");
        }
    }
}
