// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Stride.AvaloniaUI.Rendering.Bitmaps;
using Stride.AvaloniaUI.Rendering.Drawing;
using Stride.AvaloniaUI.Rendering.Fallback;
using Stride.Graphics;
using Color = Avalonia.Media.Color;
using Color4 = Stride.Core.Mathematics.Color4;
using Rectangle = Stride.Core.Mathematics.Rectangle;

namespace Stride.AvaloniaUI.Rendering.Native;

/// <summary>
/// The drawing context of the native canvas on a graphics device: it draws what it can itself into the frame's texture, and
/// hands the rest to the fallback in runs drawn in place.
/// </summary>
/// <remarks>
/// <para>
/// The context keeps the transform, the clips, and the text and render options. A clip whose transform keeps the axes is a
/// scissor when it is a rectangle, its edges rounded to pixels as Skia rounds a rectangle clip, or a rounded rectangle
/// without radii whose pixel edges are whole. Any other clip is drawn by the shader: a rounded rectangle in its own frame,
/// with aliased edges for a rectangle as Skia has them, whose coverage multiplies every quad's while the scissor is cut to
/// its pixels. There is one clip of the shader at a time. A clip pushed while there is one, or under a transform with
/// perspective, and every other push, opens a group: the push, everything up to its matching pop, and the pop go to the
/// current fallback run as they are.
/// </para>
/// <para>
/// <see cref="Clear"/> and the shapes of <see cref="NativeShapes"/> are drawn natively. Every other call goes to the current
/// fallback run, and is counted by kind; outside groups, each such call and each group is also counted by the reason it
/// went to the fallback. A run is drawn under the scissor and the clip of the shader it started with, so neither is in its
/// own calls. It ends at a native draw, at a change of the scissor or of the clip of the shader, at <see cref="GetFeature"/>
/// and at <see cref="Dispose"/>. When the context is disposed, the runs are uploaded and the frame is drawn into the
/// texture, in order.
/// </para>
/// </remarks>
internal sealed unsafe class NativeDrawingContext : IDrawingContextImpl, IDrawingContextImplWithEffects, IDrawingContextWithAcrylicLikeSupport
{
    // How far from a whole pixel the edge of a rounded rectangle clip without radii may be and still make a scissor
    private const double PixelTolerance = 1e-4;

    private readonly NativeGraphics graphics;
    private readonly FallbackChannel channel;
    private readonly Texture? target;
    private readonly Vector dpi;
    private readonly Action? ended;
    private readonly List<FrameItem> items = [];
    private readonly List<CanvasShape> shapes = [];
    private readonly Stack<(Rectangle Scissor, CanvasClip? Clip)> clips = new();
    private readonly Stack<RenderOptions> renderOptionsStack = new();
    private readonly Stack<TextOptions> textOptionsStack = new();
    private Matrix transform = Matrix.Identity;
    private Rectangle scissor;
    private CanvasClip? shaderClip;
    private CanvasClip? runClip;
    private RenderOptions renderOptions;
    private TextOptions textOptions;
    private int groupDepth;
    private FallbackRun? run;
    private bool endRunAtNextCall;
    private bool isDisposed;

    /// <param name="graphics">What the canvas keeps on the device; the frame is drawn in its <see cref="NativeGraphics.Context"/>.</param>
    /// <param name="channel">The channel to the fallback, which counts the calls of the runs.</param>
    /// <param name="target">The texture of the frame, or <c>null</c> for a frame of no pixels, whose calls are dropped.</param>
    /// <param name="dpi">The DPI of the frame, which the runs and the layers take.</param>
    /// <param name="ended">Called once the frame is drawn, even when a part of it failed.</param>
    public NativeDrawingContext(NativeGraphics graphics, FallbackChannel channel, Texture? target, Vector dpi, Action? ended)
    {
        this.graphics = graphics;
        this.channel = channel;
        this.target = target;
        this.dpi = dpi;
        this.ended = ended;
        if (target != null)
            scissor = new Rectangle(0, 0, target.Width, target.Height);
    }

