// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Stride.AvaloniaUI.Rendering.Bitmaps;
using Stride.AvaloniaUI.Rendering.Fallback;

namespace Stride.AvaloniaUI.Rendering.Drawing;

/// <summary>
/// The drawing context of the native renderer: it records the calls, and hands them to a fallback canvas when a feature is asked
/// for and when it is disposed.
/// </summary>
internal sealed class RecordingDrawingContext : IDrawingContextImpl, IDrawingContextImplWithEffects, IDrawingContextWithAcrylicLikeSupport
{
    private readonly FallbackChannel channel;
    private readonly IFallbackCanvas canvas;
    private readonly Vector dpi;
    private readonly Action? disposed;
    private readonly DrawCommandList pending = new();
    private Matrix transform = Matrix.Identity;
    private bool isDisposed;

    /// <param name="channel">The channel to the fallback, which counts the calls.</param>
    /// <param name="canvas">The canvas that draws the calls; the context disposes it.</param>
    /// <param name="dpi">The DPI of the target, which the layers take.</param>
    /// <param name="disposed">Called once the canvas has drawn everything and is disposed, such as to release the target's memory.</param>
    public RecordingDrawingContext(FallbackChannel channel, IFallbackCanvas canvas, Vector dpi, Action? disposed)
    {
        this.channel = channel;
        this.canvas = canvas;
        this.dpi = dpi;
        this.disposed = disposed;
    }

    public Matrix Transform
    {
        get => transform;
        set
        {
            if (transform == value)
                return;
            transform = value;
            Record(new SetTransformCommand(value));
        }
    }

    public void Clear(Color color) => Record(new ClearCommand(color));

    public void DrawBitmap(IBitmapImpl source, double opacity, Rect sourceRect, Rect destRect)
        => Record(new DrawBitmapCommand(source, opacity, sourceRect, destRect));

    public void DrawBitmap(IBitmapImpl source, IBrush opacityMask, Rect opacityMaskRect, Rect destRect)
        => Record(new DrawBitmapWithOpacityMaskCommand(source, opacityMask, opacityMaskRect, destRect));

    public void DrawLine(IPen? pen, Point p1, Point p2) => Record(new DrawLineCommand(pen, p1, p2));

    public void DrawGeometry(IBrush? brush, IPen? pen, IGeometryImpl geometry) => Record(new DrawGeometryCommand(brush, pen, geometry));

    public void DrawRectangle(IBrush? brush, IPen? pen, RoundedRect rect, BoxShadows boxShadows = default)
        => Record(new DrawRectangleCommand(brush, pen, rect, boxShadows));

    public void DrawRectangle(IExperimentalAcrylicMaterial material, RoundedRect rect) => Record(new DrawAcrylicRectangleCommand(material, rect));

    public void DrawRegion(IBrush? brush, IPen? pen, IPlatformRenderInterfaceRegion region)
        => throw new NotSupportedException("The native renderer of the Avalonia UI has no regions.");

    public void DrawEllipse(IBrush? brush, IPen? pen, Rect rect) => Record(new DrawEllipseCommand(brush, pen, rect));

    public void DrawGlyphRun(IBrush? foreground, IGlyphRunImpl glyphRun) => Record(new DrawGlyphRunCommand(foreground, glyphRun));

    public IDrawingContextLayerImpl CreateLayer(PixelSize size) => new StrideRenderTargetBitmap(channel, size, dpi);

    public void PushClip(Rect clip) => Record(new PushClipCommand(clip));

    public void PushClip(RoundedRect clip) => Record(new PushRoundedClipCommand(clip));

    public void PushClip(IPlatformRenderInterfaceRegion region)
        => throw new NotSupportedException("The native renderer of the Avalonia UI has no regions.");

    public void PopClip() => Record(new PopClipCommand());

    public void PushLayer(Rect bounds) => Record(new PushLayerCommand(bounds));

    public void PopLayer() => Record(new PopLayerCommand());

    public void PushOpacity(double opacity, Rect? bounds) => Record(new PushOpacityCommand(opacity, bounds));

    public void PopOpacity() => Record(new PopOpacityCommand());

    public void PushOpacityMask(IBrush mask, Rect bounds) => Record(new PushOpacityMaskCommand(mask, bounds));

    public void PopOpacityMask() => Record(new PopOpacityMaskCommand());

    public void PushGeometryClip(IGeometryImpl clip) => Record(new PushGeometryClipCommand(clip));

    public void PopGeometryClip() => Record(new PopGeometryClipCommand());

    public void PushRenderOptions(RenderOptions renderOptions) => Record(new PushRenderOptionsCommand(renderOptions));

    public void PopRenderOptions() => Record(new PopRenderOptionsCommand());

    public void PushTextOptions(TextOptions textOptions) => Record(new PushTextOptionsCommand(textOptions));

    public void PopTextOptions() => Record(new PopTextOptionsCommand());

    public void PushEffect(Rect? clipRect, IEffect effect) => Record(new PushEffectCommand(clipRect, effect));

    public void PopEffect() => Record(new PopEffectCommand());

    /// <summary>
    /// Gets a feature of the fallback's drawing context, after it has drawn the calls made so far, since the caller may draw
    /// with it directly.
    /// </summary>
    public object? GetFeature(Type t)
    {
        channel.Count(DrawCallKind.Feature);
        DrawPending();
        return canvas.GetFeature(t);
    }

    public void Dispose()
    {
        if (isDisposed)
            return;
        isDisposed = true;

        DrawPending();
        canvas.Dispose();
        disposed?.Invoke();
    }

    private void Record(DrawCommand command)
    {
        channel.Count(command.Kind);
        pending.Add(command);
    }

    private void DrawPending()
    {
        canvas.Draw(pending);
        pending.Clear();
    }
}
