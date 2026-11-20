// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Media;

namespace Stride.AvaloniaUI.Rendering.Geometry;

/// <summary>
/// The kind of a <see cref="PathCommand"/>, one per call of Avalonia's geometry context.
/// </summary>
internal enum PathCommandKind : byte
{
    BeginFigure,
    LineTo,
    QuadraticBezierTo,
    CubicBezierTo,
    ArcTo,
    EndFigure,
}

/// <summary>
/// A call of Avalonia's geometry context, with its arguments.
/// </summary>
/// <param name="Point1">The start point (<see cref="PathCommandKind.BeginFigure"/>), the end point (lines and arcs), or the first control point (curves).</param>
/// <param name="Point2">The end point of a quadratic curve, or the second control point of a cubic one.</param>
/// <param name="Point3">The end point of a cubic curve.</param>
/// <param name="Flag">Whether the figure is filled (<see cref="PathCommandKind.BeginFigure"/>), the segment stroked, or the figure closed (<see cref="PathCommandKind.EndFigure"/>).</param>
internal readonly record struct PathCommand(PathCommandKind Kind, Point Point1, Point Point2 = default, Point Point3 = default,
    Size ArcSize = default, double ArcRotation = 0, bool IsLargeArc = false, SweepDirection Sweep = default, bool Flag = true)
{
    /// <summary>
    /// Gets the point a segment ends at.
    /// </summary>
    public Point EndPoint => Kind switch
    {
        PathCommandKind.QuadraticBezierTo => Point2,
        PathCommandKind.CubicBezierTo => Point3,
        _ => Point1,
    };
}
