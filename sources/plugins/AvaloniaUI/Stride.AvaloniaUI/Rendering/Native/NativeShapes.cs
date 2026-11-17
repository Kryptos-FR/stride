// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Media;
using Stride.AvaloniaUI.Rendering.Drawing;
using Stride.AvaloniaUI.Rendering.Fallback;
using Stride.AvaloniaUI.Rendering.Geometry;

namespace Stride.AvaloniaUI.Rendering.Native;

/// <summary>
/// A shape of the native canvas: a rounded rectangle in its own frame, filled, or the ring between it and an inner one.
/// </summary>
/// <param name="Frame">The transform from the shape's frame to the pixels of the target.</param>
/// <param name="Outer">The outline of the shape, in its frame.</param>
/// <param name="Inner">The hole of a stroke, in the same frame, or <c>null</c> for a filled shape.</param>
/// <param name="Color">The color, its alpha multiplied by the brush's opacity.</param>
/// <param name="Aliased">Whether a pixel is covered whole when its center is inside the shape, and not at all otherwise.</param>
internal readonly record struct CanvasShape(Matrix Frame, RoundedRect Outer, RoundedRect? Inner, Color Color, bool Aliased);

/// <summary>
/// Turns the draw calls the native canvas draws itself into shapes, the way Skia draws them.
/// </summary>
/// <remarks>
/// Rectangles, rounded rectangles, ellipses and lines with a solid brush or none, and a solid pen that isn't dashed or none,
/// are drawn natively. A rectangle's sharp corners need a miter join. Every other call goes to the fallback, with the reason.
/// </remarks>
internal static class NativeShapes
{
    // Skia draws a right-angled corner with a miter when the miter limit is at least the miter's length, √2 thicknesses
    private const double RightAngleMiter = 1.4142135623730951;

    /// <summary>
    /// Gets the shapes a draw call is made of, in order (a fill, then a stroke), or the reason it goes to the fallback.
    /// </summary>
    /// <param name="command">A draw call.</param>
    /// <param name="transform">The transform the call is drawn with.</param>
    /// <param name="options">The render options the call is drawn with.</param>
    /// <param name="shapes">The list the shapes are added to; a call that draws nothing adds none.</param>
    /// <returns><c>null</c> when the call is drawn natively, otherwise why it isn't.</returns>
    public static FallbackReason? TryGet(DrawCommand command, Matrix transform, RenderOptions options, List<CanvasShape> shapes)
    {
        switch (command)
        {
            case DrawGlyphRunCommand:
                return FallbackReason.Text;
            case DrawBitmapCommand or DrawBitmapWithOpacityMaskCommand:
                return FallbackReason.Bitmap;
            case DrawAcrylicRectangleCommand:
                return FallbackReason.Acrylic;
        }

        if (transform.ContainsPerspective())
            return FallbackReason.Perspective;
        var aliased = options.EdgeMode == EdgeMode.Aliased;
        return command switch
        {
            DrawRectangleCommand rectangle => TryGetRectangle(rectangle, transform, aliased, shapes),
            DrawEllipseCommand ellipse => TryGetEllipse(ellipse.Brush, ellipse.Pen, ellipse.Rect, transform, transform, aliased, shapes, isGeometry: false),
            DrawLineCommand line => TryGetLine(line.Pen, line.P1, line.P2, transform, aliased, shapes),
            DrawGeometryCommand geometry => TryGetGeometry(geometry, transform, aliased, shapes),
            _ => throw new ArgumentOutOfRangeException(nameof(command), command, "Not a draw call."),
        };
    }

    // Skia draws nothing for an empty rectangle, and leaves out the box shadows of one larger than 8192 pixels. It strokes a
    // sharp rectangle that stays one under the transform as a frame of exact edges, and every other outline as a path.
    private static FallbackReason? TryGetRectangle(DrawRectangleCommand command, Matrix transform, bool aliased, List<CanvasShape> shapes)
    {
        var rect = command.Rect.Rect;
        if (rect.Width <= 0 || rect.Height <= 0)
            return null;
        if (rect.Width <= 8192 && rect.Height <= 8192)
        {
            foreach (var shadow in command.BoxShadows)
            {
                if (shadow != default)
                    return FallbackReason.BoxShadow;
            }
        }

        var outline = Normalize(command.Rect);
        var hasSharpCorners = IsSharp(outline.RadiiTopLeft) || IsSharp(outline.RadiiTopRight) || IsSharp(outline.RadiiBottomRight) || IsSharp(outline.RadiiBottomLeft);
        var strokedAsPath = outline.IsRounded || !(transform is { M12: 0, M21: 0 } || transform is { M11: 0, M22: 0 });
        return TryGetShape(command.Brush, command.Pen, outline, hasSharpCorners, transform, transform, strokedAsPath, aliased, shapes);
    }

