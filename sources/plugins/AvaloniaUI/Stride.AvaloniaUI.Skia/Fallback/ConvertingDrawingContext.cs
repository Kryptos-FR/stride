// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Platform;
using Stride.AvaloniaUI.Rendering.Bitmaps;
using Stride.AvaloniaUI.Rendering.Geometry;
using Stride.AvaloniaUI.Rendering.Text;

namespace Stride.AvaloniaUI.Skia.Fallback;

/// <summary>
/// Forwards every call to Skia's drawing context, with the geometry, bitmaps, glyph runs and brushes of the core converted to Skia's.
/// </summary>
/// <remarks>
/// Other objects go through unchanged. The context disposes Skia's when it is disposed.
/// </remarks>
internal sealed class ConvertingDrawingContext : IDrawingContextImpl, IDrawingContextImplWithEffects, IDrawingContextWithAcrylicLikeSupport
{
    // What Skia paints with a scene brush that has no content
    private static readonly IImmutableBrush Transparent = new ImmutableSolidColorBrush(Colors.Transparent);

    private readonly SkiaRenderFallback fallback;
    private readonly IDrawingContextImpl skia;
    private readonly Vector dpi;
    private readonly Matrix? scale;
    private readonly bool grayscaleText;
    private readonly Stack<RenderOptions> renderOptionsStack = new();
    private readonly Stack<TextOptions> textOptionsStack = new();
    private readonly List<IDisposable> temporaries = [];
    private RenderOptions currentRenderOptions;
    private TextOptions currentTextOptions;
    private Matrix transform = Matrix.Identity;

    /// <param name="fallback">The fallback that converts the objects of the core.</param>
    /// <param name="skia">Skia's drawing context, which this context disposes.</param>
    /// <param name="dpi">The DPI of what Skia's drawing context draws into.</param>
    /// <param name="scaleToDpi">
    /// Whether drawings are in device-independent pixels while Skia's context draws in pixels, as in Skia's render target bitmaps
    /// and layers of scene brushes.
    /// </param>
    /// <param name="grayscaleText">Whether text is drawn without subpixel antialiasing, as in Skia's bitmap caches.</param>
    public ConvertingDrawingContext(SkiaRenderFallback fallback, IDrawingContextImpl skia, Vector dpi, bool scaleToDpi = false, bool grayscaleText = false)
    {
        this.fallback = fallback;
        this.skia = skia;
        this.dpi = dpi;
        this.grayscaleText = grayscaleText;
        if (scaleToDpi && !dpi.NearlyEquals(new Vector(96, 96)))
        {
            scale = Matrix.CreateScale(dpi.X / 96, dpi.Y / 96);
            skia.Transform = scale.Value;
        }
    }

    public Matrix Transform
    {
        get => scale is null ? skia.Transform : transform;
        set
        {
            if (scale is { } post)
            {
                transform = value;
                skia.Transform = value * post;
            }
            else
            {
                skia.Transform = value;
            }
        }
    }

    public void Clear(Color color) => skia.Clear(color);

    public void DrawBitmap(IBitmapImpl source, double opacity, Rect sourceRect, Rect destRect)
    {
        if (Convert(source) is { } bitmap)
            skia.DrawBitmap(bitmap, opacity, sourceRect, destRect);
    }

    public void DrawBitmap(IBitmapImpl source, IBrush opacityMask, Rect opacityMaskRect, Rect destRect)
    {
        if (Convert(source) is not { } bitmap)
            return;
        try
        {
            skia.DrawBitmap(bitmap, Convert(opacityMask)!, opacityMaskRect, destRect);
        }
        finally
        {
            ReleaseTemporaries();
        }
    }

    public void DrawLine(IPen? pen, Point p1, Point p2)
    {
        try
        {
            skia.DrawLine(Convert(pen), p1, p2);
        }
        finally
        {
            ReleaseTemporaries();
        }
    }

