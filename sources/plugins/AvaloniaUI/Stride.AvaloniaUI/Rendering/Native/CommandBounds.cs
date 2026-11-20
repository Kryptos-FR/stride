// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using Stride.AvaloniaUI.Rendering.Drawing;
using Stride.AvaloniaUI.Rendering.Fallback;
using Stride.AvaloniaUI.Rendering.Geometry;

namespace Stride.AvaloniaUI.Rendering.Native;

/// <summary>
/// The pixels the calls of a fallback run can touch, so the run rasterizes only those.
/// </summary>
internal static class CommandBounds
{
    /// <summary>
    /// Gets the pixel bounds of what a command can touch, drawn with a transform.
    /// </summary>
    /// <returns>
    /// The bounds; an empty rectangle for a command that draws nothing by itself (a push, a pop, a transform); <c>null</c> when
    /// they are unknown, and the run then takes its whole clip.
    /// </returns>
    public static Rect? Get(DrawCommand command, Matrix transform) => command switch
    {
        DrawRectangleCommand rectangle => GetRectangle(rectangle).TransformToAABB(transform),
        DrawEllipseCommand ellipse => ellipse.Rect.Inflate(GetThickness(ellipse.Pen) / 2).TransformToAABB(transform),
        DrawLineCommand line => GetLine(line).TransformToAABB(transform),
        DrawGeometryCommand geometry => GetGeometry(geometry, transform),
        DrawGlyphRunCommand glyphRun => glyphRun.GlyphRun.Bounds.TransformToAABB(transform).Inflate(2),
        DrawBitmapCommand bitmap => bitmap.DestRect.TransformToAABB(transform),
        DrawBitmapWithOpacityMaskCommand bitmap => bitmap.DestRect.TransformToAABB(transform),
        DrawAcrylicRectangleCommand acrylic => acrylic.Rect.Rect.TransformToAABB(transform),
        SetTransformCommand => default(Rect),
        _ when IsPush(command.Kind) || command.Kind == DrawCallKind.Pop => default(Rect),
        _ => null,
    };

    /// <summary>
    /// Gets the pixel bounds of what a list of commands can touch, drawn from a transform; the list's own transforms and
    /// groups count.
    /// </summary>
    public static Rect? Get(IEnumerable<DrawCommand> commands, Matrix transform)
    {
        var bounds = new BoundsAccumulator();
        foreach (var command in commands)
        {
            if (command is SetTransformCommand setTransform)
                transform = setTransform.Transform;
            bounds.Add(command, transform);
        }
        return bounds.Result;
    }

    /// <summary>
    /// Gets whether a kind of call opens a group, which a <see cref="DrawCallKind.Pop"/> closes.
    /// </summary>
    public static bool IsPush(DrawCallKind kind) => kind is DrawCallKind.PushClip or DrawCallKind.PushRoundedClip or DrawCallKind.PushLayer
        or DrawCallKind.PushOpacity or DrawCallKind.PushOpacityMask or DrawCallKind.PushGeometryClip or DrawCallKind.PushRenderOptions
        or DrawCallKind.PushTextOptions or DrawCallKind.PushEffect;

    /// <summary>
    /// Gets the pixel bounds a group cuts its contents to, or <c>null</c> when it doesn't cut them.
    /// </summary>
    public static Rect? GetGroupCut(DrawCommand push, Matrix transform) => push switch
    {
        PushClipCommand clip => clip.Clip.TransformToAABB(transform),
        PushRoundedClipCommand clip => clip.Clip.Rect.TransformToAABB(transform),
        PushGeometryClipCommand { Clip: StrideGeometry { Path: { } path } } => path.Bounds.TransformToAABB(transform),
        PushLayerCommand layer => layer.Bounds.TransformToAABB(transform),
        // Skia takes the bounds of an opacity group as a hint, and those of an opacity mask don't hold the mask's brush
        _ => null,
    };