    public Matrix Transform
    {
        get => transform;
        set
        {
            Next();
            if (transform == value)
                return;
            transform = value;
            if (target == null)
                return;
            if (groupDepth > 0)
                Record(new SetTransformCommand(value));
            else
                run?.AddState(new SetTransformCommand(value));
        }
    }

    /// <summary>
    /// Replaces the pixels of the scissor with a color; inside a group, the fallback clears.
    /// </summary>
    public void Clear(Color color)
    {
        Next();
        if (target == null)
            return;
        if (groupDepth > 0)
        {
            Record(new ClearCommand(color));
            return;
        }

        EndRun();
        if (scissor.Width > 0 && scissor.Height > 0)
            items.Add(new FrameItem(scissor, Premultiply(color), null, null, shaderClip));
    }

    public void DrawBitmap(IBitmapImpl source, double opacity, Rect sourceRect, Rect destRect)
        => Draw(new DrawBitmapCommand(source, opacity, sourceRect, destRect));

    public void DrawBitmap(IBitmapImpl source, IBrush opacityMask, Rect opacityMaskRect, Rect destRect)
        => Draw(new DrawBitmapWithOpacityMaskCommand(source, opacityMask, opacityMaskRect, destRect));

    public void DrawLine(IPen? pen, Point p1, Point p2) => Draw(new DrawLineCommand(pen, p1, p2));

    public void DrawGeometry(IBrush? brush, IPen? pen, IGeometryImpl geometry) => Draw(new DrawGeometryCommand(brush, pen, geometry));

    public void DrawRectangle(IBrush? brush, IPen? pen, RoundedRect rect, BoxShadows boxShadows = default)
        => Draw(new DrawRectangleCommand(brush, pen, rect, boxShadows));

    public void DrawRectangle(IExperimentalAcrylicMaterial material, RoundedRect rect) => Draw(new DrawAcrylicRectangleCommand(material, rect));

    public void DrawRegion(IBrush? brush, IPen? pen, IPlatformRenderInterfaceRegion region)
        => throw new NotSupportedException("The native renderer of the Avalonia UI has no regions.");

    public void DrawEllipse(IBrush? brush, IPen? pen, Rect rect) => Draw(new DrawEllipseCommand(brush, pen, rect));

    public void DrawGlyphRun(IBrush? foreground, IGlyphRunImpl glyphRun) => Draw(new DrawGlyphRunCommand(foreground, glyphRun));

    // A layer of the CPU, which the fallback draws as a bitmap
    public IDrawingContextLayerImpl CreateLayer(PixelSize size) => new StrideRenderTargetBitmap(channel, size, dpi);

    public void PushClip(Rect clip)
    {
        Next();
        if (target == null)
            return;
        if (groupDepth > 0)
            PushGroup(new PushClipCommand(clip));
        else if (TryGetScissor(clip, roundEdges: true, out var pixels))
            PushClipState(Rectangle.Intersect(scissor, pixels), shaderClip);
        else
            PushShaderClip(new RoundedRect(clip), aliased: true, new PushClipCommand(clip));
    }

    public void PushClip(RoundedRect clip)
    {
        Next();
        if (target == null)
            return;
        if (groupDepth > 0)
            PushGroup(new PushRoundedClipCommand(clip));
        else if (!clip.IsRounded && TryGetScissor(clip.Rect, roundEdges: false, out var pixels))
            PushClipState(Rectangle.Intersect(scissor, pixels), shaderClip);
        else
            PushShaderClip(NativeShapes.Normalize(clip), aliased: false, new PushRoundedClipCommand(clip));
    }

    public void PushClip(IPlatformRenderInterfaceRegion region)
        => throw new NotSupportedException("The native renderer of the Avalonia UI has no regions.");

    public void PopClip()
    {
        Next();
        if (target == null)
            return;
        if (groupDepth > 0)
            PopGroup(new PopClipCommand());
        else if (clips.Count > 0)
            SetClipState(clips.Pop());
    }

    public void PushLayer(Rect bounds) => Push(new PushLayerCommand(bounds));

    public void PopLayer() => Pop(new PopLayerCommand());

