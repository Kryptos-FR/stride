// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;

namespace Stride.AvaloniaUI.Rendering.Geometry;

/// <summary>
/// The outline of a geometry, as the calls of Avalonia's geometry context that made it, with its bounds and fill hit testing.
/// </summary>
/// <remarks>
/// Bounds and hit testing give Skia's answers: the bounds are tight (the extrema of the stroked curves, every figure start
/// and segment end), and a point on the outline of a filled figure is inside it.
/// </remarks>
internal sealed class PathData
{
    // How far the flattened curves of the fill hit testing are from the real ones, at most
    private const double FlatteningTolerance = 0.01;
    private const int MaxFlatteningSteps = 4096;

    private Rect? bounds;
    private List<Point[]>? fillPolygons;

    /// <summary>
    /// The outline with no figure.
    /// </summary>
    public static readonly PathData Empty = new([], FillRule.EvenOdd);

    public PathData(IReadOnlyList<PathCommand> commands, FillRule fillRule)
    {
        Commands = commands;
        FillRule = fillRule;
    }

    /// <summary>
    /// Gets the calls that made the outline, in order.
    /// </summary>
    public IReadOnlyList<PathCommand> Commands { get; }

    /// <summary>
    /// Gets how the filled figures fill.
    /// </summary>
    public FillRule FillRule { get; }

    /// <summary>
    /// Gets the tight bounds of the outline, or <c>default</c> when it has no figure.
    /// </summary>
    public Rect Bounds => bounds ??= ComputeBounds();

    /// <summary>
    /// Gets whether a point is inside the filled figures, implicitly closed, by the fill rule.
    /// </summary>
    public bool FillContains(Point point)
    {
        var polygons = fillPolygons ??= Flatten();
        var winding = 0;
        var onOutline = 0;
        foreach (var polygon in polygons)
        {
            for (var i = 0; i < polygon.Length; i++)
                winding += Winding(polygon[i], polygon[i + 1 < polygon.Length ? i + 1 : 0], point.X, point.Y, ref onOutline);
        }

        if (FillRule == FillRule.EvenOdd)
            winding &= 1;
        // On the outline, as Skia counts it: inside when on an odd number of edges
        return winding != 0 || (onOutline & 1) != 0;
    }

    /// <summary>
    /// Transforms the outline, arcs included, which stay exact arcs of the transformed ellipse.
    /// </summary>
    public PathData Transform(Matrix matrix)
    {
        var commands = new PathCommand[Commands.Count];
        var walker = new Walker();
        for (var i = 0; i < commands.Length; i++)
        {
            var command = Commands[i];
            commands[i] = command.Kind switch
            {
                PathCommandKind.BeginFigure or PathCommandKind.LineTo => command with { Point1 = matrix.Transform(command.Point1) },
                PathCommandKind.QuadraticBezierTo => command with { Point1 = matrix.Transform(command.Point1), Point2 = matrix.Transform(command.Point2) },
                PathCommandKind.CubicBezierTo => command with { Point1 = matrix.Transform(command.Point1), Point2 = matrix.Transform(command.Point2), Point3 = matrix.Transform(command.Point3) },
                PathCommandKind.ArcTo => TransformArc(walker.Current, command, matrix),
                _ => command,
            };
            walker.Move(command);
        }
        return new PathData(commands, FillRule);
    }

    /// <summary>
    /// Makes the same calls on another geometry context, in the same order, then sets the fill rule.
    /// </summary>
    public void Replay(IGeometryContext target)
    {
        foreach (var command in Commands)
        {
            switch (command.Kind)
            {
                case PathCommandKind.BeginFigure:
                    target.BeginFigure(command.Point1, command.Flag);
                    break;
                case PathCommandKind.LineTo:
                    target.LineTo(command.Point1, command.Flag);
                    break;
                case PathCommandKind.QuadraticBezierTo:
                    target.QuadraticBezierTo(command.Point1, command.Point2, command.Flag);
                    break;
                case PathCommandKind.CubicBezierTo:
                    target.CubicBezierTo(command.Point1, command.Point2, command.Point3, command.Flag);
                    break;
                case PathCommandKind.ArcTo:
                    target.ArcTo(command.Point1, command.ArcSize, command.ArcRotation, command.IsLargeArc, command.Sweep, command.Flag);
                    break;
                case PathCommandKind.EndFigure:
                    target.EndFigure(command.Flag);
                    break;
            }
        }
        target.SetFillRule(FillRule);
    }

    /// <summary>
    /// Puts the figures of several outlines in one, with another fill rule.
    /// </summary>
    public static PathData Concat(FillRule fillRule, IEnumerable<PathData> parts)
    {
        var commands = new List<PathCommand>();
        foreach (var part in parts)
            commands.AddRange(part.Commands);
        return new PathData(commands, fillRule);
    }

