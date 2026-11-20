// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Stride.AvaloniaUI.Rendering.Drawing;
using Stride.AvaloniaUI.Rendering.Fallback;
using Rectangle = Stride.Core.Mathematics.Rectangle;

namespace Stride.AvaloniaUI.Rendering.Native;

/// <summary>
/// Consecutive calls of a frame the canvas doesn't draw itself, rasterized by the fallback into memory of their bounds and
/// drawn into the frame as an image.
/// </summary>
/// <remarks>
/// The fallback starts from the state of the frame when the run starts: its transform, its text and render options. The
/// run's pixels are those of its bounds, so every transform it is given is moved by minus the bounds' origin.
/// </remarks>
internal sealed unsafe class FallbackRun : IDisposable
{
    private readonly DrawCommandList commands = new();
    private readonly BoundsAccumulator accumulator = new();
    private readonly Matrix startTransform;
    private readonly RenderOptions renderOptions;
    private readonly TextOptions textOptions;
    private IFallbackCanvas? liveCanvas;
    private byte* pixels;

    /// <param name="scissor">The scissor of the frame when the run starts, in pixels: the run draws nothing outside it.</param>
    public FallbackRun(Rectangle scissor, Matrix transform, RenderOptions renderOptions, TextOptions textOptions)
    {
        Scissor = scissor;
        startTransform = transform;
        this.renderOptions = renderOptions;
        this.textOptions = textOptions;
    }

    public Rectangle Scissor { get; }

    /// <summary>
    /// Gets the pixels of the frame the run covers, known once it is rasterized: empty when it draws nothing.
    /// </summary>
    public Rectangle Bounds { get; private set; }

    /// <summary>
    /// Gets the premultiplied RGBA pixels of the run, rows packed, or <see cref="IntPtr.Zero"/> when it draws nothing.
    /// </summary>
    public IntPtr Pixels => (IntPtr)pixels;

    /// <summary>
    /// Gets whether the fallback's canvas of the run is made, since a caller draws with its feature.
    /// </summary>
    public bool IsLive => liveCanvas != null;

    /// <summary>
    /// Adds a call that touches pixels, or opens or closes a group, drawn with a transform.
    /// </summary>
    public void Add(DrawCommand command, Matrix transform)
    {
        commands.Add(command);
        if (!IsLive)
            accumulator.Add(command, transform);
    }

    /// <summary>
    /// Adds a change of the frame's state (transform, text and render options), which touches no pixel.
    /// </summary>
    public void AddState(DrawCommand command) => commands.Add(command);

    /// <summary>
    /// Makes the fallback's canvas of the run now, over its whole scissor, draws the calls so far and gets a feature of the canvas.
    /// </summary>
    public object? GetFeature(FallbackChannel channel, Vector dpi, Type featureType)
    {
        if (liveCanvas == null)
        {
            Allocate(Scissor);
            liveCanvas = channel.Fallback.CreateCanvas(CreateTarget(dpi));
            liveCanvas.Draw(Moved(withPrologue: true));
        }
        else
        {
            liveCanvas.Draw(Moved(withPrologue: false));
        }
        commands.Clear();
        return liveCanvas.GetFeature(featureType);
    }

    /// <summary>
    /// Rasterizes the run into memory of its bounds: the union of what its calls can touch, inflated by a pixel, cut by the
    /// scissor (which is inside the target).
    /// </summary>
    public void Rasterize(FallbackChannel channel, Vector dpi)
    {
        if (liveCanvas is { } canvas)
        {
            liveCanvas = null;
            try
            {
                canvas.Draw(Moved(withPrologue: false));
            }
            finally
            {
                canvas.Dispose();
                commands.Clear();
            }
            return;
        }

        var bounds = GetBounds();
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            Bounds = default;
            return;
        }

        Allocate(bounds);
        using var newCanvas = channel.Fallback.CreateCanvas(CreateTarget(dpi));
        newCanvas.Draw(Moved(withPrologue: true));
        commands.Clear();
    }

    public void Dispose()
    {
        if (liveCanvas != null)
        {
            liveCanvas.Dispose();
            liveCanvas = null;
        }
        Free();
        commands.Clear();
        accumulator.Clear();
    }

    private Rectangle GetBounds()
    {
        if (accumulator.Result is not { } rect)
            return Scissor;
        if (rect.Width <= 0 || rect.Height <= 0)
            return default;

        var inflated = rect.Inflate(1);
        var left = (int)Math.Max(Math.Floor(inflated.Left), int.MinValue / 2);
        var top = (int)Math.Max(Math.Floor(inflated.Top), int.MinValue / 2);
        var right = (int)Math.Min(Math.Ceiling(inflated.Right), int.MaxValue / 2);
        var bottom = (int)Math.Min(Math.Ceiling(inflated.Bottom), int.MaxValue / 2);
        return Rectangle.Intersect(new Rectangle(left, top, right - left, bottom - top), Scissor);
    }

    private void Allocate(Rectangle bounds)
    {
        Free();
        Bounds = bounds;
        pixels = (byte*)NativeMemory.AllocZeroed((nuint)bounds.Width * (nuint)bounds.Height * 4);
    }

    private void Free()
    {
        if (pixels != null)
        {
            NativeMemory.Free(pixels);
            pixels = null;
        }
    }

    private FallbackTarget CreateTarget(Vector dpi)
        => new((IntPtr)pixels, new PixelSize(Bounds.Width, Bounds.Height), Bounds.Width * 4, PixelFormat.Rgba8888, dpi, PreviousFrameRetained: false);

    // The calls, their transforms moved to the run's pixels; first the frame's state when the run started
    private DrawCommandList Moved(bool withPrologue)
    {
        var offset = Matrix.CreateTranslation(-Bounds.X, -Bounds.Y);
        var moved = new DrawCommandList();
        if (withPrologue)
        {
            moved.Add(new SetTransformCommand(startTransform * offset));
            moved.Add(new PushRenderOptionsCommand(renderOptions));
            moved.Add(new PushTextOptionsCommand(textOptions));
        }
        foreach (var command in commands.Commands)
            moved.Add(command is SetTransformCommand setTransform ? new SetTransformCommand(setTransform.Transform * offset) : command);
        return moved;
    }
}
