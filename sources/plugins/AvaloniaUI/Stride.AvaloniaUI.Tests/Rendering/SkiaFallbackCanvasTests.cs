// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;
using Avalonia.Skia;
using SkiaSharp;
using Stride.AvaloniaUI.Rendering;
using Stride.AvaloniaUI.Rendering.Drawing;
using Stride.AvaloniaUI.Rendering.Fallback;
using Stride.AvaloniaUI.Skia.Fallback;
using Xunit;

namespace Stride.AvaloniaUI.Tests.Rendering;

/// <summary>
/// Tests the canvas of the Skia fallback, which draws the recorded calls into memory with Skia.
/// </summary>
public class SkiaFallbackCanvasTests
{
    private static readonly Vector Dpi = new(144, 144);

    [Fact]
    public void FallbackDrawsWhatSkiaDrawsWithItsOwnObjects() => TestAvalonia.Run(() =>
    {
        var channel = SkiaOracle.CreateChannel();
        var memoryA = new Memory(200, 150);
        var memoryB = new Memory(200, 150);

        // A: the core's objects, recorded, then drawn by the fallback's canvas
        var core = new StrideRenderInterface(channel);
        using (var scene = new Scene(core))
        {
            var recording = new RecordingDrawingContext(channel, channel.Fallback.CreateCanvas(memoryA.Target), Dpi, null);
            DrawScene(recording, scene);
            recording.Dispose();
        }

        // B: Skia's own objects, drawn by Skia's own drawing context over the same kind of memory
        var skia = SkiaOracle.RenderInterface;
        using (var scene = new Scene(skia))
        using (var context = skia.CreateBackendContext(null))
        using (var renderTarget = context.CreateRenderTarget([new MemoryFramebufferSurface(memoryB.Target)]))
        using (var drawing = renderTarget.CreateDrawingContext(memoryB.SceneInfo, out _))
        {
            DrawScene(drawing, scene);
        }

        Assert.Contains(memoryB.Pixels, value => value != 0);
        Assert.Equal(memoryB.Pixels, memoryA.Pixels);
    });

    [Fact]
    public void VisualBrushContentWithCoreObjectsIsDrawn() => TestAvalonia.Run(() =>
    {
        var channel = SkiaOracle.CreateChannel();
        var memory = new Memory(100, 100);
        var skia = SkiaOracle.RenderInterface;

        // Glyph runs keep the render interface they first find: it stays Skia's while the core's makes the path's geometry
        RuntimeHelpers.RunClassConstructor(typeof(GlyphRun).TypeHandle);
        AvaloniaLocator.CurrentMutable.Bind<IPlatformRenderInterface>().ToConstant(new StrideRenderInterface(channel));
        try
        {
            var path = new Path { Data = Geometry.Parse("M 10,10 H 50 V 50 H 10 Z"), Fill = Brushes.Blue };
            var text = new TextBlock { Text = "Text", FontFamily = new FontFamily("fonts:Inter#Inter"), Foreground = Brushes.Black };
            Canvas.SetLeft(text, 10);
            Canvas.SetTop(text, 60);
            var content = new Canvas { Width = 100, Height = 100, Children = { path, text } };
            content.Measure(new Size(100, 100));
            content.Arrange(new Rect(0, 0, 100, 100));
            var brush = new VisualBrush(content);

            var recording = new RecordingDrawingContext(channel, channel.Fallback.CreateCanvas(memory.Target), Dpi, null);
            recording.DrawRectangle(brush, null, new RoundedRect(new Rect(0, 0, 100, 100)));
            recording.Dispose();
        }
        finally
        {
            AvaloniaLocator.CurrentMutable.Bind<IPlatformRenderInterface>().ToConstant(skia);
        }

        // The path covers (10, 10) to (50, 50), at the target's DPI
        var blue = (255, 0, 0, 255);
        Assert.Equal(blue, memory.GetPixel(12, 12));
        Assert.Equal(blue, memory.GetPixel(47, 47));
        Assert.Equal((0, 0, 0, 0), memory.GetPixel(53, 53));
        Assert.Equal((0, 0, 0, 0), memory.GetPixel(80, 30));
    });