    private Rect ComputeBounds()
    {
        var minX = double.PositiveInfinity;
        var minY = double.PositiveInfinity;
        var maxX = double.NegativeInfinity;
        var maxY = double.NegativeInfinity;

        var walker = new Walker();
        foreach (var command in Commands)
        {
            if (command.Kind == PathCommandKind.BeginFigure)
            {
                Add(command.Point1);
            }
            else if (command.Kind != PathCommandKind.EndFigure)
            {
                // A segment with no figure starts one at the current point, as Skia does
                if (!walker.IsOpen)
                    Add(walker.Current);
                // Only the stroked segments are in the outline Skia measures: the others move to their end
                if (command.Flag)
                    AddExtrema(walker.Current, command);
                Add(command.EndPoint);
            }
            walker.Move(command);
        }
        return double.IsPositiveInfinity(minX) ? default : new Rect(minX, minY, maxX - minX, maxY - minY);

        void Add(Point point)
        {
            minX = Math.Min(minX, point.X);
            minY = Math.Min(minY, point.Y);
            maxX = Math.Max(maxX, point.X);
            maxY = Math.Max(maxY, point.Y);
        }

        void AddExtrema(Point start, in PathCommand command)
        {
            switch (command.Kind)
            {
                case PathCommandKind.QuadraticBezierTo:
                    foreach (var t in QuadraticExtrema(start.X, command.Point1.X, command.Point2.X))
                        Add(Quadratic(start, command.Point1, command.Point2, t));
                    foreach (var t in QuadraticExtrema(start.Y, command.Point1.Y, command.Point2.Y))
                        Add(Quadratic(start, command.Point1, command.Point2, t));
                    break;
                case PathCommandKind.CubicBezierTo:
                    foreach (var t in CubicExtrema(start.X, command.Point1.X, command.Point2.X, command.Point3.X))
                        Add(Cubic(start, command.Point1, command.Point2, command.Point3, t));
                    foreach (var t in CubicExtrema(start.Y, command.Point1.Y, command.Point2.Y, command.Point3.Y))
                        Add(Cubic(start, command.Point1, command.Point2, command.Point3, t));
                    break;
                case PathCommandKind.ArcTo when EllipticArc.TryCreate(start, command, out var arc):
                    // Where the derivative of each coordinate is zero, on the swept part of the ellipse
                    foreach (var angle in new[] { Math.Atan2(arc.V.X, arc.U.X), Math.Atan2(arc.V.Y, arc.U.Y) })
                    {
                        if (arc.Sweeps(angle))
                            Add(arc.PointAt(angle));
                        if (arc.Sweeps(angle + Math.PI))
                            Add(arc.PointAt(angle + Math.PI));
                    }
                    break;
            }
        }
    }

    // The filled figures as polygons, the curves flattened
    private List<Point[]> Flatten()
    {
        var polygons = new List<Point[]>();
        List<Point>? polygon = null;
        var filled = false;
        var walker = new Walker();
        foreach (var command in Commands)
        {
            switch (command.Kind)
            {
                case PathCommandKind.BeginFigure:
                    EndPolygon();
                    polygon = [command.Point1];
                    filled = command.Flag;
                    break;
                case PathCommandKind.EndFigure:
                    EndPolygon();
                    break;
                default:
                    if (polygon == null)
                    {
                        polygon = [walker.Current];
                        filled = true;
                    }
                    AddSegment(polygon, walker.Current, command);
                    break;
            }
            walker.Move(command);
        }
        EndPolygon();
        return polygons;

        void EndPolygon()
        {
            if (polygon != null && filled)
                polygons.Add(polygon.ToArray());
            polygon = null;
        }
    }

    private static void AddSegment(List<Point> polygon, Point start, in PathCommand command)
    {
        switch (command.Kind)
        {
            case PathCommandKind.QuadraticBezierTo:
            {
                // A uniform split is within the tolerance once (second derivative / 8) / steps² is
                var steps = Steps(2 * Length(start - 2 * command.Point1 + command.Point2));
                for (var i = 1; i < steps; i++)
                    polygon.Add(Quadratic(start, command.Point1, command.Point2, (double)i / steps));
                break;
            }
            case PathCommandKind.CubicBezierTo:
            {
                var steps = Steps(6 * Math.Max(Length(start - 2 * command.Point1 + command.Point2), Length(command.Point1 - 2 * command.Point2 + command.Point3)));
                for (var i = 1; i < steps; i++)
                    polygon.Add(Cubic(start, command.Point1, command.Point2, command.Point3, (double)i / steps));
                break;
            }
            case PathCommandKind.ArcTo when EllipticArc.TryCreate(start, command, out var arc):
            {
                // The second derivative of the ellipse is at most |U| + |V|
                var steps = (int)Math.Clamp(Math.Ceiling(Math.Abs(arc.SweepAngle) * Math.Sqrt((Length(arc.U) + Length(arc.V)) / (8 * FlatteningTolerance))), 1, MaxFlatteningSteps);
                for (var i = 1; i < steps; i++)
                    polygon.Add(arc.PointAt(arc.StartAngle + arc.SweepAngle * i / steps));
                break;
            }
        }
        polygon.Add(command.EndPoint);

        static int Steps(double secondDerivative)
            => (int)Math.Clamp(Math.Ceiling(Math.Sqrt(secondDerivative / (8 * FlatteningTolerance))), 1, MaxFlatteningSteps);
    }

