// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;
using Stride.AvaloniaUI.Platform;
using Stride.AvaloniaUI.Rendering.Fallback;
using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Games;
using Stride.Graphics.Regression;
using Stride.Input;
using Stride.Rendering.Compositing;
using Xunit;
using AvaloniaColor = Avalonia.Media.Color;
using AvaloniaPixelFormat = Avalonia.Platform.PixelFormat;
using AvaloniaPoint = Avalonia.Point;
using AvaloniaVector = Avalonia.Vector;
using Color = Stride.Core.Mathematics.Color;
using Image = Stride.Graphics.Image;
using PixelFormat = Stride.Graphics.PixelFormat;
using Rect = Avalonia.Rect;

namespace Stride.AvaloniaUI.Native.Tests;

/// <summary>
/// Runs games with Avalonia views drawn by the native renderer, which draws through the Skia fallback on the CPU.
/// </summary>
public sealed class NativeRendererGameTests : GameTestBase
{
    private const int Width = 400;
    private const int Height = 300;

    private static readonly Color BackgroundColor = new(32, 64, 96, 255);
    private static readonly Color ButtonColor = new(255, 128, 0, 255);
    private static readonly Color Red = new(255, 0, 0, 255);
    private static readonly Color Green = new(0, 128, 0, 255);
    private static readonly Color Lime = new(0, 255, 0, 255);