    // The rectangle with its stroke and its outer shadows; the inset shadows are inside it
    private static Rect GetRectangle(DrawRectangleCommand command)
    {
        var bounds = command.Rect.Rect.Inflate(GetThickness(command.Pen) / 2);
        foreach (var shadow in command.BoxShadows)
        {
            if (shadow.IsInset)
                continue;
            var shadowBounds = command.Rect.Rect.Translate(new Vector(shadow.OffsetX, shadow.OffsetY)).Inflate(shadow.Spread + 3 * shadow.Blur);
            bounds = Union(bounds, shadowBounds);
        }
        return bounds;
    }

    // The thickness covers the caps, square ones included
    private static Rect GetLine(DrawLineCommand command)
    {
        var left = Math.Min(command.P1.X, command.P2.X);
        var top = Math.Min(command.P1.Y, command.P2.Y);
        var right = Math.Max(command.P1.X, command.P2.X);
        var bottom = Math.Max(command.P1.Y, command.P2.Y);
        return new Rect(left, top, right - left, bottom - top).Inflate(GetThickness(command.Pen));
    }

    // Miter joins reach as far as the miter limit times the thickness; other joins and the caps, the thickness
    private static Rect? GetGeometry(DrawGeometryCommand command, Matrix transform)
    {
        if (command.Geometry is not StrideGeometry { Path: { } path })
            return null;
        var inflation = command.Pen is { } pen
            ? pen.LineJoin == PenLineJoin.Miter ? pen.Thickness * Math.Max(pen.MiterLimit, 1) : pen.Thickness
            : 0;
        return path.Bounds.Inflate(inflation).TransformToAABB(transform);
    }

    private static double GetThickness(IPen? pen) => pen?.Thickness ?? 0;

    // The union of two rectangles, either of which may be empty
    internal static Rect Union(Rect a, Rect b) => a.Width <= 0 || a.Height <= 0 ? b : b.Width <= 0 || b.Height <= 0 ? a : a.Union(b);
}

/// <summary>
/// The bounds of commands added one by one with the transform of each, through their groups.
/// </summary>
internal sealed class BoundsAccumulator
{
    private readonly List<Group> groups = [new Group(null, false)];

    /// <summary>
    /// Gets the bounds of the commands added so far, the open groups closed: <c>null</c> when unknown, empty when nothing is
    /// touched.
    /// </summary>
    public Rect? Result
    {
        get
        {
            var result = groups[^1].Bounds;
            for (var i = groups.Count - 2; i >= 0; i--)
                result = groups[i].With(result).Bounds;
            return result;
        }
    }

    public void Add(DrawCommand command, Matrix transform)
    {
        if (CommandBounds.IsPush(command.Kind))
        {
            groups.Add(new Group(CommandBounds.GetGroupCut(command, transform), command.Kind == DrawCallKind.PushEffect));
        }
        else if (command.Kind == DrawCallKind.Pop)
        {
            // An unbalanced pop closes nothing
            if (groups.Count > 1)
            {
                var closed = groups[^1].Bounds;
                groups.RemoveAt(groups.Count - 1);
                groups[^1] = groups[^1].With(closed);
            }
        }
        else
        {
            groups[^1] = groups[^1].With(CommandBounds.Get(command, transform));
        }
    }

    public void Clear()
    {
        groups.Clear();
        groups.Add(new Group(null, false));
    }

    // A group's contents so far: their union, or unknown; an effect makes the whole group unknown
    private readonly record struct Group(Rect? Cut, bool IsEffect, Rect Union = default, bool IsUnknown = false)
    {
        public Rect? Bounds => IsEffect ? Cut : IsUnknown ? Cut : Cut is { } cut ? Intersect(Union, cut) : Union;

        public Group With(Rect? bounds) => bounds is { } rect ? this with { Union = CommandBounds.Union(Union, rect) } : this with { IsUnknown = true };

        private static Rect Intersect(Rect a, Rect b) => a.Width <= 0 || a.Height <= 0 ? default : a.Intersect(b);
    }
}