    public void PushOpacity(double opacity, Rect? bounds) => Push(new PushOpacityCommand(opacity, bounds));

    public void PopOpacity() => Pop(new PopOpacityCommand());

    public void PushOpacityMask(IBrush mask, Rect bounds) => Push(new PushOpacityMaskCommand(mask, bounds));

    public void PopOpacityMask() => Pop(new PopOpacityMaskCommand());

    public void PushGeometryClip(IGeometryImpl clip) => Push(new PushGeometryClipCommand(clip));

    public void PopGeometryClip() => Pop(new PopGeometryClipCommand());

    public void PushEffect(Rect? clipRect, IEffect effect) => Push(new PushEffectCommand(clipRect, effect));

    public void PopEffect() => Pop(new PopEffectCommand());

    public void PushRenderOptions(RenderOptions options)
    {
        Next();
        if (target == null)
            return;
        if (groupDepth > 0)
        {
            PushGroup(new PushRenderOptionsCommand(options));
            return;
        }

        renderOptionsStack.Push(renderOptions);
        SetRenderOptions(renderOptions.MergeWith(options));
    }

    public void PopRenderOptions()
    {
        Next();
        if (target == null)
            return;
        if (groupDepth > 0)
            PopGroup(new PopRenderOptionsCommand());
        else if (renderOptionsStack.Count > 0)
            SetRenderOptions(renderOptionsStack.Pop());
    }

    public void PushTextOptions(TextOptions options)
    {
        Next();
        if (target == null)
            return;
        if (groupDepth > 0)
        {
            PushGroup(new PushTextOptionsCommand(options));
            return;
        }

        textOptionsStack.Push(textOptions);
        SetTextOptions(textOptions.MergeWith(options));
    }

    public void PopTextOptions()
    {
        Next();
        if (target == null)
            return;
        if (groupDepth > 0)
            PopGroup(new PopTextOptionsCommand());
        else if (textOptionsStack.Count > 0)
            SetTextOptions(textOptionsStack.Pop());
    }

    /// <summary>
    /// Gets a feature of the fallback, such as a lease of its canvas: the calls so far are drawn first, and the caller draws
    /// into a run that covers the scissor and ends at the next call.
    /// </summary>
    /// <returns>The feature, or <c>null</c> when the fallback doesn't have it (and no run is made).</returns>
    public object? GetFeature(Type t)
    {
        Next();
        if (target == null)
            return null;
        channel.Count(DrawCallKind.Feature);

        // In a group, the group's run draws now, and goes on afterwards
        if (groupDepth > 0)
            return GetRunFeature(EnsureRun(), t);

        channel.Count(FallbackReason.CustomDrawing);
        EndRun();
        var feature = GetRunFeature(EnsureRun(), t);
        if (feature == null)
            DropRun();
        else
            endRunAtNextCall = true;
        return feature;
    }

    /// <summary>
    /// Ends the last run and draws the frame into the texture; then calls back, even when a part of the frame failed.
    /// </summary>
    public void Dispose()
    {
        if (isDisposed)
            return;
        isDisposed = true;

        try
        {
            EndRun();
            if (target != null && !graphics.IsDisposed)
                DrawFrame(target);
        }
        finally
        {
            DropRun();
            foreach (var item in items)
                item.Run?.Dispose();
            items.Clear();
            ended?.Invoke();
        }
    }

    // Outside groups, a call is drawn natively when it can be; a native call that draws nothing leaves the run open
    private void Draw(DrawCommand command)
    {
        Next();
        if (target == null)
            return;
        if (groupDepth > 0)
        {
            Record(command);
            return;
        }

        shapes.Clear();
        if (NativeShapes.TryGet(command, transform, renderOptions, shapes) is { } reason)
        {
            channel.Count(reason);
            Record(command);
            return;
        }
        if (shapes.Count == 0)
            return;

        EndRun();
        foreach (var shape in shapes)
            items.Add(new FrameItem(scissor, default, null, shape, shaderClip));
    }

    private void Push(DrawCommand push)
    {
        Next();
        if (target != null)
            PushGroup(push);
    }