    public void DrawGeometry(IBrush? brush, IPen? pen, IGeometryImpl geometry)
    {
        try
        {
            skia.DrawGeometry(Convert(brush), Convert(pen), Convert(geometry));
        }
        finally
        {
            ReleaseTemporaries();
        }
    }

    public void DrawRectangle(IBrush? brush, IPen? pen, RoundedRect rect, BoxShadows boxShadows = default)
    {
        try
        {
            skia.DrawRectangle(Convert(brush), Convert(pen), rect, boxShadows);
        }
        finally
        {
            ReleaseTemporaries();
        }
    }

    public void DrawRectangle(IExperimentalAcrylicMaterial material, RoundedRect rect)
        => ((IDrawingContextWithAcrylicLikeSupport)skia).DrawRectangle(material, rect);

    public void DrawRegion(IBrush? brush, IPen? pen, IPlatformRenderInterfaceRegion region)
    {
        try
        {
            skia.DrawRegion(Convert(brush), Convert(pen), region);
        }
        finally
        {
            ReleaseTemporaries();
        }
    }

    public void DrawEllipse(IBrush? brush, IPen? pen, Rect rect)
    {
        try
        {
            skia.DrawEllipse(Convert(brush), Convert(pen), rect);
        }
        finally
        {
            ReleaseTemporaries();
        }
    }

    // The text options reach Skia's context with each glyph run, as a whole: they can then be changed for the run, where pushing
    // more options can't change what an outer push set
    public void DrawGlyphRun(IBrush? foreground, IGlyphRunImpl glyphRun)
    {
        var textOptions = grayscaleText ? WithoutSubpixelText(currentTextOptions) : currentTextOptions;
        var pushed = textOptions != default;
        if (pushed)
            skia.PushTextOptions(textOptions);
        try
        {
            skia.DrawGlyphRun(Convert(foreground), glyphRun is StrideGlyphRun core ? fallback.GetGlyphRun(core) : glyphRun);
        }
        finally
        {
            if (pushed)
                skia.PopTextOptions();
            ReleaseTemporaries();
        }
    }

    public IDrawingContextLayerImpl CreateLayer(PixelSize size) => skia.CreateLayer(size);

    public void PushClip(Rect clip) => skia.PushClip(clip);

    public void PushClip(RoundedRect clip) => skia.PushClip(clip);

    public void PushClip(IPlatformRenderInterfaceRegion region) => skia.PushClip(region);

    public void PopClip() => skia.PopClip();

    public void PushLayer(Rect bounds) => skia.PushLayer(bounds);

    public void PopLayer() => skia.PopLayer();

    public void PushOpacity(double opacity, Rect? bounds) => skia.PushOpacity(opacity, bounds);

    public void PopOpacity() => skia.PopOpacity();

    public void PushOpacityMask(IBrush mask, Rect bounds)
    {
        try
        {
            skia.PushOpacityMask(Convert(mask)!, bounds);
        }
        finally
        {
            ReleaseTemporaries();
        }
    }

    public void PopOpacityMask() => skia.PopOpacityMask();

    public void PushGeometryClip(IGeometryImpl clip) => skia.PushGeometryClip(Convert(clip));

    public void PopGeometryClip() => skia.PopGeometryClip();

    public void PushRenderOptions(RenderOptions renderOptions)
    {
        renderOptionsStack.Push(currentRenderOptions);
        currentRenderOptions = currentRenderOptions.MergeWith(renderOptions);
        skia.PushRenderOptions(renderOptions);
    }

    public void PopRenderOptions()
    {
        currentRenderOptions = renderOptionsStack.Pop();
        skia.PopRenderOptions();
    }

    public void PushTextOptions(TextOptions textOptions)
    {
        textOptionsStack.Push(currentTextOptions);
        currentTextOptions = currentTextOptions.MergeWith(textOptions);
    }

    public void PopTextOptions() => currentTextOptions = textOptionsStack.Pop();

