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
/// The context keeps the transform, the clips, and the text and render options. A rectangle clip whose transform keeps the
/// axes and whose pixel edges are whole is a scissor. Any other clip, and every other push, opens a group: the push,
/// everything up to its matching pop, and the pop go to the current fallback run as they are.
/// </para>
/// <para>
/// <see cref="Clear"/> is drawn natively. Every other call goes to the current fallback run, and is counted by kind; a run
/// ends at a native draw, at a change of the scissor, at <see cref="GetFeature"/> and at <see cref="Dispose"/>. When the
/// context is disposed, the runs are uploaded and the frame is drawn into the texture, in order.
/// </para>
/// </remarks>
internal sealed unsafe class NativeDrawingContext : IDrawingContextImpl, IDrawingContextImplWithEffects, IDrawingContextWithAcrylicLikeSupport
{
    // How far from a whole pixel a clip's edge may be and still make a scissor
    private const double PixelTolerance = 1e-4;

    private readonly NativeGraphics graphics;
    private readonly FallbackChannel channel;
    private readonly Texture? target;
    private readonly Vector dpi;
    private readonly Action? ended;
    private readonly List<FrameItem> items = [];
    private readonly Stack<Rectangle> scissors = new();
    private readonly Stack<RenderOptions> renderOptionsStack = new();
    private readonly Stack<TextOptions> textOptionsStack = new();
    private Matrix transform = Matrix.Identity;
    private Rectangle scissor;
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
            items.Add(new FrameItem(scissor, Premultiply(color), null));
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
        if (groupDepth == 0 && TryGetScissor(clip, out var pixels))
            PushScissor(pixels);
        else
            PushGroup(new PushClipCommand(clip));
    }

    public void PushClip(RoundedRect clip)
    {
        Next();
        if (target == null)
            return;
        if (groupDepth == 0 && !clip.IsRounded && TryGetScissor(clip.Rect, out var pixels))
            PushScissor(pixels);
        else
            PushGroup(new PushRoundedClipCommand(clip));
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
        else if (scissors.Count > 0)
            SetScissor(scissors.Pop());
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

    private void Draw(DrawCommand command)
    {
        Next();
        if (target != null)
            Record(command);
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

    private void PushGroup(DrawCommand push)
    {
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

    private FallbackRun EnsureRun() => run ??= new FallbackRun(scissor, transform, renderOptions, textOptions);

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
            items.Add(new FrameItem(default, default, finished));
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

    private void PushScissor(Rectangle pixels)
    {
        scissors.Push(scissor);
        SetScissor(Rectangle.Intersect(scissor, pixels));
    }

    private void SetScissor(Rectangle value)
    {
        if (value == scissor)
            return;
        EndRun();
        scissor = value;
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

    // A clip is a scissor when the transform keeps the axes and the clip's pixel edges are whole
    private bool TryGetScissor(Rect clip, out Rectangle pixels)
    {
        pixels = default;
        var m = transform;
        if (m.M12 != 0 || m.M21 != 0 || m.M13 != 0 || m.M23 != 0 || m.M33 != 1)
            return false;

        var rect = clip.TransformToAABB(m);
        if (!TryRound(rect.Left, out var left) || !TryRound(rect.Top, out var top) || !TryRound(rect.Right, out var right) || !TryRound(rect.Bottom, out var bottom))
            return false;
        pixels = new Rectangle(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
        return true;

        static bool TryRound(double value, out int pixel)
        {
            // Beyond any target, a clip edge only needs to stay there
            var rounded = Math.Round(Math.Clamp(value, -1e8, 1e8));
            pixel = (int)rounded;
            return Math.Abs(value - rounded) <= PixelTolerance || Math.Abs(value) >= 1e8;
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
                if (item.Run is not { } fallbackRun)
                {
                    batch.AddFill(item.Rectangle, item.Color);
                    continue;
                }

                var bounds = fallbackRun.Bounds;
                var texture = graphics.RentTexture(bounds.Width, bounds.Height);
                rented.Add(texture);
                var pixels = new ReadOnlySpan<byte>((void*)fallbackRun.Pixels, bounds.Width * bounds.Height * 4);
                texture.SetData(commandList, pixels, 0, 0, new ResourceRegion(0, 0, 0, bounds.Width, bounds.Height, 1));
                commandList.ResourceBarrierTransition(texture, BarrierLayout.ShaderResource);
                batch.AddImage(bounds, texture, fallbackRun.Scissor);
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

    // A rectangle replaced with a color, or a run drawn in place
    private readonly record struct FrameItem(Rectangle Rectangle, Color4 Color, FallbackRun? Run);
}