    private void Pop(DrawCommand pop)
    {
        Next();
        if (target != null && groupDepth > 0)
            PopGroup(pop);
    }

    // A push that opens a group outside groups is counted by its reason, by default the push's own
    private void PushGroup(DrawCommand push, FallbackReason? reason = null)
    {
        if (groupDepth == 0)
            channel.Count(reason ?? GetReason(push));
        Record(push);
        groupDepth++;
    }

    private void PopGroup(DrawCommand pop)
    {
        Record(pop);
        groupDepth--;
    }

    // A call the fallback draws, counted, in the current run
    private void Record(DrawCommand command)
    {
        channel.Count(command.Kind);
        EnsureRun().Add(command, transform);
    }

    private FallbackRun EnsureRun()
    {
        if (run == null)
        {
            run = new FallbackRun(scissor, transform, renderOptions, textOptions);
            runClip = shaderClip;
        }
        return run;
    }

    // The run made for a feature ends at the next call
    private void Next()
    {
        if (endRunAtNextCall)
            EndRun();
    }

    // Rasterizes the current run; a run the fallback fails to draw is lost, not the frame
    private void EndRun()
    {
        endRunAtNextCall = false;
        if (run is not { } finished)
            return;
        run = null;

        try
        {
            finished.Rasterize(channel, dpi);
        }
        catch (Exception exception)
        {
            graphics.ReportFailure(exception);
            finished.Dispose();
            return;
        }

        if (finished.Pixels == IntPtr.Zero)
            finished.Dispose();
        else
            items.Add(new FrameItem(default, default, finished, null, runClip));
    }

    private void DropRun()
    {
        endRunAtNextCall = false;
        run?.Dispose();
        run = null;
    }

    private object? GetRunFeature(FallbackRun featureRun, Type featureType)
    {
        try
        {
            return featureRun.GetFeature(channel, dpi, featureType);
        }
        catch (Exception exception)
        {
            graphics.ReportFailure(exception);
            DropRun();
            return null;
        }
    }

    // A clip the scissor can't express: the shader draws it, unless it already draws one or the transform has perspective
    private void PushShaderClip(RoundedRect outline, bool aliased, DrawCommand push)
    {
        if (transform.ContainsPerspective())
        {
            PushGroup(push, FallbackReason.Perspective);
            return;
        }
        if (shaderClip != null)
        {
            PushGroup(push, FallbackReason.ClipDepth);
            return;
        }

        // A clip of no area covers no pixel
        var rect = outline.Rect;
        if (!(rect.Width > 0 && rect.Height > 0) || !transform.HasInverse)
        {
            PushClipState(new Rectangle(scissor.X, scissor.Y, 0, 0), shaderClip);
            return;
        }

        // A pixel the clip covers has its center inside the clip's bounds, or less than half a pixel out of them
        var bounds = rect.TransformToAABB(transform);
        var left = Math.Max(Math.Floor(bounds.Left), scissor.Left);
        var top = Math.Max(Math.Floor(bounds.Top), scissor.Top);
        var right = Math.Min(Math.Ceiling(bounds.Right), scissor.Right);
        var bottom = Math.Min(Math.Ceiling(bounds.Bottom), scissor.Bottom);
        var pixels = left < right && top < bottom
            ? new Rectangle((int)left, (int)top, (int)(right - left), (int)(bottom - top))
            : new Rectangle(scissor.X, scissor.Y, 0, 0);
        PushClipState(pixels, new CanvasClip(transform, outline, aliased));
    }

    private void PushClipState(Rectangle newScissor, CanvasClip? newClip)
    {
        clips.Push((scissor, shaderClip));
        SetClipState((newScissor, newClip));
    }

    private void SetClipState((Rectangle Scissor, CanvasClip? Clip) state)
    {
        if (state.Scissor == scissor && state.Clip == shaderClip)
            return;
        EndRun();
        (scissor, shaderClip) = state;
    }

    // The run's options are always the merged ones, so the fallback's stack holds one entry of each
    private void SetRenderOptions(RenderOptions value)
    {
        renderOptions = value;
        if (run == null)
            return;
        run.AddState(new PopRenderOptionsCommand());
        run.AddState(new PushRenderOptionsCommand(value));
    }

