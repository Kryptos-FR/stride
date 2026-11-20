// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;
using Stride.AvaloniaUI.Rendering.Drawing;
using Stride.AvaloniaUI.Rendering.Fallback;

namespace Stride.AvaloniaUI.Skia.Fallback;

/// <summary>
/// A canvas that draws the recorded calls into the target's memory with Skia's own drawing context, as Skia's whole-frame renderer
/// draws on the CPU.
/// </summary>
internal sealed class SkiaFallbackCanvas : IFallbackCanvas
{
    private readonly IRenderTarget renderTarget;
    private readonly ConvertingDrawingContext context;

    public SkiaFallbackCanvas(SkiaRenderFallback fallback, FallbackTarget target)
    {
        renderTarget = fallback.CpuContext.CreateRenderTarget([new MemoryFramebufferSurface(target)]);
        var sceneInfo = new IRenderTarget.RenderTargetSceneInfo(target.Size, target.Dpi.X / 96, CompositionTransparencyLevel.None);
        context = new ConvertingDrawingContext(fallback, renderTarget.CreateDrawingContext(sceneInfo, out _), target.Dpi, target.ScaleDrawingToDpi, target.GrayscaleText);
    }

    /// <summary>
    /// Draws the calls, after those of the lists drawn before, whose transform and pushes still hold.
    /// </summary>
    public void Draw(DrawCommandList commands) => commands.Replay(context);

    /// <summary>
    /// Gets a feature of Skia's drawing context, such as <see cref="Avalonia.Skia.ISkiaSharpApiLeaseFeature"/> to draw with SkiaSharp.
    /// </summary>
    public object? GetFeature(Type featureType) => context.GetFeature(featureType);

    /// <summary>
    /// Disposes Skia's drawing context, which writes what it drew into the memory.
    /// </summary>
    public void Dispose()
    {
        context.Dispose();
        renderTarget.Dispose();
    }
}
