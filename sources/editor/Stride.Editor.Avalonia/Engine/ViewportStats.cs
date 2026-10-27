// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Diagnostics;

namespace Stride.Editor.Avalonia.Engine;

/// <summary>
/// Measures an embedded game viewport (viewport spike): frames drawn by the game and shown by the editor, the cost of
/// each step, and the time from a layout change to a back buffer of the new size. It writes one line a second to the
/// standard output, prefixed with <c>[viewport]</c>.
/// </summary>
internal sealed class ViewportStats
{
    private readonly object sync = new();
    private readonly string mode;
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private long windowStart;

    private int framesDrawn;
    private double drawMs;
    private int framesExported;
    private double exportMs;
    private int framesShown;
    private int newFramesShown;
    private double showMs;
    private int uiTicks;
    private int framesSkipped;

    private (int Width, int Height) requestedSize;
    private long requestedAt = -1;
    private readonly List<double> resizeLatencies = [];

    private (int Width, int Height) lastBackBufferSize;

    public ViewportStats(string mode)
    {
        this.mode = mode;
    }

    /// <summary>
    /// Gets the current time of the stats clock, in ticks of <see cref="Stopwatch"/>.
    /// </summary>
    public long Now => clock.ElapsedTicks;

    /// <summary>
    /// Records that the editor laid out the viewport at a new size (UI thread).
    /// </summary>
    public void SizeRequested(int width, int height)
    {
        lock (sync)
        {
            if (requestedSize == (width, height))
                return;
            requestedSize = (width, height);
            requestedAt = clock.ElapsedTicks;
        }
    }

    /// <summary>
    /// Records a frame drawn by the game (game thread), with the size of its back buffer.
    /// </summary>
    public void FrameDrawn(long startTicks, int width, int height)
    {
        lock (sync)
        {
            framesDrawn++;
            drawMs += ToMs(clock.ElapsedTicks - startTicks);
            if (lastBackBufferSize != (width, height))
            {
                lastBackBufferSize = (width, height);
                if (requestedAt >= 0 && requestedSize == (width, height))
                {
                    resizeLatencies.Add(ToMs(clock.ElapsedTicks - requestedAt));
                    requestedAt = -1;
                }
            }
            Flush();
        }
    }

    /// <summary>
    /// Records the copy of a frame out of the game (game thread): GPU copy and readback, or GPU copy and signal.
    /// </summary>
    public void FrameExported(long startTicks)
    {
        lock (sync)
        {
            framesExported++;
            exportMs += ToMs(clock.ElapsedTicks - startTicks);
        }
    }

    /// <summary>
    /// Records a frame the game couldn't hand over because the editor still used the previous one (game thread).
    /// </summary>
    public void FrameSkipped()
    {
        lock (sync)
            framesSkipped++;
    }

    /// <summary>
    /// Records a render of the view (UI thread): bitmap draw or compositor update. <paramref name="isNewFrame"/> tells
    /// whether it shows a frame the view hadn't shown yet (a render for a layout change redraws the same frame).
    /// </summary>
    public void FrameShown(long startTicks, bool isNewFrame = true)
    {
        lock (sync)
        {
            framesShown++;
            if (isNewFrame)
                newFramesShown++;
            showMs += ToMs(clock.ElapsedTicks - startTicks);
        }
    }

    /// <summary>
    /// Records an animation frame of the editor's top level (UI thread): the rate the editor can render at.
    /// </summary>
    public void UiTick()
    {
        lock (sync)
            uiTicks++;
    }

    private void Flush()
    {
        var now = clock.ElapsedTicks;
        var elapsed = ToMs(now - windowStart);
        if (elapsed < 1000)
            return;

        var seconds = elapsed / 1000;
        var line = $"[viewport] mode={mode} size={lastBackBufferSize.Width}x{lastBackBufferSize.Height}"
            + $" drawn={framesDrawn / seconds:F1}/s draw={Average(drawMs, framesDrawn):F2}ms"
            + $" exported={framesExported / seconds:F1}/s export={Average(exportMs, framesExported):F2}ms"
            + $" shown={framesShown / seconds:F1}/s new={newFramesShown / seconds:F1}/s show={Average(showMs, framesShown):F2}ms skipped={framesSkipped}"
            + $" ui-ticks={uiTicks / seconds:F1}/s";
        if (resizeLatencies.Count > 0)
            line += $" resize-latency(ms)=[{string.Join(",", resizeLatencies.Select(x => x.ToString("F1")))}]";
        Console.WriteLine(line);

        windowStart = now;
        framesDrawn = framesExported = framesShown = newFramesShown = framesSkipped = uiTicks = 0;
        drawMs = exportMs = showMs = 0;
        resizeLatencies.Clear();
    }

    private static double Average(double total, int count) => count > 0 ? total / count : 0;

    private static double ToMs(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;
}
