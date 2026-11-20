// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Media;
using Stride.AvaloniaUI.Rendering.Fallback;

namespace Stride.AvaloniaUI.Rendering.Geometry;

/// <summary>
/// A rectangle, outlined from its top left corner clockwise as Skia's.
/// </summary>
internal sealed class StrideRectangleGeometry(FallbackChannel channel, Rect rect) : StrideGeometry(channel)
{
    public Rect Rect { get; } = rect;

    public override PathData Path { get; } = new(
    [
        new PathCommand(PathCommandKind.BeginFigure, rect.TopLeft),
        new PathCommand(PathCommandKind.LineTo, rect.TopRight),
        new PathCommand(PathCommandKind.LineTo, rect.BottomRight),
        new PathCommand(PathCommandKind.LineTo, rect.BottomLeft),
        new PathCommand(PathCommandKind.EndFigure, default),
    ], FillRule.EvenOdd);
}

/// <summary>
/// An ellipse, outlined clockwise as two half-ellipse arcs.
/// </summary>
internal sealed class StrideEllipseGeometry(FallbackChannel channel, Rect rect) : StrideGeometry(channel)
{
    public Rect Rect { get; } = rect;

    public override PathData Path { get; } = CreatePath(rect);

    private static PathData CreatePath(Rect rect)
    {
        var left = new Point(rect.Left, rect.Center.Y);
        var right = new Point(rect.Right, rect.Center.Y);
        var radii = new Size(rect.Width / 2, rect.Height / 2);
        return new PathData(
        [
            new PathCommand(PathCommandKind.BeginFigure, left),
            new PathCommand(PathCommandKind.ArcTo, right, ArcSize: radii, Sweep: SweepDirection.Clockwise),
            new PathCommand(PathCommandKind.ArcTo, left, ArcSize: radii, Sweep: SweepDirection.Clockwise),
            new PathCommand(PathCommandKind.EndFigure, default),
        ], FillRule.EvenOdd);
    }
}

/// <summary>
/// A line, which has no fill.
/// </summary>
internal sealed class StrideLineGeometry(FallbackChannel channel, Point p1, Point p2) : StrideGeometry(channel)
{
    public Point P1 { get; } = p1;

    public Point P2 { get; } = p2;

    public override PathData Path { get; } = new(
    [
        new PathCommand(PathCommandKind.BeginFigure, p1, Flag: false),
        new PathCommand(PathCommandKind.LineTo, p2),
        new PathCommand(PathCommandKind.EndFigure, default, Flag: false),
    ], FillRule.EvenOdd);

    internal override bool HasFill => false;
}
