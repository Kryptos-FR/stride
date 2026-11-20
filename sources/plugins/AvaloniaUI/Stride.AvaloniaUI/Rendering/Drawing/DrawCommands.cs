// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Stride.AvaloniaUI.Rendering.Fallback;

namespace Stride.AvaloniaUI.Rendering.Drawing;

internal sealed record SetTransformCommand(Matrix Transform) : DrawCommand
{
    public override DrawCallKind Kind => DrawCallKind.SetTransform;

    public override void Replay(IDrawingContextImpl target) => target.Transform = Transform;
}

internal sealed record ClearCommand(Color Color) : DrawCommand
{
    public override DrawCallKind Kind => DrawCallKind.Clear;

    public override void Replay(IDrawingContextImpl target) => target.Clear(Color);
}

internal sealed record DrawBitmapCommand(IBitmapImpl Source, double Opacity, Rect SourceRect, Rect DestRect) : DrawCommand
{
    public override DrawCallKind Kind => DrawCallKind.DrawBitmap;

    public override void Replay(IDrawingContextImpl target) => target.DrawBitmap(Source, Opacity, SourceRect, DestRect);
}

internal sealed record DrawBitmapWithOpacityMaskCommand(IBitmapImpl Source, IBrush OpacityMask, Rect OpacityMaskRect, Rect DestRect) : DrawCommand
{
    public override DrawCallKind Kind => DrawCallKind.DrawBitmapWithOpacityMask;

    public override void Replay(IDrawingContextImpl target) => target.DrawBitmap(Source, OpacityMask, OpacityMaskRect, DestRect);
}

internal sealed record DrawLineCommand(IPen? Pen, Point P1, Point P2) : DrawCommand
{
    public override DrawCallKind Kind => DrawCallKind.DrawLine;

    public override void Replay(IDrawingContextImpl target) => target.DrawLine(Pen, P1, P2);
}

internal sealed record DrawGeometryCommand(IBrush? Brush, IPen? Pen, IGeometryImpl Geometry) : DrawCommand
{
    public override DrawCallKind Kind => DrawCallKind.DrawGeometry;

    public override void Replay(IDrawingContextImpl target) => target.DrawGeometry(Brush, Pen, Geometry);
}

internal sealed record DrawRectangleCommand(IBrush? Brush, IPen? Pen, RoundedRect Rect, BoxShadows BoxShadows) : DrawCommand
{
    public override DrawCallKind Kind => DrawCallKind.DrawRectangle;

    public override void Replay(IDrawingContextImpl target) => target.DrawRectangle(Brush, Pen, Rect, BoxShadows);
}

// Skipped by a target without acrylic, as Avalonia's compositor does
internal sealed record DrawAcrylicRectangleCommand(IExperimentalAcrylicMaterial Material, RoundedRect Rect) : DrawCommand
{
    public override DrawCallKind Kind => DrawCallKind.DrawAcrylicRectangle;

    public override void Replay(IDrawingContextImpl target) => (target as IDrawingContextWithAcrylicLikeSupport)?.DrawRectangle(Material, Rect);
}

internal sealed record DrawEllipseCommand(IBrush? Brush, IPen? Pen, Rect Rect) : DrawCommand
{
    public override DrawCallKind Kind => DrawCallKind.DrawEllipse;

    public override void Replay(IDrawingContextImpl target) => target.DrawEllipse(Brush, Pen, Rect);
}

internal sealed record DrawGlyphRunCommand(IBrush? Foreground, IGlyphRunImpl GlyphRun) : DrawCommand
{
    public override DrawCallKind Kind => DrawCallKind.DrawGlyphRun;

    public override void Replay(IDrawingContextImpl target) => target.DrawGlyphRun(Foreground, GlyphRun);
}

internal sealed record PushClipCommand(Rect Clip) : DrawCommand
{
    public override DrawCallKind Kind => DrawCallKind.PushClip;

    public override void Replay(IDrawingContextImpl target) => target.PushClip(Clip);
}

