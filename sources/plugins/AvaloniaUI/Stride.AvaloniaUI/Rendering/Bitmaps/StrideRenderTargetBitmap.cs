// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Platform;
using Stride.AvaloniaUI.Rendering.Drawing;
using Stride.AvaloniaUI.Rendering.Fallback;

namespace Stride.AvaloniaUI.Rendering.Bitmaps;

/// <summary>
/// A bitmap of the core that can be drawn into: a render target bitmap, a layer, or an offscreen render target.
/// </summary>
/// <remarks>
/// Premultiplied Bgra8888; it keeps its pixels from a drawing to the next.
/// </remarks>
/// <param name="scaleDrawingToDpi">Whether drawings are in device-independent pixels, scaled to the DPI, as in a render target bitmap.</param>
/// <param name="grayscaleText">Whether text is drawn without subpixel antialiasing, as in a bitmap cache.</param>
internal sealed class StrideRenderTargetBitmap(FallbackChannel channel, PixelSize size, Vector dpi, bool scaleDrawingToDpi = false, bool grayscaleText = false)
    : StrideBitmap(size, dpi, PixelFormat.Bgra8888, AlphaFormat.Premul), IRenderTargetBitmapImpl, IDrawingContextLayerImpl
{
    public bool CanBlit => true;

    public bool IsCorrupted => false;

    /// <summary>
    /// Makes a drawing context into the pixels: the version grows when it is disposed.
    /// </summary>
    public IDrawingContextImpl CreateDrawingContext()
    {
        var canvas = channel.Fallback.CreateCanvas(new FallbackTarget(Address, PixelSize, RowBytes, Format, Dpi, PreviousFrameRetained: true, scaleDrawingToDpi, grayscaleText));
        return new RecordingDrawingContext(channel, canvas, Dpi, MarkChanged);
    }

    /// <summary>
    /// Draws the pixels at the top left of a drawing context, one for one.
    /// </summary>
    public void Blit(IDrawingContextImpl context)
    {
        var rect = new Rect(0, 0, PixelSize.Width, PixelSize.Height);
        context.DrawBitmap(this, 1, rect, rect);
    }
}