    public NativeRendererGameTests()
    {
        GraphicsDeviceManager.PreferredBackBufferWidth = Width;
        GraphicsDeviceManager.PreferredBackBufferHeight = Height;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ViewIsDrawnOverTheSceneAndTakesItsClicks(bool renderOnGpu) => TestAvalonia.Run(() =>
    {
        var channel = StrideAvaloniaPlatform.NativeChannel;
        Assert.NotNull(channel);
        channel.ResetCounts();
        var clicks = 0;
        var button = CreateButton();
        button.Click += (_, _) => clicks++;
        bool? onGpu = null;

        using var frame = RenderView(button, renderOnGpu: renderOnGpu, steps: (game, frames) => frames
            .Update(5, () => game.MouseSimulated.SetPosition(new Vector2(0.5f, 0.5f)))
            .Update(7, () => game.MouseSimulated.SimulateMouseDown(MouseButton.Left))
            .Update(9, () => game.MouseSimulated.SimulateMouseUp(MouseButton.Left))
            // Away from the button, so it shows its own color and not the pointer-over one
            .Update(10, () => game.MouseSimulated.SetPosition(new Vector2(0.02f, 0.02f)))
            .Draw(14, () => onGpu = game.Services.GetSafeServiceAs<AvaloniaUISystem>().IsRenderingOnGpu), readAt: 14);

        Assert.Equal(AvaloniaUIRenderer.Native, StrideAvaloniaPlatform.Renderer);
        // The native renderer draws on the CPU in this step, whatever RenderOnGpu says
        Assert.False(onGpu);
        Assert.Equal(1, clicks);
        AssertColor(BackgroundColor, frame, 5, 5);
        AssertColor(BackgroundColor, frame, Width - 5, Height - 5);
        AssertColor(ButtonColor, frame, Width / 2 - 60, Height / 2);
        Assert.True(channel.GetCount(DrawCallKind.DrawRectangle) > 0);
        Assert.True(channel.GetCount(DrawCallKind.DrawGlyphRun) > 0);
    });

    [Fact]
    public void CustomSkiaDrawingIsDrawnInPlace() => TestAvalonia.Run(() =>
    {
        var drawn = new SkiaDrawnControl { Width = 100, Height = 80 };
        Canvas.SetLeft(drawn, 100);
        Canvas.SetTop(drawn, 80);
        var border = new Border
        {
            ClipToBounds = true,
            Width = 150,
            Height = 120,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Child = new Canvas { Children = { drawn } },
        };

        using var frame = RenderView(border);

        // The control covers (100, 80) to (200, 160), and the border clips it at (150, 120)
        AssertColor(Red, frame, 102, 82);
        AssertColor(Red, frame, 147, 117);
        AssertColor(BackgroundColor, frame, 98, 100);
        AssertColor(BackgroundColor, frame, 153, 100);
        AssertColor(BackgroundColor, frame, 120, 123);
        AssertColor(BackgroundColor, frame, 180, 150);
    });

    [Fact]
    public void VisualBrushContentIsDrawn() => TestAvalonia.Run(() =>
    {
        var path = new Path { Data = Geometry.Parse("M 0,0 H 40 V 40 H 0 Z"), Fill = Brushes.Lime };
        Canvas.SetLeft(path, 10);
        Canvas.SetTop(path, 10);
        var text = new TextBlock { Text = "Brush", Foreground = Brushes.White };
        Canvas.SetLeft(text, 10);
        Canvas.SetTop(text, 60);
        var visual = new Canvas { Width = 100, Height = 100, Children = { path, text } };
        var border = new Border
        {
            Width = 100,
            Height = 100,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Background = new VisualBrush(visual),
        };

        using var frame = RenderView(border);

        AssertColor(Lime, frame, 30, 30);
        AssertColor(BackgroundColor, frame, 80, 30);
    });

    [Fact]
    public void WriteableBitmapChangesShowInTheNextFrame() => TestAvalonia.Run(() =>
    {
        using var bitmap = new WriteableBitmap(new PixelSize(20, 20), new AvaloniaVector(96, 96), AvaloniaPixelFormat.Bgra8888, AlphaFormat.Premul);
        Fill(bitmap, Colors.Red);
        var image = CreateImage(bitmap);
        Image? redFrame = null;

        using var frame = RenderView(image, steps: (game, frames) => frames
            .Draw(6, () => redFrame = ReadBackBuffer(game))
            .Update(7, () =>
            {
                Fill(bitmap, Colors.Green);
                image.InvalidateVisual();
            }));

        using (redFrame)
            AssertColor(Red, redFrame!, 60, 60);
        AssertColor(Green, frame, 60, 60);
    });

    [Fact]
    public void UnpremultipliedBgraBitmapShowsItsColor() => TestAvalonia.Run(() =>
    {
        // Red at half alpha, its color not multiplied by the alpha
        const byte Alpha = 128;
        var pixels = new uint[20 * 20];
        Array.Fill(pixels, (uint)Alpha << 24 | 0xFF0000);
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        Bitmap bitmap;
        try
        {
            bitmap = new Bitmap(AvaloniaPixelFormat.Bgra8888, AlphaFormat.Unpremul, handle.AddrOfPinnedObject(), new PixelSize(20, 20), new AvaloniaVector(96, 96), 20 * 4);
        }
        finally
        {
            handle.Free();
        }

        using (bitmap)
        {
            var image = CreateImage(bitmap);
            (int R, int G, int B, int A)? drawn = null;

            // The view's own frame, premultiplied RGBA: the game then blends it over the scene the same way whatever the renderer
            using var frame = RenderView(image, steps: (game, frames) => frames.Draw(10, () =>
            {
                var impl = (StrideTopLevelImpl)TopLevel.GetTopLevel(image)!.PlatformImpl!;
                Assert.True(impl.TryGetFrame(out var data, out _, out var rowPitch));
                var pixel = data + 60 * rowPitch + 60 * 4;
                drawn = (Marshal.ReadByte(pixel), Marshal.ReadByte(pixel, 1), Marshal.ReadByte(pixel, 2), Marshal.ReadByte(pixel, 3));
            }));

            Assert.NotNull(drawn);
            var (r, g, b, a) = drawn.Value;
            Assert.True(Math.Abs(r - Alpha) <= 3 && g <= 3 && b <= 3 && Math.Abs(a - Alpha) <= 3, $"Expected ({Alpha}, 0, 0, {Alpha}), got {drawn}.");
        }
    });

    [Fact]
    public void ScaledViewDrawsAtItsPixelScale() => TestAvalonia.Run(() =>
    {
        // 20×20 DIPs each, at x = 10, 50 and 90 DIPs, y = 10 DIPs; the brush and the cached control are lime on their left half
        var drawn = new SkiaDrawnControl { Width = 20, Height = 20 };
        Canvas.SetLeft(drawn, 10);
        Canvas.SetTop(drawn, 10);
        var brushed = new Border { Width = 20, Height = 20, Background = new VisualBrush(CreateHalfLime()) };
        Canvas.SetLeft(brushed, 50);
        Canvas.SetTop(brushed, 10);
        var cached = new Border { Width = 20, Height = 20, CacheMode = new BitmapCache(), Child = CreateHalfLime() };
        Canvas.SetLeft(cached, 90);
        Canvas.SetTop(cached, 10);
        var content = new Canvas { Children = { drawn, brushed, cached } };

        // The view's resolution is half the back buffer's: it draws at a scaling of 2
        using var frame = RenderView(content, resolution: new Vector2(Width / 2, Height / 2));

        // The custom drawing covers (20, 20) to (60, 60) in the back buffer
        AssertColor(Red, frame, 21, 21);
        AssertColor(Red, frame, 58, 58);
        AssertColor(BackgroundColor, frame, 62, 40);
        AssertColor(BackgroundColor, frame, 40, 62);
        // The brush's lime covers (100, 20) to (120, 60)
        AssertColor(Lime, frame, 101, 21);
        AssertColor(Lime, frame, 118, 58);
        AssertColor(BackgroundColor, frame, 122, 40);
        AssertColor(BackgroundColor, frame, 110, 62);
        // The cached control's lime covers (180, 20) to (200, 60)
        AssertColor(Lime, frame, 181, 21);
        AssertColor(Lime, frame, 198, 58);
        AssertColor(BackgroundColor, frame, 202, 40);
        AssertColor(BackgroundColor, frame, 190, 62);
    });

    [Fact]
    public void EmptyContentDrawsNothing() => TestAvalonia.Run(() =>
    {
        // Avalonia's writeable bitmaps have a pixel at least, its other bitmaps don't
        using var emptyBitmap = new Bitmap(AvaloniaPixelFormat.Bgra8888, AlphaFormat.Premul, IntPtr.Zero, new PixelSize(0, 0), new AvaloniaVector(96, 96), 0);
        var content = new StackPanel
        {
            Children =
            {
                new Path { Data = new StreamGeometry(), Fill = Brushes.Red, Stroke = Brushes.White, StrokeThickness = 2 },
                new Avalonia.Controls.Image { Source = emptyBitmap, Width = 20, Height = 20 },
                new TextBlock { Text = string.Empty },
            },
        };

        using var frame = RenderView(content);

        AssertColor(BackgroundColor, frame, 5, 5);
        AssertColor(BackgroundColor, frame, 15, 25);
        AssertColor(BackgroundColor, frame, Width / 2, Height / 2);
    });

    private static Button CreateButton() => new()
    {
        Content = "Play",
        FontSize = 24,
        HorizontalContentAlignment = HorizontalAlignment.Center,
        VerticalContentAlignment = VerticalAlignment.Center,
        Width = 160,
        Height = 60,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        Background = new SolidColorBrush(AvaloniaColor.FromRgb(ButtonColor.R, ButtonColor.G, ButtonColor.B)),
        // The theme's press animation runs on the wall clock, which the frames of a test don't follow
        Transitions = [],
    };

    // An image at (50, 50), one pixel of the bitmap to one of the view
    private static Avalonia.Controls.Image CreateImage(Bitmap bitmap) => new()
    {
        Source = bitmap,
        Stretch = Stretch.None,
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
        Margin = new Thickness(50, 50, 0, 0),
    };

    // 20×20 DIPs, lime on the left half
    private static Canvas CreateHalfLime() => new()
    {
        Width = 20,
        Height = 20,
        Children = { new Avalonia.Controls.Shapes.Rectangle { Width = 10, Height = 20, Fill = Brushes.Lime } },
    };

    private static void Fill(WriteableBitmap bitmap, AvaloniaColor color)
    {
        using var pixels = bitmap.Lock();
        var bgra = (uint)color.A << 24 | (uint)color.R << 16 | (uint)color.G << 8 | color.B;
        for (var y = 0; y < pixels.Size.Height; y++)
        {
            for (var x = 0; x < pixels.Size.Width; x++)
                Marshal.WriteInt32(pixels.Address + y * pixels.RowBytes + x * 4, (int)bgra);
        }
    }

    /// <summary>
    /// Runs a game that shows a view over a cleared scene, and reads the back buffer at a frame.
    /// </summary>
    private static Image RenderView(Control content, Vector2? resolution = null, bool renderOnGpu = true,
        Func<NativeRendererGameTests, FrameGameSystem, FrameGameSystem>? steps = null, int readAt = 10)
    {
        var game = new NativeRendererGameTests();
        var component = new AvaloniaUIComponent { Content = content, Resolution = resolution ?? new Vector2(Width, Height) };
        var scene = new Scene();
        scene.Entities.Add(new Entity("UI") { component });
        game.SceneSystem.SceneInstance = new SceneInstance(game.Services, scene);
        game.SceneSystem.GraphicsCompositor = new GraphicsCompositor { Game = new ClearRenderer { Color = BackgroundColor } };
        game.Services.GetSafeServiceAs<AvaloniaUISystem>().RenderOnGpu = renderOnGpu;

        Image? frame = null;
        game.FrameGameSystem.IsUnitTestFeeding = true;
        // Read the back buffer once everything else is drawn
        game.FrameGameSystem.DrawOrder = int.MaxValue;
        steps?.Invoke(game, game.FrameGameSystem);
        game.FrameGameSystem.Draw(readAt, () => frame = ReadBackBuffer(game));

        RunGameTest(game);

        Assert.NotNull(frame);
        return frame;
    }

    private static Image ReadBackBuffer(NativeRendererGameTests game)
        => game.GraphicsDevice.Presenter.BackBuffer.GetDataAsImage(game.GraphicsContext.CommandList);

    private static Color GetPixel(Image image, int x, int y)
    {
        var pixel = image.PixelBuffer[0].GetPixel<Color>(x, y);
        return image.Description.Format is PixelFormat.B8G8R8A8_UNorm or PixelFormat.B8G8R8A8_UNorm_SRgb
            ? new Color(pixel.B, pixel.G, pixel.R, pixel.A)
            : pixel;
    }

    private static void AssertColor(Color expected, Image image, int x, int y)
    {
        var pixel = GetPixel(image, x, y);
        const int Tolerance = 3;
        Assert.True(Math.Abs(expected.R - pixel.R) <= Tolerance && Math.Abs(expected.G - pixel.G) <= Tolerance && Math.Abs(expected.B - pixel.B) <= Tolerance,
            $"Expected {expected} at ({x}, {y}), got {pixel}.");
    }

    // A control that fills its bounds red with SkiaSharp, through the drawing context's Skia feature
    private sealed class SkiaDrawnControl : Control
    {
        public override void Render(DrawingContext context) => context.Custom(new FillRed(new Rect(Bounds.Size)));

        private sealed class FillRed(Rect bounds) : ICustomDrawOperation
        {
            public Rect Bounds => bounds;

            public bool HitTest(AvaloniaPoint p) => bounds.Contains(p);

            public bool Equals(ICustomDrawOperation? other) => other is FillRed fill && fill.Bounds == Bounds;

            public void Render(ImmediateDrawingContext context)
            {
                if (context.TryGetFeature<ISkiaSharpApiLeaseFeature>() is not { } feature)
                    return;
                using var lease = feature.Lease();
                using var paint = new SKPaint { Color = SKColors.Red };
                lease.SkCanvas.DrawRect(0, 0, (float)bounds.Width, (float)bounds.Height, paint);
            }

            public void Dispose()
            {
            }
        }
    }
}