internal sealed record PushRoundedClipCommand(RoundedRect Clip) : DrawCommand
{
    public override DrawCallKind Kind => DrawCallKind.PushRoundedClip;

    public override void Replay(IDrawingContextImpl target) => target.PushClip(Clip);
}

internal sealed record PopClipCommand : DrawCommand
{
    public override DrawCallKind Kind => DrawCallKind.Pop;

    public override void Replay(IDrawingContextImpl target) => target.PopClip();
}

internal sealed record PushLayerCommand(Rect Bounds) : DrawCommand
{
    public override DrawCallKind Kind => DrawCallKind.PushLayer;

    public override void Replay(IDrawingContextImpl target) => target.PushLayer(Bounds);
}

internal sealed record PopLayerCommand : DrawCommand
{
    public override DrawCallKind Kind => DrawCallKind.Pop;

    public override void Replay(IDrawingContextImpl target) => target.PopLayer();
}

internal sealed record PushOpacityCommand(double Opacity, Rect? Bounds) : DrawCommand
{
    public override DrawCallKind Kind => DrawCallKind.PushOpacity;

    public override void Replay(IDrawingContextImpl target) => target.PushOpacity(Opacity, Bounds);
}

internal sealed record PopOpacityCommand : DrawCommand
{
    public override DrawCallKind Kind => DrawCallKind.Pop;

    public override void Replay(IDrawingContextImpl target) => target.PopOpacity();
}

internal sealed record PushOpacityMaskCommand(IBrush Mask, Rect Bounds) : DrawCommand
{
    public override DrawCallKind Kind => DrawCallKind.PushOpacityMask;

    public override void Replay(IDrawingContextImpl target) => target.PushOpacityMask(Mask, Bounds);
}

internal sealed record PopOpacityMaskCommand : DrawCommand
{
    public override DrawCallKind Kind => DrawCallKind.Pop;

    public override void Replay(IDrawingContextImpl target) => target.PopOpacityMask();
}

internal sealed record PushGeometryClipCommand(IGeometryImpl Clip) : DrawCommand
{
    public override DrawCallKind Kind => DrawCallKind.PushGeometryClip;

    public override void Replay(IDrawingContextImpl target) => target.PushGeometryClip(Clip);
}

internal sealed record PopGeometryClipCommand : DrawCommand
{
    public override DrawCallKind Kind => DrawCallKind.Pop;

    public override void Replay(IDrawingContextImpl target) => target.PopGeometryClip();
}

internal sealed record PushRenderOptionsCommand(RenderOptions RenderOptions) : DrawCommand
{
    public override DrawCallKind Kind => DrawCallKind.PushRenderOptions;

    public override void Replay(IDrawingContextImpl target) => target.PushRenderOptions(RenderOptions);
}

internal sealed record PopRenderOptionsCommand : DrawCommand
{
    public override DrawCallKind Kind => DrawCallKind.Pop;

    public override void Replay(IDrawingContextImpl target) => target.PopRenderOptions();
}

internal sealed record PushTextOptionsCommand(TextOptions TextOptions) : DrawCommand
{
    public override DrawCallKind Kind => DrawCallKind.PushTextOptions;

    public override void Replay(IDrawingContextImpl target) => target.PushTextOptions(TextOptions);
}

internal sealed record PopTextOptionsCommand : DrawCommand
{
    public override DrawCallKind Kind => DrawCallKind.Pop;

    public override void Replay(IDrawingContextImpl target) => target.PopTextOptions();
}

// Skipped by a target without effects, as Avalonia's compositor does
internal sealed record PushEffectCommand(Rect? ClipRect, IEffect Effect) : DrawCommand
{
    public override DrawCallKind Kind => DrawCallKind.PushEffect;

    public override void Replay(IDrawingContextImpl target) => (target as IDrawingContextImplWithEffects)?.PushEffect(ClipRect, Effect);
}

internal sealed record PopEffectCommand : DrawCommand
{
    public override DrawCallKind Kind => DrawCallKind.Pop;

    public override void Replay(IDrawingContextImpl target) => (target as IDrawingContextImplWithEffects)?.PopEffect();
}
