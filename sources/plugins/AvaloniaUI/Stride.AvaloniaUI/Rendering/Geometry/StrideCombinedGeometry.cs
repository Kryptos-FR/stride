// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Media;
using Stride.AvaloniaUI.Rendering.Fallback;

namespace Stride.AvaloniaUI.Rendering.Geometry;

/// <summary>
/// Two geometries combined: its fill hit testing comes from theirs, its outline from the fallback.
/// </summary>
internal sealed class StrideCombinedGeometry(FallbackChannel channel, GeometryCombineMode mode, StrideGeometry first, StrideGeometry second) : StrideGeometry(channel)
{
    public GeometryCombineMode Mode { get; } = mode;

    public StrideGeometry First { get; } = first;

    public StrideGeometry Second { get; } = second;

    public override PathData? Path => null;

    // Skia combines the fills only when both have one
    internal override bool HasFill => First.HasFill && Second.HasFill;

    public override bool FillContains(Point point)
    {
        if (!HasFill)
            return false;

        var inFirst = First.FillContains(point);
        var inSecond = Second.FillContains(point);
        return Mode switch
        {
            GeometryCombineMode.Union => inFirst || inSecond,
            GeometryCombineMode.Intersect => inFirst && inSecond,
            GeometryCombineMode.Xor => inFirst != inSecond,
            _ => inFirst && !inSecond,
        };
    }
}
