// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;

namespace Stride.AvaloniaUI.Tests.Rendering;

/// <summary>
/// A drawing context that logs each call and its arguments.
/// </summary>
internal sealed class DrawingCallLog : IDrawingContextImpl, IDrawingContextImplWithEffects, IDrawingContextWithAcrylicLikeSupport
{
    private Matrix transform = Matrix.Identity;

    public List<(string Name, object?[] Arguments)> Calls { get; } = [];

    public Matrix Transform
    {
        get => transform;
        set
        {
            transform = value;
            Log(nameof(Transform), value);
        }
    }

    public void Clear(Color color) => Log(nameof(Clear), color);

    public void DrawBitmap(IBitmapImpl source, double opacity, Rect sourceRect, Rect destRect) => Log(nameof(DrawBitmap), source, opacity, sourceRect, destRect);

    public void DrawBitmap(IBitmapImpl source, IBrush opacityMask, Rect opacityMaskRect, Rect destRect) => Log(nameof(DrawBitmap), source, opacityMask, opacityMaskRect, destRect);

    public void DrawLine(IPen? pen, Point p1, Point p2) => Log(nameof(DrawLine), pen, p1, p2);

    public void DrawGeometry(IBrush? brush, IPen? pen, IGeometryImpl geometry) => Log(nameof(DrawGeometry), brush, pen, geometry);

    public void DrawRectangle(IBrush? brush, IPen? pen, RoundedRect rect, BoxShadows boxShadows = default) => Log(nameof(DrawRectangle), brush, pen, rect, boxShadows);

    public void DrawRectangle(IExperimentalAcrylicMaterial material, RoundedRect rect) => Log("DrawAcrylicRectangle", material, rect);

    public void DrawRegion(IBrush? brush, IPen? pen, IPlatformRenderInterfaceRegion region) => Log(nameof(DrawRegion), brush, pen, region);

    public void DrawEllipse(IBrush? brush, IPen? pen, Rect rect) => Log(nameof(DrawEllipse), brush, pen, rect);

    public void DrawGlyphRun(IBrush? foreground, IGlyphRunImpl glyphRun) => Log(nameof(DrawGlyphRun), foreground, glyphRun);

    public IDrawingContextLayerImpl CreateLayer(PixelSize size) => throw new NotSupportedException();

    public void PushClip(Rect clip) => Log(nameof(PushClip), clip);

    public void PushClip(RoundedRect clip) => Log(nameof(PushClip), clip);

    public void PushClip(IPlatformRenderInterfaceRegion region) => Log(nameof(PushClip), region);

    public void PopClip() => Log(nameof(PopClip));

    public void PushLayer(Rect bounds) => Log(nameof(PushLayer), bounds);

    public void PopLayer() => Log(nameof(PopLayer));

    public void PushOpacity(double opacity, Rect? bounds) => Log(nameof(PushOpacity), opacity, bounds);

    public void PopOpacity() => Log(nameof(PopOpacity));

    public void PushOpacityMask(IBrush mask, Rect bounds) => Log(nameof(PushOpacityMask), mask, bounds);

    public void PopOpacityMask() => Log(nameof(PopOpacityMask));

    public void PushGeometryClip(IGeometryImpl clip) => Log(nameof(PushGeometryClip), clip);

    public void PopGeometryClip() => Log(nameof(PopGeometryClip));

    public void PushRenderOptions(RenderOptions renderOptions) => Log(nameof(PushRenderOptions), renderOptions);

    public void PopRenderOptions() => Log(nameof(PopRenderOptions));

    public void PushTextOptions(TextOptions textOptions) => Log(nameof(PushTextOptions), textOptions);

    public void PopTextOptions() => Log(nameof(PopTextOptions));

    public void PushEffect(Rect? clipRect, IEffect effect) => Log(nameof(PushEffect), clipRect, effect);

    public void PopEffect() => Log(nameof(PopEffect));

    public object? GetFeature(Type t) => null;

    public void Dispose()
    {
    }

    private void Log(string name, params object?[] arguments) => Calls.Add((name, arguments));
}