    [Fact]
    public void CustomSkiaDrawingDrawsInPlaceAfterWhatCameBefore() => TestAvalonia.Run(() =>
    {
        var channel = SkiaOracle.CreateChannel();
        var memory = new Memory(40, 40);

        using (var canvas = channel.Fallback.CreateCanvas(memory.Target))
        {
            var commands = new DrawCommandList();
            commands.Add(new DrawRectangleCommand(new ImmutableSolidColorBrush(Colors.Orange), null, new RoundedRect(new Rect(0, 0, 40, 40)), default));
            canvas.Draw(commands);

            var feature = Assert.IsAssignableFrom<ISkiaSharpApiLeaseFeature>(canvas.GetFeature(typeof(ISkiaSharpApiLeaseFeature)));
            using var lease = feature.Lease();
            using var paint = new SKPaint { Color = SKColors.Red };
            lease.SkCanvas.DrawRect(10, 10, 20, 20, paint);
        }

        var red = (0, 0, 255, 255);
        var orange = (0, 165, 255, 255);
        Assert.Equal(red, memory.GetPixel(10, 10));
        Assert.Equal(red, memory.GetPixel(29, 29));
        Assert.Equal(orange, memory.GetPixel(5, 5));
        Assert.Equal(orange, memory.GetPixel(30, 30));
    });

    [Fact]
    public void TargetWithNoPixelsDrawsNothing() => TestAvalonia.Run(() =>
    {
        var channel = SkiaOracle.CreateChannel();

        using var canvas = channel.Fallback.CreateCanvas(new FallbackTarget(IntPtr.Zero, new PixelSize(0, 0), 0, PixelFormat.Bgra8888, Dpi, false));

        Assert.IsType<EmptyFallbackCanvas>(canvas);
        var commands = new DrawCommandList();
        commands.Add(new DrawRectangleCommand(Brushes.Red.ToImmutable(), null, new RoundedRect(new Rect(0, 0, 10, 10)), default));
        canvas.Draw(commands);
    });