    // Skia's winding of a line (SkPath::contains): the edges cross a ray going left of the point, ends half open
    private static int Winding(Point start, Point end, double x, double y, ref int onOutline)
    {
        var y0 = start.Y;
        var y1 = end.Y;
        var dy = y1 - y0;
        var direction = 1;
        if (y0 > y1)
        {
            (y0, y1) = (y1, y0);
            direction = -1;
        }
        if (y < y0 || y > y1)
            return 0;

        if (start.Y == end.Y ? (start.X - x) * (end.X - x) <= 0 && x != end.X : x == start.X && y == start.Y)
        {
            onOutline++;
            return 0;
        }
        if (y == y1)
            return 0;

        var cross = (end.X - start.X) * (y - start.Y) - dy * (x - start.X);
        if (cross == 0)
        {
            if (x != end.X || y != end.Y)
                onOutline++;
            direction = 0;
        }
        else if (Math.Sign(cross) == direction)
        {
            direction = 0;
        }
        return direction;
    }

    private static PathCommand TransformArc(Point start, in PathCommand command, Matrix matrix)
    {
        var end = matrix.Transform(command.Point1);
        if (!EllipticArc.TryCreate(start, command, out var arc))
            return new PathCommand(PathCommandKind.LineTo, end, Flag: command.Flag);

        // The transformed ellipse is C' + U' cos + V' sin: its axes are the eigenvectors of [U' V'][U' V']ᵀ
        var u = TransformVector(arc.U, matrix);
        var v = TransformVector(arc.V, matrix);
        var a = u.X * u.X + v.X * v.X;
        var b = u.X * u.Y + v.X * v.Y;
        var c = u.Y * u.Y + v.Y * v.Y;
        var mean = (a + c) / 2;
        var spread = Math.Sqrt((a - c) * (a - c) / 4 + b * b);
        var radii = new Size(Math.Sqrt(mean + spread), Math.Sqrt(Math.Max(0, mean - spread)));
        var rotation = Math.Atan2(2 * b, a - c) / 2 * 180 / Math.PI;
        // A mirroring transform turns the arc the other way
        var mirrored = matrix.M11 * matrix.M22 - matrix.M12 * matrix.M21 < 0;
        var clockwise = arc.SweepAngle > 0 != mirrored;
        return new PathCommand(PathCommandKind.ArcTo, end, ArcSize: radii, ArcRotation: rotation, IsLargeArc: Math.Abs(arc.SweepAngle) > Math.PI,
            Sweep: clockwise ? SweepDirection.Clockwise : SweepDirection.CounterClockwise, Flag: command.Flag);

        static Vector TransformVector(Vector vector, Matrix matrix)
            => new(vector.X * matrix.M11 + vector.Y * matrix.M21, vector.X * matrix.M12 + vector.Y * matrix.M22);
    }

    private static Point Quadratic(Point p0, Point p1, Point p2, double t)
    {
        var s = 1 - t;
        return new Point(s * s * p0.X + 2 * s * t * p1.X + t * t * p2.X, s * s * p0.Y + 2 * s * t * p1.Y + t * t * p2.Y);
    }

    private static Point Cubic(Point p0, Point p1, Point p2, Point p3, double t)
    {
        var s = 1 - t;
        double a = s * s * s, b = 3 * s * s * t, c = 3 * s * t * t, d = t * t * t;
        return new Point(a * p0.X + b * p1.X + c * p2.X + d * p3.X, a * p0.Y + b * p1.Y + c * p2.Y + d * p3.Y);
    }

    // The parameters in (0, 1) where one coordinate of a quadratic curve has its extremum
    private static IEnumerable<double> QuadraticExtrema(double p0, double p1, double p2)
    {
        var denominator = p0 - 2 * p1 + p2;
        if (denominator != 0)
        {
            var t = (p0 - p1) / denominator;
            if (t > 0 && t < 1)
                yield return t;
        }
    }

