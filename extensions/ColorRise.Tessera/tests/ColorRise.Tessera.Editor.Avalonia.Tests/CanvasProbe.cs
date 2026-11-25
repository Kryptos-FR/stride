using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Platform;
using Avalonia.Threading;
using ColorRise.Tessera.Editor.Canvas;
using StrideColor = Stride.Core.Mathematics.Color;
using Vector2 = Stride.Core.Mathematics.Vector2;

namespace ColorRise.Tessera.Editor.Avalonia.Tests;

/// <summary>
/// Shows a control in a window, and reads the pixels the window rendered.
/// </summary>
internal static class CanvasProbe
{
    /// <summary>
    /// A window of the given size whose content is <paramref name="content"/>, shown and laid out.
    /// </summary>
    public static Window Show(Control content, int width, int height)
    {
        var window = new Window { Width = width, Height = height, Content = content };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    /// <summary>
    /// Runs the dispatched work until the condition holds, or fails after 10 seconds: the pictures of a tile set are made again
    /// after the session records a change, from a thread of the pool.
    /// </summary>
    public static async Task WaitUntilAsync(Func<bool> condition, string what)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"Timed out waiting for {what}.");
            }
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
        }
    }

    /// <summary>
    /// Renders the window, then reads a pixel of the frame.
    /// </summary>
    public static StrideColor Pixel(Window window, int x, int y)
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        using var buffer = frame!.Lock();
        var bytes = new byte[4];
        Marshal.Copy(buffer.Address + y * buffer.RowBytes + x * 4, bytes, 0, 4);
        // The headless platform captures in either byte order
        return frame.Format == PixelFormat.Rgba8888
            ? new StrideColor(bytes[0], bytes[1], bytes[2], bytes[3])
            : new StrideColor(bytes[2], bytes[1], bytes[0], bytes[3]);
    }
}

/// <summary>
/// An input that writes down what the canvas tells it: "press 20,40 Left None", "move 21,40 None", "key B None", "left".
/// </summary>
internal sealed class RecordingInput : ICanvasInput
{
    public List<string> Events { get; } = [];

    /// <summary>
    /// What <see cref="KeyPressed"/> answers.
    /// </summary>
    public bool Handles { get; set; } = true;

    public void PointerPressed(Vector2 position, CanvasButton button, CanvasModifiers modifiers) => Events.Add($"press {At(position)} {button} {modifiers}");

    public void PointerMoved(Vector2 position, CanvasModifiers modifiers) => Events.Add($"move {At(position)} {modifiers}");

    public void PointerReleased(Vector2 position, CanvasButton button, CanvasModifiers modifiers) => Events.Add($"release {At(position)} {button} {modifiers}");

    public void PointerLeft() => Events.Add("left");

    public bool KeyPressed(CanvasKey key, CanvasModifiers modifiers)
    {
        Events.Add($"key {key} {modifiers}");
        return Handles;
    }

    private static string At(Vector2 position) => string.Create(CultureInfo.InvariantCulture, $"{position.X:0.##},{position.Y:0.##}");
}