    private static void DrawScene(IDrawingContextImpl context, Scene scene)
    {
        var black = new ImmutableSolidColorBrush(Colors.Black);
        var linear = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Colors.Gold, 0), new GradientStop(Colors.Purple, 1) },
        }.ToImmutable();
        var radial = new RadialGradientBrush
        {
            GradientStops = { new GradientStop(Colors.Black, 0), new GradientStop(Colors.Transparent, 1) },
        }.ToImmutable();

        context.Transform = Matrix.CreateTranslation(3.5, 2.25);
        context.DrawRectangle(
            new ImmutableSolidColorBrush(Colors.SteelBlue),
            new ImmutablePen(black, 2),
            new RoundedRect(new Rect(10, 10, 60, 30), 6),
            new BoxShadows(BoxShadow.Parse("3 3 6 #80000000")));
        context.DrawEllipse(linear, new ImmutablePen(new ImmutableSolidColorBrush(Colors.DarkRed), 1.5), new Rect(80, 10, 50, 30));
        context.DrawLine(new ImmutablePen(new ImmutableSolidColorBrush(Colors.Crimson), 2, new ImmutableDashStyle([2, 1], 0.5), PenLineCap.Round), new Point(10, 50), new Point(140, 62));
        context.DrawGeometry(new ImmutableSolidColorBrush(Color.FromArgb(160, 0, 128, 0)), new ImmutablePen(black, 1, lineJoin: PenLineJoin.Round), scene.Path);
        // Skia draws subpixel text of two glyph runs made alike differently, one run in two: the text is in grayscale
        context.PushTextOptions(new TextOptions { TextRenderingMode = TextRenderingMode.Antialias });
        context.DrawGlyphRun(black, scene.GlyphRun);
        context.PopTextOptions();
        context.DrawBitmap(scene.Bitmap, 0.8, new Rect(0, 0, 4, 4), new Rect(150, 8, 32, 24));
        context.DrawRectangle(scene.ImageBrush, new ImmutablePen(scene.ImageBrush, 3), new RoundedRect(new Rect(150, 40, 40, 30)));

        context.PushClip(new Rect(5, 75, 190, 70));
        context.PushClip(new RoundedRect(new Rect(5, 78, 150, 64), 10));
        context.PushOpacity(0.7, null);
        context.PushOpacityMask(radial, new Rect(5, 78, 150, 64));
        context.PushGeometryClip(scene.Ellipse);
        ((IDrawingContextImplWithEffects)context).PushEffect(null, new ImmutableBlurEffect(2));
        context.Transform = Matrix.CreateRotation(0.1) * Matrix.CreateTranslation(4, -6);
        context.DrawRectangle(linear, null, new RoundedRect(new Rect(0, 70, 200, 80)));
        context.DrawGeometry(null, new ImmutablePen(black, 2), scene.Path);
        ((IDrawingContextImplWithEffects)context).PopEffect();
        context.PopGeometryClip();
        context.PopOpacityMask();
        context.PopOpacity();
        context.PopClip();
        context.PopClip();
    }

    // The objects of the scene, made by a render interface
    private sealed class Scene : IDisposable
    {
        public Scene(IPlatformRenderInterface factory)
        {
            Path = factory.CreateStreamGeometry();
            using (var context = ((IStreamGeometryImpl)Path).Open())
            using (var parser = new PathMarkupParser(context))
                parser.Parse("M 15,60 C 40,40 60,90 90,65 L 120,95 Q 70,120 15,95 Z");
            Ellipse = factory.CreateEllipseGeometry(new Rect(20, 80, 120, 55));
            var (typeface, glyphs) = GlyphRunTests.Shape("Hamburgefonstiv", 14, FontWeight.Normal);
            GlyphRun = factory.CreateGlyphRun(typeface, 14, glyphs, new Point(80, 58));

            uint[] pixels =
            [
                0xFFFF0000, 0xFF00FF00, 0xFF0000FF, 0x80800000,
                0xFF000000, 0xFFFFFFFF, 0x80008000, 0x00000000,
                0xFFFFFF00, 0x80000080, 0xFF00FFFF, 0xFFFF00FF,
                0x40404040, 0xFF808080, 0xFF102030, 0xC0602010,
            ];
            var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            try
            {
                var address = handle.AddrOfPinnedObject();
                Bitmap = factory.LoadBitmap(PixelFormat.Bgra8888, AlphaFormat.Premul, address, new PixelSize(4, 4), new Vector(96, 96), 16);
                imageBrushSource = new PlatformBitmap(factory.LoadBitmap(PixelFormat.Bgra8888, AlphaFormat.Premul, address, new PixelSize(4, 4), new Vector(96, 96), 16));
            }
            finally
            {
                handle.Free();
            }
            ImageBrush = new ImageBrush(imageBrushSource)
            {
                DestinationRect = new RelativeRect(0, 0, 6, 6, RelativeUnit.Absolute),
                Opacity = 0.9,
                Stretch = Stretch.Fill,
                TileMode = TileMode.FlipXY,
            }.ToImmutable();
        }

        private readonly PlatformBitmap imageBrushSource;

        public IGeometryImpl Path { get; }

        public IGeometryImpl Ellipse { get; }

        public IGlyphRunImpl GlyphRun { get; }

        public IBitmapImpl Bitmap { get; }

        public IImmutableBrush ImageBrush { get; }

        public void Dispose()
        {
            Bitmap.Dispose();
            imageBrushSource.Dispose();
            GlyphRun.Dispose();
        }
    }

    // A bitmap over a platform bitmap, the core's or Skia's
    private sealed class PlatformBitmap(IBitmapImpl impl) : Bitmap(impl);

    // Premultiplied Bgra8888 pixels in pinned memory
    private sealed class Memory
    {
        public Memory(int width, int height)
        {
            Pixels = GC.AllocateArray<byte>(width * height * 4, pinned: true);
            Target = new FallbackTarget(Marshal.UnsafeAddrOfPinnedArrayElement(Pixels, 0), new PixelSize(width, height), width * 4, PixelFormat.Bgra8888, Dpi, false);
        }

        public byte[] Pixels { get; }

        public FallbackTarget Target { get; }

        public IRenderTarget.RenderTargetSceneInfo SceneInfo => new(Target.Size, Target.Dpi.X / 96, CompositionTransparencyLevel.None);

        public (int B, int G, int R, int A) GetPixel(int x, int y)
        {
            var offset = y * Target.RowBytes + x * 4;
            return (Pixels[offset], Pixels[offset + 1], Pixels[offset + 2], Pixels[offset + 3]);
        }
    }
}
