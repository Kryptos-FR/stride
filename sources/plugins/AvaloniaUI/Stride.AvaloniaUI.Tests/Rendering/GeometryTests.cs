// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Platform;
using Stride.AvaloniaUI.Rendering.Fallback;
using Stride.AvaloniaUI.Rendering.Geometry;
using Xunit;

namespace Stride.AvaloniaUI.Tests.Rendering;

/// <summary>
/// Checks the core's geometry against Skia's: bounds and fill hit testing of its own, the other operations from the fallback.
/// </summary>
public sealed class GeometryTests
{
    private static readonly Matrix Transform = Matrix.CreateRotation(Math.PI / 6) * Matrix.CreateSkew(0.3, 0) * Matrix.CreateTranslation(7, -3);

    public static TheoryData<string> Paths => new()
    {
        "M 10 10 L 90 10 L 50 80 Z",
        // Extrema beyond the end points
        "M 0 0 C 100 -50 -50 150 80 80",
        "M 0 50 Q 50 -40 100 50",
        // A rotated large arc
        "M 10 50 A 40 20 30 1 1 90 50",
        // Radii too small
        "M 10 50 A 5 5 0 0 0 90 50",
        // A zero radius
        "M 20 20 A 0 10 0 0 1 60 60",
        "F1 M 0 0 L 100 0 L 100 100 L 0 100 Z M 25 25 L 75 25 L 75 75 L 25 75 Z",
        "F0 M 0 0 L 100 0 L 100 100 L 0 100 Z M 25 25 L 75 25 L 75 75 L 25 75 Z",
        // A figure with only its start point
        "M 5 5",
        // Open figures
        "M 30 30 L 60 30 M 0 0 L 100 100 L 0 100",
        // Fluent's check mark
        "M 3.5 8 L 6.5 11 L 12.5 5",
    };

    [Theory]
    [MemberData(nameof(Paths))]
    public void StreamGeometryHasSkiasBoundsAndFill(string data) => TestAvalonia.Run(() =>
    {
        var core = Parse(SkiaOracle.CreateChannel(), data);
        var skia = SkiaOracle.Parse(data);
        AssertSameBoundsAndFill(skia, core);
    });

    [Fact]
    public void ShapesGroupsAndCombinationsHaveSkiasBoundsAndFill() => TestAvalonia.Run(() =>
    {
        var channel = SkiaOracle.CreateChannel();
        var skia = SkiaOracle.RenderInterface;
        var rect = new Rect(10, 20, 80, 50);
        var ellipseRect = new Rect(5, 10, 90, 60);
        var otherEllipseRect = new Rect(40, 30, 70, 50);

        AssertSameBoundsAndFill(skia.CreateRectangleGeometry(rect), new StrideRectangleGeometry(channel, rect));
        AssertSameBoundsAndFill(skia.CreateEllipseGeometry(ellipseRect), new StrideEllipseGeometry(channel, ellipseRect));
        AssertSameBoundsAndFill(skia.CreateLineGeometry(new Point(10, 10), new Point(90, 70)), new StrideLineGeometry(channel, new Point(10, 10), new Point(90, 70)));

        foreach (var fillRule in new[] { FillRule.EvenOdd, FillRule.NonZero })
        {
            var skiaGroup = skia.CreateGeometryGroup(fillRule, [skia.CreateEllipseGeometry(ellipseRect), skia.CreateEllipseGeometry(otherEllipseRect)]);
            var coreGroup = new StrideGeometryGroup(channel, fillRule, [new StrideEllipseGeometry(channel, ellipseRect), new StrideEllipseGeometry(channel, otherEllipseRect)]);
            AssertSameBoundsAndFill(skiaGroup, coreGroup);
        }

        foreach (var mode in new[] { GeometryCombineMode.Union, GeometryCombineMode.Intersect, GeometryCombineMode.Xor, GeometryCombineMode.Exclude })
        {
            var skiaCombined = skia.CreateCombinedGeometry(mode, skia.CreateRectangleGeometry(rect), skia.CreateEllipseGeometry(otherEllipseRect));
            var coreCombined = new StrideCombinedGeometry(channel, mode, new StrideRectangleGeometry(channel, rect), new StrideEllipseGeometry(channel, otherEllipseRect));
            AssertSameBoundsAndFill(skiaCombined, coreCombined);
        }
    });

    [Fact]
    public void OtherOperationsGiveSkiasAnswersAndAreCounted() => TestAvalonia.Run(() =>
    {
        var channel = SkiaOracle.CreateChannel();
        var core = Parse(channel, "M 0 0 C 100 -50 -50 150 80 80");
        var skia = SkiaOracle.Parse("M 0 0 C 100 -50 -50 150 80 80");
        var pen = new ImmutablePen(Brushes.Black, 6, lineCap: PenLineCap.Round);

        Assert.Equal(skia.GetRenderBounds(pen), core.GetRenderBounds(pen));
        Assert.Equal(skia.GetWidenedGeometry(pen).Bounds, core.GetWidenedGeometry(pen).Bounds);
        Assert.Equal(skia.ContourLength, core.ContourLength);
        Assert.True(core.TryGetPointAtDistance(40, out var point));
        skia.TryGetPointAtDistance(40, out var skiaPoint);
        Assert.Equal(skiaPoint, point);
        Assert.Equal(1, channel.GetCount(DrawCallKind.GeometryWidening));
        Assert.Equal(1, channel.GetCount(DrawCallKind.GeometryRenderBounds));
        Assert.Equal(2, channel.GetCount(DrawCallKind.GeometryContourMeasuring));

        var widened = Assert.IsType<FallbackGeometry>(core.GetWidenedGeometry(pen));
        Assert.Same(widened.Inner, channel.Fallback.GetGeometry(widened));

        // The intersection of two core geometries is Skia's, between their converted geometries
        var square = new Rect(0, 0, 40, 40);
        var intersection = core.Intersect(new StrideRectangleGeometry(channel, square));
        Assert.NotNull(intersection);
        Assert.Equal(skia.Intersect(SkiaOracle.RenderInterface.CreateRectangleGeometry(square))!.Bounds, intersection.Bounds);
        Assert.Equal(1, channel.GetCount(DrawCallKind.GeometryIntersection));
    });

