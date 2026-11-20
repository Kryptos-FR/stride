// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Stride.AvaloniaUI.Rendering.Fallback;

namespace Stride.AvaloniaUI.Rendering.Geometry;

/// <summary>
/// A geometry described by the calls of a geometry context, which it records.
/// </summary>
internal sealed class StrideStreamGeometry(FallbackChannel channel) : StrideGeometry(channel), IStreamGeometryImpl
{
    private PathData path = PathData.Empty;

    public override PathData? Path => path;

    public IStreamGeometryImpl Clone() => new StrideStreamGeometry(Channel) { path = path };

    /// <summary>
    /// Opens the geometry to add figures to it, as Skia's does; the fallback's equivalent is made again.
    /// </summary>
    public IStreamGeometryContextImpl Open()
    {
        FallbackObject = null;
        return new Context(this);
    }

    private sealed class Context(StrideStreamGeometry geometry) : IStreamGeometryContextImpl
    {
        private readonly List<PathCommand> commands = [.. geometry.path.Commands];
        private FillRule fillRule = geometry.path.FillRule;
        private bool disposed;

        public void BeginFigure(Point startPoint, bool isFilled = true)
            => commands.Add(new PathCommand(PathCommandKind.BeginFigure, startPoint, Flag: isFilled));

        public void LineTo(Point point, bool isStroked = true)
            => commands.Add(new PathCommand(PathCommandKind.LineTo, point, Flag: isStroked));

        public void QuadraticBezierTo(Point controlPoint, Point endPoint, bool isStroked = true)
            => commands.Add(new PathCommand(PathCommandKind.QuadraticBezierTo, controlPoint, endPoint, Flag: isStroked));

        public void CubicBezierTo(Point controlPoint1, Point controlPoint2, Point endPoint, bool isStroked = true)
            => commands.Add(new PathCommand(PathCommandKind.CubicBezierTo, controlPoint1, controlPoint2, endPoint, Flag: isStroked));

        public void ArcTo(Point point, Size size, double rotationAngle, bool isLargeArc, SweepDirection sweepDirection, bool isStroked = true)
            => commands.Add(new PathCommand(PathCommandKind.ArcTo, point, ArcSize: size, ArcRotation: rotationAngle, IsLargeArc: isLargeArc, Sweep: sweepDirection, Flag: isStroked));

        public void EndFigure(bool isClosed)
            => commands.Add(new PathCommand(PathCommandKind.EndFigure, default, Flag: isClosed));

        public void SetFillRule(FillRule fillRule) => this.fillRule = fillRule;

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            geometry.path = new PathData(commands, fillRule);
            geometry.FallbackObject = null;
        }
    }
}
