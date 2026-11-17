// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Stride.Core.Mathematics;
using Stride.Graphics;
using AvaloniaRoundedRect = Avalonia.RoundedRect;

namespace Stride.AvaloniaUI.Rendering.Native;

/// <summary>
/// A quad of the canvas, one instance of the canvas shader's draws.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct CanvasInstance
{
    /// <summary>
    /// The size of an instance in the buffer, in bytes.
    /// </summary>
    public const int Size = 11 * 16;

    /// <summary>
    /// The kind of a quad filled with its color.
    /// </summary>
    public const float ColorKind = 0;

    /// <summary>
    /// The kind of a quad that samples a texture.
    /// </summary>
    public const float TextureKind = 1;

    /// <summary>
    /// The kind of a quad that draws a shape.
    /// </summary>
    public const float ShapeKind = 2;

    /// <summary>
    /// The per-instance streams of the canvas shader, all in the buffer of slot 0.
    /// </summary>
    public static readonly InputElementDescription[] InputElements =
    [
        Element("QUAD_RECT", 0),
        Element("QUAD_COLOR", 16),
        Element("QUAD_KIND", 32),
        Element("SHAPE_X", 48),
        Element("SHAPE_Y", 64),
        Element("OUTER_RECT", 80),
        Element("OUTER_RADII_TOP", 96),
        Element("OUTER_RADII_BOTTOM", 112),
        Element("INNER_RECT", 128),
        Element("INNER_RADII_TOP", 144),
        Element("INNER_RADII_BOTTOM", 160),
    ];

    /// <summary>
    /// The quad's left, top, right and bottom edges, in pixels of the target.
    /// </summary>
    public Vector4 Rect;

    /// <summary>
    /// The quad's premultiplied color, which multiplies the texture's or the shape's coverage.
    /// </summary>
    public Vector4 Color;

    /// <summary>
    /// The texel of the texture at the quad's top left corner (x, y), the kind of the quad (z), and 1 in w for a shape
    /// with aliased edges.
    /// </summary>
    public Vector4 Kind;

    /// <summary>
    /// The first row of the transform from the pixels of the target to the shape's frame: x = X.x * px + X.y * py + X.z.
    /// </summary>
    public Vector4 ShapeX;

    /// <summary>
    /// The second row of the transform from the pixels of the target to the shape's frame.
    /// </summary>
    public Vector4 ShapeY;

    /// <summary>
    /// The outline of the shape in its frame: left, top, right and bottom.
    /// </summary>
    public Vector4 OuterRect;

    /// <summary>
    /// The radii of the outline's top left (x, y) and top right (z, w) corners.
    /// </summary>
    public Vector4 OuterRadiiTop;

    /// <summary>
    /// The radii of the outline's bottom right (x, y) and bottom left (z, w) corners.
    /// </summary>
    public Vector4 OuterRadiiBottom;

    /// <summary>
    /// The hole of a stroke in the shape's frame, or an empty rectangle for a filled shape.
    /// </summary>
    public Vector4 InnerRect;

    /// <summary>
    /// The radii of the hole's top left (x, y) and top right (z, w) corners.
    /// </summary>
    public Vector4 InnerRadiiTop;

    /// <summary>
    /// The radii of the hole's bottom right (x, y) and bottom left (z, w) corners.
    /// </summary>
    public Vector4 InnerRadiiBottom;

    private static InputElementDescription Element(string semantic, int offset) => new()
    {
        SemanticName = semantic,
        Format = PixelFormat.R32G32B32A32_Float,
        InputSlot = 0,
        AlignedByteOffset = offset,
        InputSlotClass = InputClassification.Instance,
        InstanceDataStepRate = 1,
    };
}

/// <summary>
/// Consecutive instances drawn with one scissor, one texture and one blending.
/// </summary>
/// <param name="Texture">The texture the instances sample, or <c>null</c> for none.</param>
/// <param name="Opaque">Whether the instances replace the target's pixels rather than blend over them.</param>
internal readonly record struct CanvasSegment(int Start, int Count, Rectangle Scissor, Texture? Texture, bool Opaque);

/// <summary>
/// The quads of a frame of the canvas, in the order they are drawn.
/// </summary>
internal sealed class CanvasBatch
{
    private readonly List<CanvasInstance> instances = [];
    private readonly List<CanvasSegment> segments = [];

    /// <summary>
    /// Adds a quad of a premultiplied color that replaces the pixels of a rectangle.
    /// </summary>
    public void AddFill(Rectangle rectangle, Color4 premultipliedColor)
        => Add(new CanvasInstance { Rect = Edges(rectangle), Color = premultipliedColor.ToVector4(), Kind = new Vector4(0, 0, CanvasInstance.ColorKind, 0) }, rectangle, null, opaque: true);