    [Fact]
    public void EmptyStreamGeometryHasNoBoundsAndConverts() => TestAvalonia.Run(() =>
    {
        var channel = SkiaOracle.CreateChannel();
        var core = new StrideStreamGeometry(channel);
        core.Open().Dispose();
        var skia = SkiaOracle.RenderInterface.CreateStreamGeometry();
        skia.Open().Dispose();

        Assert.Equal(default, core.Bounds);
        Assert.Equal(skia.Bounds, core.Bounds);
        Assert.False(core.FillContains(new Point(0, 0)));
        Assert.Equal(default, channel.Fallback.GetGeometry(core).Bounds);
    });

    [Fact]
    public void CloneIsIndependentAndReopeningDropsTheConversion() => TestAvalonia.Run(() =>
    {
        var channel = SkiaOracle.CreateChannel();
        var core = Parse(channel, "M 0 0 L 10 0 L 10 10 Z");
        var clone = (StrideStreamGeometry)core.Clone();
        var converted = channel.Fallback.GetGeometry(core);

        using (var context = core.Open())
        {
            context.BeginFigure(new Point(50, 50));
            context.LineTo(new Point(60, 60));
            context.EndFigure(false);
        }

        Assert.Equal(new Rect(0, 0, 60, 60), core.Bounds);
        Assert.Equal(new Rect(0, 0, 10, 10), clone.Bounds);
        var reconverted = channel.Fallback.GetGeometry(core);
        Assert.NotSame(converted, reconverted);
        Assert.Equal(core.Bounds, reconverted.Bounds);
        Assert.Equal(new Rect(0, 0, 10, 10), channel.Fallback.GetGeometry(clone).Bounds);
    });

    private static StrideStreamGeometry Parse(FallbackChannel channel, string data)
    {
        var geometry = new StrideStreamGeometry(channel);
        using (var context = geometry.Open())
        using (var parser = new PathMarkupParser(context))
            parser.Parse(data);
        return geometry;
    }

    private static void AssertSameBoundsAndFill(IGeometryImpl skia, IGeometryImpl core)
    {
        AssertClose(skia.Bounds, core.Bounds, 0.01);
        AssertSameFill(skia, core);
        AssertClose(skia.WithTransform(Transform).Bounds, core.WithTransform(Transform).Bounds, 0.01);
        AssertSameFill(skia.WithTransform(Transform), core.WithTransform(Transform));
    }

    private static void AssertClose(Rect expected, Rect actual, double tolerance)
    {
        Assert.True(Math.Abs(expected.Left - actual.Left) <= tolerance && Math.Abs(expected.Top - actual.Top) <= tolerance
            && Math.Abs(expected.Right - actual.Right) <= tolerance && Math.Abs(expected.Bottom - actual.Bottom) <= tolerance,
            $"Expected bounds {expected}, got {actual}.");
    }

    // Points every 0.5 units over the bounds inflated by 5, except those on the outline, where both are right: the core flattens
    // curves to within 0.01, so a point whose 8 neighbors 0.05 away aren't all on its side of Skia's outline is skipped
    private static void AssertSameFill(IGeometryImpl skia, IGeometryImpl core)
    {
        const double near = 0.05;
        var bounds = skia.Bounds.Inflate(5);
        var differences = new List<Point>();
        var tested = 0;
        for (var y = bounds.Top; y <= bounds.Bottom; y += 0.5)
        {
            for (var x = bounds.Left; x <= bounds.Right; x += 0.5)
            {
                var point = new Point(x, y);
                var inSkia = skia.FillContains(point);
                if (!AllNeighborsAre(skia, point, near, inSkia))
                    continue;
                tested++;
                if (inSkia != core.FillContains(point))
                    differences.Add(point);
            }
        }
        Assert.True(differences.Count == 0, $"{differences.Count} of {tested} points differ from Skia's fill, such as {string.Join(", ", differences.GetRange(0, Math.Min(5, differences.Count)))} (Skia: {(differences.Count > 0 ? skia.FillContains(differences[0]) : false)}).");
    }

    private static bool AllNeighborsAre(IGeometryImpl geometry, Point point, double distance, bool inFill)
    {
        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                if ((dx != 0 || dy != 0) && geometry.FillContains(new Point(point.X + dx * distance, point.Y + dy * distance)) != inFill)
                    return false;
            }
        }
        return true;
    }
}
