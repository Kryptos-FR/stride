// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Stride.AvaloniaUI.Rendering.Fallback;

namespace Stride.AvaloniaUI.Rendering.Geometry;

/// <summary>
/// A geometry of the core: its bounds and fill hit testing come from its own outline, the other operations from the fallback.
/// </summary>
internal abstract class StrideGeometry(FallbackChannel channel) : IGeometryImpl
{
    /// <summary>
    /// Gets the channel to the fallback, which answers what the core doesn't.
    /// </summary>
    public FallbackChannel Channel { get; } = channel;

    /// <summary>
    /// Gets or sets the fallback's equivalent of this geometry.
    /// </summary>
    internal object? FallbackObject { get; set; }

    /// <summary>
    /// Gets the outline of the geometry, or <c>null</c> when only the fallback knows it.
    /// </summary>
    public abstract PathData? Path { get; }

    /// <summary>
    /// Gets whether the geometry has a fill: a line has none, as in Skia.
    /// </summary>
    internal virtual bool HasFill => true;

    public Rect Bounds => Path is { } path ? path.Bounds : Fallback(DrawCallKind.GeometryBounds).Bounds;

    public double ContourLength => Fallback(DrawCallKind.GeometryContourMeasuring).ContourLength;

    public virtual bool FillContains(Point point)
        => Path is { } path ? HasFill && path.FillContains(point) : Fallback(DrawCallKind.GeometryFillContains).FillContains(point);

    public Rect GetRenderBounds(IPen? pen) => Fallback(DrawCallKind.GeometryRenderBounds).GetRenderBounds(pen);

    public IGeometryImpl GetWidenedGeometry(IPen pen) => Wrap(Fallback(DrawCallKind.GeometryWidening).GetWidenedGeometry(pen));

    public IGeometryImpl? Intersect(IGeometryImpl geometry)
    {
        var fallback = Fallback(DrawCallKind.GeometryIntersection);
        // The fallback intersects its own geometries only
        var other = geometry is StrideGeometry core ? core.Channel.Fallback.GetGeometry(core) : geometry;
        return fallback.Intersect(other) is { } intersection ? Wrap(intersection) : null;
    }

    public bool StrokeContains(IPen? pen, Point point) => Fallback(DrawCallKind.GeometryStrokeContains).StrokeContains(pen, point);

    public ITransformedGeometryImpl WithTransform(Matrix transform) => new StrideTransformedGeometry(this, transform);

    public bool TryGetPointAtDistance(double distance, out Point point)
        => Fallback(DrawCallKind.GeometryContourMeasuring).TryGetPointAtDistance(distance, out point);

    public bool TryGetPointAndTangentAtDistance(double distance, out Point point, out Point tangent)
        => Fallback(DrawCallKind.GeometryContourMeasuring).TryGetPointAndTangentAtDistance(distance, out point, out tangent);

    public bool TryGetSegment(double startDistance, double stopDistance, bool startOnBeginFigure, [NotNullWhen(true)] out IGeometryImpl? segmentGeometry)
    {
        if (Fallback(DrawCallKind.GeometryContourMeasuring).TryGetSegment(startDistance, stopDistance, startOnBeginFigure, out var segment))
        {
            segmentGeometry = Wrap(segment);
            return true;
        }
        segmentGeometry = null;
        return false;
    }

    /// <summary>
    /// Counts a call handed to the fallback, and gets the fallback's equivalent of this geometry to answer it.
    /// </summary>
    protected IGeometryImpl Fallback(DrawCallKind kind)
    {
        Channel.Count(kind);
        return Channel.Fallback.GetGeometry(this);
    }

    private FallbackGeometry Wrap(IGeometryImpl geometry) => new(Channel, geometry);
}