    // Skia draws nothing for an empty ellipse given to DrawEllipse; an empty one of a geometry has an outline it strokes
    private static FallbackReason? TryGetEllipse(IBrush? brush, IPen? pen, Rect rect, Matrix frame, Matrix transform, bool aliased, List<CanvasShape> shapes, bool isGeometry)
    {
        if (rect.Width <= 0 || rect.Height <= 0)
            return isGeometry && IsVisible(pen) ? FallbackReason.Path : null;
        var radii = new Vector(rect.Width / 2, rect.Height / 2);
        return TryGetShape(brush, pen, new RoundedRect(rect, radii, radii, radii, radii), hasSharpCorners: false, frame, transform, strokedAsPath: true, aliased, shapes);
    }

    // A line is a rectangle in its own frame: x along the line, y across it
    private static FallbackReason? TryGetLine(IPen? pen, Point p1, Point p2, Matrix transform, bool aliased, List<CanvasShape> shapes)
    {
        if (!IsVisible(pen))
            return null;
        if (CheckPen(pen, hasSharpCorners: false) is { } reason)
            return reason;

        var direction = p2 - p1;
        var length = Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y);
        if (length == 0)
        {
            // Skia draws a dot for the caps of a line of no length
            return pen.LineCap == PenLineCap.Flat ? null : FallbackReason.Path;
        }
        if (GetStroke(pen, transform, strokedAsPath: true, aliased) is not { } stroke)
            return FallbackReason.Stroke;

