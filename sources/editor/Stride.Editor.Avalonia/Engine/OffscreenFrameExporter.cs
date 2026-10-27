// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Platform;
using Stride.Games;
using Stride.Graphics;
using AvaloniaPixelFormat = Avalonia.Platform.PixelFormat;
using StridePixelFormat = Stride.Graphics.PixelFormat;

namespace Stride.Editor.Avalonia.Engine;

/// <summary>
/// The game side of an offscreen viewport (viewport spike): it sizes the back buffer of a window-less game after its
/// <see cref="OffscreenGameView"/>, and hands each frame over to the view, by CPU readback (B') or through a texture
/// shared with the compositor (B, <see cref="GpuFramePresenter"/>).
/// </summary>
/// <remarks>
/// The readback is pipelined: each frame is copied into one of three staging textures, and a staging texture is read
/// when the GPU is done with it, without waiting (one or two frames of latency instead of a stall of the GPU).
/// </remarks>
internal sealed class OffscreenFrameExporter : IDisposable
{
    private const int StagingCount = 3;

    private readonly OffscreenGameView view;
    private readonly ViewportStats stats;
    private readonly Texture?[] stagings = new Texture?[StagingCount];
    private readonly Queue<Texture> inFlight = new();
    private int nextStaging;
    private volatile int requestedWidth;
    private volatile int requestedHeight;

    public OffscreenFrameExporter(OffscreenGameView view, ViewportStats stats)
    {
        this.view = view;
        this.stats = stats;
        view.PixelSizeChanged += (width, height) =>
        {
            requestedWidth = width;
            requestedHeight = height;
        };
        if (ViewportModeSelector.ForcedSize is { } forced)
            (requestedWidth, requestedHeight) = forced;
    }

    /// <summary>
    /// Gets or sets the presenter of the GPU interop mode, or <c>null</c> to read the frames back (B').
    /// </summary>
    public GpuFramePresenter? GpuPresenter { get; set; }

    /// <summary>
    /// Resizes the back buffer to the size of the view (game thread, in the update of the game).
    /// </summary>
    public void ApplyRequestedSize(GraphicsDeviceManager graphicsDeviceManager)
    {
        var (width, height) = ViewportModeSelector.ForcedSize ?? (requestedWidth, requestedHeight);
        if (width <= 0 || height <= 0)
            return;
        if (graphicsDeviceManager.PreferredBackBufferWidth == width && graphicsDeviceManager.PreferredBackBufferHeight == height)
            return;
        graphicsDeviceManager.PreferredBackBufferWidth = width;
        graphicsDeviceManager.PreferredBackBufferHeight = height;
        graphicsDeviceManager.ApplyChanges();
    }

    /// <summary>
    /// Hands the frame over to the view (game thread, before the command list of the frame is closed).
    /// </summary>
    public void EndFrame(CommandList commandList, Texture backBuffer)
    {
        var start = stats.Now;
        if (GpuPresenter is { IsActive: true })
        {
            if (GpuPresenter.CopyFrame(commandList, backBuffer))
                stats.FrameExported(start);
            else
                stats.FrameSkipped();
        }
        else
        {
            ReadBackCompletedFrames(commandList);
            CopyToStaging(commandList, backBuffer);
            stats.FrameExported(start);
        }
    }

    /// <summary>
    /// Completes the frame after its command list was submitted (game thread).
    /// </summary>
    public void AfterSubmit()
    {
        GpuPresenter?.AfterSubmit();
    }

    public void Dispose()
    {
        GpuPresenter?.Dispose();
        foreach (var staging in stagings)
            staging?.Dispose();
    }

    private void CopyToStaging(CommandList commandList, Texture backBuffer)
    {
        // Every staging texture still waits for the GPU: drop this frame rather than stall
        if (inFlight.Count >= StagingCount)
        {
            stats.FrameSkipped();
            return;
        }

        ref var staging = ref stagings[nextStaging];
        nextStaging = (nextStaging + 1) % StagingCount;
        if (staging == null || staging.Width != backBuffer.Width || staging.Height != backBuffer.Height || staging.Format != backBuffer.Format)
        {
            staging?.Dispose();
            staging = backBuffer.ToStaging();
        }
        commandList.Copy(backBuffer, staging);
        inFlight.Enqueue(staging);
    }

    private unsafe void ReadBackCompletedFrames(CommandList commandList)
    {
        // Only the latest completed frame is worth showing
        Texture? completed = null;
        while (inFlight.TryPeek(out var staging) && IsComplete(commandList, staging))
            completed = inFlight.Dequeue();
        if (completed == null)
            return;

        var format = ToAvaloniaFormat(completed.Format);
        var bitmap = view.AcquireBitmap(completed.Width, completed.Height, format);
        using (var buffer = bitmap.Lock())
        {
            var span = new Span<byte>((void*)buffer.Address, buffer.RowBytes * buffer.Size.Height);
            if (buffer.RowBytes == completed.Width * 4)
            {
                completed.GetData(commandList, completed, span);
            }
            else
            {
                var pixels = new byte[completed.Width * completed.Height * 4];
                completed.GetData<byte>(commandList, completed, pixels.AsSpan());
                for (var y = 0; y < completed.Height; y++)
                    pixels.AsSpan(y * completed.Width * 4, completed.Width * 4).CopyTo(span.Slice(y * buffer.RowBytes));
            }
        }
        view.PublishBitmap(bitmap);
    }

    private static bool IsComplete(CommandList commandList, Texture staging)
    {
        // Mapping without waiting tells whether the GPU finished the copy; the data is read right after
        var mapped = commandList.MapSubResource(staging, 0, MapMode.Read, doNotWait: true);
        var complete = !mapped.DataBox.IsEmpty;
        if (complete)
            commandList.UnmapSubResource(mapped);
        return complete;
    }

    private static AvaloniaPixelFormat ToAvaloniaFormat(StridePixelFormat format)
    {
        return format.ToNonSRgb() switch
        {
            StridePixelFormat.B8G8R8A8_UNorm or StridePixelFormat.B8G8R8X8_UNorm => AvaloniaPixelFormat.Bgra8888,
            StridePixelFormat.R8G8B8A8_UNorm => AvaloniaPixelFormat.Rgba8888,
            _ => throw new NotSupportedException($"The back buffer format {format} has no Avalonia bitmap format."),
        };
    }
}