    /// <summary>
    /// Adds a quad that draws a texture over a rectangle of the target, one texel to one pixel from the texture's top left
    /// corner, blended over the target with premultiplied alpha, under a scissor.
    /// </summary>
    public void AddImage(Rectangle rectangle, Texture texture, Rectangle scissor)
        => Add(new CanvasInstance { Rect = Edges(rectangle), Color = Vector4.One, Kind = new Vector4(0, 0, CanvasInstance.TextureKind, 0) }, scissor, texture, opaque: false);

    /// <summary>
    /// Adds a shape blended over the target with premultiplied alpha, under a scissor; a shape whose frame is flat draws nothing.
    /// </summary>
    public void AddShape(in CanvasShape shape, Rectangle scissor)
    {
        if (!shape.Frame.TryInvert(out var inverse))
            return;

        // The quad holds every pixel the anti-aliased outline touches
        var bounds = shape.Outer.Rect.TransformToAABB(shape.Frame);
        var left = Math.Max(Math.Floor(bounds.Left) - 1, scissor.Left);
        var top = Math.Max(Math.Floor(bounds.Top) - 1, scissor.Top);
        var right = Math.Min(Math.Ceiling(bounds.Right) + 1, scissor.Right);
        var bottom = Math.Min(Math.Ceiling(bounds.Bottom) + 1, scissor.Bottom);
        if (!(left < right && top < bottom))
            return;

        // The shape's frame is moved to the outline's center, which keeps the precision of large shapes
        var center = shape.Outer.Rect.Center;
        var color = shape.Color;
        var alpha = color.A / 255f;
        Add(new CanvasInstance
        {
            Rect = new Vector4((float)left, (float)top, (float)right, (float)bottom),
            Color = new Vector4(color.R / 255f * alpha, color.G / 255f * alpha, color.B / 255f * alpha, alpha),
            Kind = new Vector4(0, 0, CanvasInstance.ShapeKind, shape.Aliased ? 1 : 0),
            ShapeX = new Vector4((float)inverse.M11, (float)inverse.M21, (float)(inverse.M31 - center.X), 0),
            ShapeY = new Vector4((float)inverse.M12, (float)inverse.M22, (float)(inverse.M32 - center.Y), 0),
            OuterRect = Edges(shape.Outer, center),
            OuterRadiiTop = RadiiTop(shape.Outer),
            OuterRadiiBottom = RadiiBottom(shape.Outer),
            InnerRect = shape.Inner is { } inner ? Edges(inner, center) : default,
            InnerRadiiTop = shape.Inner is { } innerTop ? RadiiTop(innerTop) : default,
            InnerRadiiBottom = shape.Inner is { } innerBottom ? RadiiBottom(innerBottom) : default,
        }, scissor, null, opaque: false);
    }

    /// <summary>
    /// Draws the quads into a texture, then forgets them.
    /// </summary>
    public void Draw(NativeGraphics graphics, Texture target)
    {
        graphics.Draw(target, CollectionsMarshal.AsSpan(instances), CollectionsMarshal.AsSpan(segments));
        instances.Clear();
        segments.Clear();
    }

    private void Add(in CanvasInstance instance, Rectangle scissor, Texture? texture, bool opaque)
    {
        instances.Add(instance);
        if (segments.Count > 0 && segments[^1] is var last && last.Scissor == scissor && last.Texture == texture && last.Opaque == opaque)
            segments[^1] = last with { Count = last.Count + 1 };
        else
            segments.Add(new CanvasSegment(instances.Count - 1, 1, scissor, texture, opaque));
    }

    private static Vector4 Edges(Rectangle rectangle) => new(rectangle.Left, rectangle.Top, rectangle.Right, rectangle.Bottom);

    private static Vector4 Edges(in AvaloniaRoundedRect rounded, Avalonia.Point center)
    {
        var rect = rounded.Rect;
        return new Vector4((float)(rect.Left - center.X), (float)(rect.Top - center.Y), (float)(rect.Right - center.X), (float)(rect.Bottom - center.Y));
    }

    private static Vector4 RadiiTop(in AvaloniaRoundedRect rounded)
        => new((float)rounded.RadiiTopLeft.X, (float)rounded.RadiiTopLeft.Y, (float)rounded.RadiiTopRight.X, (float)rounded.RadiiTopRight.Y);

    private static Vector4 RadiiBottom(in AvaloniaRoundedRect rounded)
        => new((float)rounded.RadiiBottomRight.X, (float)rounded.RadiiBottomRight.Y, (float)rounded.RadiiBottomLeft.X, (float)rounded.RadiiBottomLeft.Y);
}