    private void SetTextOptions(TextOptions value)
    {
        textOptions = value;
        if (run == null)
            return;
        run.AddState(new PopTextOptionsCommand());
        run.AddState(new PushTextOptionsCommand(value));
    }

    // A clip is a scissor when the transform keeps the axes, and its pixel edges are whole or are to be rounded; Skia
    // rounds a pixel edge half up, so that a pixel is in a rectangle clip when its center is past the left or top edge and
    // not past the right or bottom one
    private bool TryGetScissor(Rect clip, bool roundEdges, out Rectangle pixels)
    {
        pixels = default;
        var m = transform;
        if (!(m.M12 == 0 && m.M21 == 0 || m.M11 == 0 && m.M22 == 0) || m.ContainsPerspective())
            return false;

        var rect = clip.TransformToAABB(m);
        if (!TryRound(rect.Left, roundEdges, out var left) || !TryRound(rect.Top, roundEdges, out var top)
            || !TryRound(rect.Right, roundEdges, out var right) || !TryRound(rect.Bottom, roundEdges, out var bottom))
            return false;
        pixels = new Rectangle(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
        return true;

        static bool TryRound(double value, bool roundEdge, out int pixel)
        {
            // Beyond any target, a clip edge only needs to stay there
            var rounded = Math.Floor(Math.Clamp(value, -1e8, 1e8) + 0.5);
            pixel = (int)rounded;
            return roundEdge || Math.Abs(value - rounded) <= PixelTolerance || Math.Abs(value) >= 1e8;
        }
    }

    private void DrawFrame(Texture frame)
    {
        if (graphics.Context is not { } context)
            return;

        var commandList = context.CommandList;
        var batch = new CanvasBatch();
        var rented = new List<Texture>();
        try
        {
            foreach (var item in items)
            {
                if (item.Shape is { } shape)
                {
                    batch.AddShape(shape, item.Rectangle, item.Clip);
                    continue;
                }
                if (item.Run is not { } fallbackRun)
                {
                    batch.AddFill(item.Rectangle, item.Color, item.Clip);
                    continue;
                }

                var bounds = fallbackRun.Bounds;
                var texture = graphics.RentTexture(bounds.Width, bounds.Height);
                rented.Add(texture);
                var pixels = new ReadOnlySpan<byte>((void*)fallbackRun.Pixels, bounds.Width * bounds.Height * 4);
                texture.SetData(commandList, pixels, 0, 0, new ResourceRegion(0, 0, 0, bounds.Width, bounds.Height, 1));
                commandList.ResourceBarrierTransition(texture, BarrierLayout.ShaderResource);
                batch.AddImage(bounds, texture, fallbackRun.Scissor, item.Clip);
            }
            batch.Draw(graphics, frame);
        }
        finally
        {
            graphics.ReturnTextures(rented);
        }
    }

    private static Color4 Premultiply(Color color)
    {
        var alpha = color.A / 255f;
        return new Color4(color.R / 255f * alpha, color.G / 255f * alpha, color.B / 255f * alpha, alpha);
    }

    // Why a push that always opens a group goes to the fallback
    private static FallbackReason GetReason(DrawCommand push) => push switch
    {
        PushOpacityCommand => FallbackReason.OpacityGroup,
        PushOpacityMaskCommand => FallbackReason.OpacityMask,
        PushGeometryClipCommand => FallbackReason.GeometryClip,
        PushLayerCommand => FallbackReason.Layer,
        PushEffectCommand => FallbackReason.Effect,
        _ => throw new ArgumentOutOfRangeException(nameof(push), push, "Not a push that always opens a group."),
    };

    // A rectangle replaced with a color, a shape under the scissor of its rectangle, or a run drawn in place; each under
    // the clip of the shader it was drawn with
    private readonly record struct FrameItem(Rectangle Rectangle, Color4 Color, FallbackRun? Run, CanvasShape? Shape, CanvasClip? Clip);
}