        // A hairline has no round caps: Skia lengthens it by π/8 of a pixel for them, and by half a pixel for square ones
        var half = stroke.Half;
        var cap = pen.LineCap switch
        {
            PenLineCap.Flat => 0,
            PenLineCap.Round when stroke.IsHairline => half * Math.PI / 4,
            _ => half,
        };
        var radii = pen.LineCap == PenLineCap.Round && !stroke.IsHairline ? new Vector(half, half) : default;
        var x = direction / length;
        var frame = new Matrix(x.X, x.Y, -x.Y, x.X, p1.X, p1.Y) * transform;
        var rect = new Rect(-cap, -half, length + 2 * cap, 2 * half);
        if (stroke.Color(GetColor((ISolidColorBrush)pen.Brush!)) is { A: > 0 } color)
            shapes.Add(new CanvasShape(frame, new RoundedRect(rect, radii, radii, radii, radii), null, color, aliased));
        return null;
    }

    // Avalonia gives Skia the geometry with its own transforms applied, and Skia strokes it in the space of the transform: the
    // outline is moved into that space when it stays a rectangle or an ellipse there. A rectangle or an ellipse of no size has
    // only an outline, which Skia strokes.
    private static FallbackReason? TryGetGeometry(DrawGeometryCommand command, Matrix transform, bool aliased, List<CanvasShape> shapes)
    {
        var geometry = command.Geometry;
        var geometryTransform = Matrix.Identity;
        while (geometry is StrideTransformedGeometry transformed)
        {
            if (transformed.Transform.ContainsPerspective())
                return FallbackReason.Perspective;
            geometryTransform = transformed.Transform * geometryTransform;
            geometry = transformed.Source;
        }

        switch (geometry)
        {
            case StrideRectangleGeometry rectangle:
            {
                if (!TryMove(rectangle.Rect, geometryTransform, transform, out var rect, out var frame))
                    return FallbackReason.Path;
                if (rect.Width <= 0 || rect.Height <= 0)
                    return IsVisible(command.Pen) ? FallbackReason.Path : null;
                return TryGetShape(command.Brush, command.Pen, new RoundedRect(rect), hasSharpCorners: true, frame, transform, strokedAsPath: true, aliased, shapes);
            }
            case StrideEllipseGeometry ellipse:
                return TryMove(ellipse.Rect, geometryTransform, transform, out var ellipseRect, out var ellipseFrame)
                    ? TryGetEllipse(command.Brush, command.Pen, ellipseRect, ellipseFrame, transform, aliased, shapes, isGeometry: true)
                    : FallbackReason.Path;
            case StrideLineGeometry line:
                return TryGetLine(command.Pen, geometryTransform.Transform(line.P1), geometryTransform.Transform(line.P2), transform, aliased, shapes);
            default:
                return FallbackReason.Path;
        }
    }

    // A rectangle moved by a transform that keeps it a rectangle: one that keeps the axes, or a rotation with a uniform scale,
    // which goes into the frame the rectangle is drawn in
    private static bool TryMove(Rect rect, Matrix geometryTransform, Matrix transform, out Rect moved, out Matrix frame)
    {
        var g = geometryTransform;
        if (g.M12 == 0 && g.M21 == 0)
        {
            moved = rect.TransformToAABB(g);
            frame = transform;
            return true;
        }

        if (GetUniformScale(g) is { } scale)
        {
            moved = new Rect(rect.X * scale, rect.Y * scale, rect.Width * scale, rect.Height * scale).Normalize();
            frame = new Matrix(g.M11 / scale, g.M12 / scale, g.M21 / scale, g.M22 / scale, g.M31, g.M32) * transform;
            return true;
        }

        moved = default;
        frame = default;
        return false;
    }

    // A fill, then the ring of the stroke around the outline
    private static FallbackReason? TryGetShape(IBrush? brush, IPen? pen, RoundedRect outline, bool hasSharpCorners, Matrix frame, Matrix transform, bool strokedAsPath, bool aliased, List<CanvasShape> shapes)
    {
        if (brush != null && brush is not ISolidColorBrush)
            return FallbackReason.Brush;
        Stroke? stroke = null;
        if (IsVisible(pen))
        {
            if (CheckPen(pen, hasSharpCorners) is { } reason)
                return reason;
            stroke = GetStroke(pen, transform, strokedAsPath, aliased);
            if (stroke == null)
                return FallbackReason.Stroke;
        }

        if (brush is ISolidColorBrush fill && GetColor(fill) is { A: > 0 } fillColor)
            shapes.Add(new CanvasShape(frame, outline, null, fillColor, aliased));
        if (stroke is { } ring && ring.Color(GetColor((ISolidColorBrush)pen!.Brush!)) is { A: > 0 } strokeColor)
        {
            var (outer, inner) = GetRing(outline, ring.Half);
            shapes.Add(new CanvasShape(frame, outer, inner, strokeColor, aliased));
        }
        return null;
    }

    // Skia draws the anti-aliased stroke of a path that is no wider than a pixel on either axis as a hairline: a pixel wide,
    // its alpha scaled by the stroke's width (SkDrawTreatAsHairline). A hairline needs a transform with a uniform scale.
    private static Stroke? GetStroke(IPen pen, Matrix transform, bool strokedAsPath, bool aliased)
    {
        var width = (float)pen.Thickness;
        var across = FastLength(width * (float)transform.M11, width * (float)transform.M12);
        var along = FastLength(width * (float)transform.M21, width * (float)transform.M22);
        if (!strokedAsPath || aliased || across > 1 || along > 1)
            return new Stroke(pen.Thickness / 2, 256, false);
        if (GetUniformScale(transform) is not { } scale)
            return null;
        var coverage = (across + along) / 2;
        return new Stroke(0.5 / scale, coverage == 1 ? 256 : (int)(coverage * 256), true);

        // Skia's estimate of a length
        static float FastLength(float x, float y)
        {
            x = Math.Abs(x);
            y = Math.Abs(y);
            return x < y ? y + x / 2 : x + y / 2;
        }
    }

    // The scale of a transform that scales both axes the same, rotated or mirrored, or null
    private static double? GetUniformScale(Matrix transform)
    {
        var scale = Math.Sqrt(Math.Abs(transform.M11 * transform.M22 - transform.M12 * transform.M21));
        var tolerance = 1e-9 * scale;
        var rotation = Math.Abs(transform.M11 - transform.M22) <= tolerance && Math.Abs(transform.M12 + transform.M21) <= tolerance;
        var mirror = Math.Abs(transform.M11 + transform.M22) <= tolerance && Math.Abs(transform.M12 - transform.M21) <= tolerance;
        return scale > 0 && (rotation || mirror) ? scale : null;
    }

    // Skia's rule: a pen draws when it has a brush and a thickness other than 0
    private static bool IsVisible([NotNullWhen(true)] IPen? pen) => pen is { Brush: not null } && pen.Thickness != 0;

    private static FallbackReason? CheckPen(IPen pen, bool hasSharpCorners)
    {
        if (pen.Brush is not ISolidColorBrush)
            return FallbackReason.Brush;
        if (pen.DashStyle?.Dashes is { Count: > 0 } || !(pen.Thickness > 0))
            return FallbackReason.Stroke;
        if (hasSharpCorners && (pen.LineJoin != PenLineJoin.Miter || pen.MiterLimit < RightAngleMiter))
            return FallbackReason.Stroke;
        return null;
    }

    // Skia's paint takes the alpha times the opacity, cut to a byte
    private static Color GetColor(ISolidColorBrush brush)
    {
        var color = brush.Color;
        return new Color((byte)(color.A * brush.Opacity), color.R, color.G, color.B);
    }

    // The outline grown by half the thickness, its radii too where they aren't 0; and shrunk by it, its radii never below 0
    private static (RoundedRect Outer, RoundedRect? Inner) GetRing(RoundedRect outline, double half)
    {
        var outer = new RoundedRect(outline.Rect.Inflate(half),
            Grow(outline.RadiiTopLeft, half), Grow(outline.RadiiTopRight, half), Grow(outline.RadiiBottomRight, half), Grow(outline.RadiiBottomLeft, half));
        var innerRect = outline.Rect.Deflate(half);
        if (outline.Rect.Width <= 2 * half || outline.Rect.Height <= 2 * half)
            return (outer, null);
        var inner = Normalize(new RoundedRect(innerRect,
            Grow(outline.RadiiTopLeft, -half), Grow(outline.RadiiTopRight, -half), Grow(outline.RadiiBottomRight, -half), Grow(outline.RadiiBottomLeft, -half)));
        return (outer, inner);

        static Vector Grow(Vector radii, double by) => IsSharp(radii) ? default : new Vector(Math.Max(0, radii.X + by), Math.Max(0, radii.Y + by));
    }

    private static bool IsSharp(Vector radii) => radii.X <= 0 || radii.Y <= 0;

    /// <summary>
    /// Gets a rounded rectangle with the radii Skia draws it with: a corner with a radius of 0 or less on either axis is
    /// sharp, and radii that don't fit their sides are all scaled down by one factor.
    /// </summary>
    internal static RoundedRect Normalize(RoundedRect rounded)
    {
        var rect = rounded.Rect;
        Span<Vector> radii = [rounded.RadiiTopLeft, rounded.RadiiTopRight, rounded.RadiiBottomRight, rounded.RadiiBottomLeft];
        for (var i = 0; i < radii.Length; i++)
        {
            if (IsSharp(radii[i]) || !double.IsFinite(radii[i].X) || !double.IsFinite(radii[i].Y))
                radii[i] = default;
        }

        var scale = 1.0;
        scale = GetScale(radii[0].X, radii[1].X, rect.Width, scale);
        scale = GetScale(radii[1].Y, radii[2].Y, rect.Height, scale);
        scale = GetScale(radii[2].X, radii[3].X, rect.Width, scale);
        scale = GetScale(radii[3].Y, radii[0].Y, rect.Height, scale);
        if (scale < 1)
        {
            for (var i = 0; i < radii.Length; i++)
                radii[i] *= scale;
        }
        return new RoundedRect(rect, radii[0], radii[1], radii[2], radii[3]);

        static double GetScale(double first, double second, double side, double scale)
            => first + second > side ? Math.Min(scale, side / (first + second)) : scale;
    }

    // The half thickness of a stroke in the space of the transform, and the scale of its alpha in 256ths
    private readonly record struct Stroke(double Half, int AlphaScale, bool IsHairline)
    {
        public Color Color(Color color) => AlphaScale == 256 ? color : new Color((byte)(color.A * AlphaScale >> 8), color.R, color.G, color.B);
    }
}