    public void PushEffect(Rect? clipRect, IEffect effect) => ((IDrawingContextImplWithEffects)skia).PushEffect(clipRect, effect);

    public void PopEffect() => ((IDrawingContextImplWithEffects)skia).PopEffect();

    public object? GetFeature(Type t) => skia.GetFeature(t);

    public void Dispose() => skia.Dispose();

    private IGeometryImpl Convert(IGeometryImpl geometry) => geometry is StrideGeometry core ? fallback.GetGeometry(core) : geometry;

    // Null when the bitmap has no pixels (0×0), which Skia can't have
    private IBitmapImpl? Convert(IBitmapImpl bitmap) => bitmap is StrideBitmap core ? fallback.GetBitmap(core) : bitmap;

    private IPen? Convert(IPen? pen)
    {
        if (pen?.Brush is not { } brush || Convert(brush) is not { } converted || ReferenceEquals(converted, brush))
            return pen;

        var dashStyle = pen.DashStyle switch
        {
            null => null,
            ImmutableDashStyle immutable => immutable,
            var other => new ImmutableDashStyle(other.Dashes, other.Offset),
        };
        return new ImmutablePen((IImmutableBrush)converted, pen.Thickness, dashStyle, pen.LineCap, pen.LineJoin, pen.MiterLimit);
    }

    // The conversions are in the order Skia reads the brushes, and the objects they make are released after the call
    private IBrush? Convert(IBrush? brush)
    {
        switch (brush)
        {
            case ISceneBrush sceneBrush:
                var created = sceneBrush.CreateContent();
                if (created is null)
                    return Transparent;
                temporaries.Add(created);
                return Convert(created);

            case ISceneBrushContent content:
                return Convert(content);

            case IImageBrush { Source: { } source } imageBrush when ImageBrushSources.GetBitmap(source) is StrideBitmap bitmap:
                return new SkiaImageBrush(imageBrush, fallback.GetBitmapReference(bitmap));

            default:
                return brush;
        }
    }

    // As Skia draws scene brushes into a layer (DrawingContextImpl.ConfigureSceneBrushContentWithSurface in Avalonia.Skia 12.1.1),
    // whose objects may be the core's
    private IBrush Convert(ISceneBrushContent content)
    {
        // Content under a pixel makes no layer and paints nothing
        var rect = content.Rect;
        if (!(rect.Width >= 1) || !(rect.Height >= 1))
            return Transparent;

        var layer = fallback.CpuContext.CreateOffscreenRenderTarget(PixelSize.FromSizeWithDpi(rect.Size, dpi), dpi / 96, enableTextAntialiasing: false);
        var reference = new SkiaBitmapReference(layer);
        temporaries.Add(reference);
        using (var context = new ConvertingDrawingContext(fallback, layer.CreateDrawingContext(), dpi, scaleToDpi: true))
        {
            context.PushRenderOptions(currentRenderOptions);
            context.Clear(Colors.Transparent);
            content.Render(context, rect.TopLeft == default ? null : Matrix.CreateTranslation(-rect.X, -rect.Y));
            context.PopRenderOptions();
        }
        return new SkiaImageBrush(content.Brush, reference);
    }

    // As Skia's contexts without subpixel text (DrawingContextImpl.DrawGlyphRun in Avalonia.Skia 12.1.1): subpixel text, and text
    // of no mode whose edges aren't aliased, is drawn in grayscale
    private TextOptions WithoutSubpixelText(TextOptions options)
    {
        var mode = options.TextRenderingMode;
        var edges = currentRenderOptions.EdgeMode;
        return mode == TextRenderingMode.SubpixelAntialias || (mode == TextRenderingMode.Unspecified && edges is EdgeMode.Unspecified or EdgeMode.Antialias)
            ? options with { TextRenderingMode = TextRenderingMode.Antialias }
            : options;
    }

    private void ReleaseTemporaries()
    {
        foreach (var temporary in temporaries)
            temporary.Dispose();
        temporaries.Clear();
    }
}