    // The parameters in (0, 1) where one coordinate of a cubic curve has its extrema: the roots of its derivative
    private static IEnumerable<double> CubicExtrema(double p0, double p1, double p2, double p3)
    {
        var a = -p0 + 3 * p1 - 3 * p2 + p3;
        var b = 2 * (p0 - 2 * p1 + p2);
        var c = p1 - p0;
        if (Math.Abs(a) < 1e-12)
        {
            if (b != 0)
            {
                var t = -c / b;
                if (t > 0 && t < 1)
                    yield return t;
            }
            yield break;
        }

        var discriminant = b * b - 4 * a * c;
        if (discriminant < 0)
            yield break;
        var root = Math.Sqrt(discriminant);
        foreach (var t in new[] { (-b + root) / (2 * a), (-b - root) / (2 * a) })
        {
            if (t > 0 && t < 1)
                yield return t;
        }
    }

    private static double Length(Vector vector) => Math.Sqrt(vector.X * vector.X + vector.Y * vector.Y);

    /// <summary>
    /// Follows the current point and the start of the figure through the calls.
    /// </summary>
    private struct Walker
    {
        private Point start;

        public Point Current { get; private set; }

        public bool IsOpen { get; private set; }

        public void Move(in PathCommand command)
        {
            switch (command.Kind)
            {
                case PathCommandKind.BeginFigure:
                    start = Current = command.Point1;
                    IsOpen = true;
                    break;
                case PathCommandKind.EndFigure:
                    // A closed figure ends where it started
                    if (command.Flag)
                        Current = start;
                    IsOpen = false;
                    break;
                default:
                    if (!IsOpen)
                    {
                        start = Current;
                        IsOpen = true;
                    }
                    Current = command.EndPoint;
                    break;
            }
        }
    }

    /// <summary>
    /// An arc of an ellipse, as the points <c>Center + U cos θ + V sin θ</c> from a start angle over a signed sweep.
    /// </summary>
    private readonly record struct EllipticArc(Point Center, Vector U, Vector V, double StartAngle, double SweepAngle)
    {
        public Point PointAt(double angle) => Center + U * Math.Cos(angle) + V * Math.Sin(angle);

        public bool Sweeps(double angle)
        {
            var offset = SweepAngle >= 0 ? angle - StartAngle : StartAngle - angle;
            offset %= 2 * Math.PI;
            if (offset < 0)
                offset += 2 * Math.PI;
            return offset <= Math.Abs(SweepAngle);
        }

        /// <summary>
        /// Gets the arc of an <see cref="PathCommandKind.ArcTo"/> from its end points, as the SVG implementation notes say (F.6.5, F.6.6).
        /// </summary>
        /// <returns><c>false</c> when the arc is a line: a zero radius, or the same start and end.</returns>
        public static bool TryCreate(Point start, in PathCommand command, out EllipticArc arc)
        {
            arc = default;
            var end = command.Point1;
            var rx = Math.Abs(command.ArcSize.Width);
            var ry = Math.Abs(command.ArcSize.Height);
            if (rx == 0 || ry == 0 || start == end)
                return false;

            var angle = command.ArcRotation * Math.PI / 180;
            var cos = Math.Cos(angle);
            var sin = Math.Sin(angle);
            var halfX = (start.X - end.X) / 2;
            var halfY = (start.Y - end.Y) / 2;
            var x1 = cos * halfX + sin * halfY;
            var y1 = -sin * halfX + cos * halfY;

            // Radii too small to reach the end are scaled up
            var lambda = x1 * x1 / (rx * rx) + y1 * y1 / (ry * ry);
            if (lambda > 1)
            {
                var scale = Math.Sqrt(lambda);
                rx *= scale;
                ry *= scale;
            }

            var numerator = rx * rx * ry * ry - rx * rx * y1 * y1 - ry * ry * x1 * x1;
            var denominator = rx * rx * y1 * y1 + ry * ry * x1 * x1;
            var coefficient = Math.Sqrt(Math.Max(0, numerator / denominator));
            var clockwise = command.Sweep == SweepDirection.Clockwise;
            if (command.IsLargeArc == clockwise)
                coefficient = -coefficient;
            var cx1 = coefficient * rx * y1 / ry;
            var cy1 = -coefficient * ry * x1 / rx;
            var center = new Point(cos * cx1 - sin * cy1 + (start.X + end.X) / 2, sin * cx1 + cos * cy1 + (start.Y + end.Y) / 2);

            var ux = (x1 - cx1) / rx;
            var uy = (y1 - cy1) / ry;
            var vx = (-x1 - cx1) / rx;
            var vy = (-y1 - cy1) / ry;
            var startAngle = Math.Atan2(uy, ux);
            var sweep = Math.Atan2(ux * vy - uy * vx, ux * vx + uy * vy);
            if (clockwise && sweep < 0)
                sweep += 2 * Math.PI;
            else if (!clockwise && sweep > 0)
                sweep -= 2 * Math.PI;

            arc = new EllipticArc(center, new Vector(rx * cos, rx * sin), new Vector(-ry * sin, ry * cos), startAngle, sweep);
            return true;
        }
    }
}
