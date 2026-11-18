// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Platform;
using Avalonia.Platform.Surfaces;
using Avalonia.Rendering.Composition;
using Stride.AvaloniaUI.Rendering;
using Stride.AvaloniaUI.Rendering.Bitmaps;
using Stride.AvaloniaUI.Rendering.Drawing;
using Stride.AvaloniaUI.Rendering.Fallback;
using Stride.AvaloniaUI.Rendering.Geometry;
using Stride.AvaloniaUI.Rendering.Text;
using Xunit;

namespace Stride.AvaloniaUI.Tests.Rendering;

/// <summary>
/// Tests the drawing context that records the calls for the fallback, and the render targets that make it.
/// </summary>
public class RecordingDrawingContextTests
{
    private static readonly Vector Dpi = new(96, 96);
    private static readonly IBrush Brush = new ImmutableSolidColorBrush(Colors.Red);
    private static readonly IPen Pen = new ImmutablePen(Brushes.Black, 2);
    private static readonly RoundedRect RoundedRect = new(new Rect(1, 2, 30, 20), 4);

    [Fact]
    public void EveryCallReachesTheCanvasInOrder() => TestAvalonia.Run(() =>
    {
        var fallback = new LoggingFallback();
        var channel = new FallbackChannel(fallback);
        var mask = new ImmutableSolidColorBrush(Colors.Blue);
        var geometry = new StrideRectangleGeometry(channel, new Rect(0, 0, 10, 10));
        using var bitmap = new StrideBitmap(new PixelSize(2, 2), Dpi, PixelFormat.Bgra8888, AlphaFormat.Premul);
        var (typeface, glyphs) = GlyphRunTests.Shape("Text", 12, FontWeight.Normal);
        using var glyphRun = new StrideGlyphRun(channel, typeface, 12, glyphs, new Point(5, 20));
        var effect = new ImmutableBlurEffect(3);
        var material = new ExperimentalAcrylicMaterial();
        var transform = Matrix.CreateTranslation(3, 4);
        var shadows = new BoxShadows(BoxShadow.Parse("2 2 4 Black"));
        var renderOptions = new RenderOptions { BitmapInterpolationMode = Avalonia.Media.Imaging.BitmapInterpolationMode.LowQuality };
        var textOptions = new TextOptions { TextRenderingMode = TextRenderingMode.Alias };
        var rect = new Rect(1, 2, 3, 4);
        var otherRect = new Rect(5, 6, 7, 8);

        (string, object?[])[] expected =
        [
            ("Transform", [transform]),
            ("Clear", [Colors.White]),
            ("DrawBitmap", [bitmap, 0.5, rect, otherRect]),
            ("DrawBitmap", [bitmap, mask, rect, otherRect]),
            ("DrawLine", [Pen, new Point(1, 2), new Point(3, 4)]),
            ("DrawGeometry", [Brush, Pen, geometry]),
            ("DrawRectangle", [Brush, Pen, RoundedRect, shadows]),
            ("DrawEllipse", [Brush, null, rect]),
            ("DrawGlyphRun", [Brush, glyphRun]),
            ("PushClip", [rect]),
            ("PushClip", [RoundedRect]),
            ("PopClip", []),
            ("PopClip", []),
            ("PushLayer", [rect]),
            ("PopLayer", []),
            ("PushOpacity", [0.5, (Rect?)rect]),
            ("PopOpacity", []),
            ("PushOpacityMask", [mask, rect]),
            ("PopOpacityMask", []),
            ("PushGeometryClip", [geometry]),
            ("PopGeometryClip", []),
            ("PushRenderOptions", [renderOptions]),
            ("PopRenderOptions", []),
            ("PushTextOptions", [textOptions]),
            ("PopTextOptions", []),
            ("PushEffect", [(Rect?)rect, effect]),
            ("PopEffect", []),
            ("DrawAcrylicRectangle", [material, RoundedRect]),
        ];

        var context = new RecordingDrawingContext(channel, fallback.CreateCanvas(default), Dpi, null);
        context.Transform = transform;
        context.Clear(Colors.White);
        context.DrawBitmap(bitmap, 0.5, rect, otherRect);
        context.DrawBitmap(bitmap, mask, rect, otherRect);
        context.DrawLine(Pen, new Point(1, 2), new Point(3, 4));
        context.DrawGeometry(Brush, Pen, geometry);
        context.DrawRectangle(Brush, Pen, RoundedRect, shadows);
        context.DrawEllipse(Brush, null, rect);
        context.DrawGlyphRun(Brush, glyphRun);
        context.PushClip(rect);
        context.PushClip(RoundedRect);
        context.PopClip();
        context.PopClip();
        context.PushLayer(rect);
        context.PopLayer();
        context.PushOpacity(0.5, rect);
        context.PopOpacity();
        context.PushOpacityMask(mask, rect);
        context.PopOpacityMask();
        context.PushGeometryClip(geometry);
        context.PopGeometryClip();
        context.PushRenderOptions(renderOptions);
        context.PopRenderOptions();
        context.PushTextOptions(textOptions);
        context.PopTextOptions();
        context.PushEffect(rect, effect);
        context.PopEffect();
        context.DrawRectangle(material, RoundedRect);
        context.Dispose();

        AssertCalls(expected, fallback.Calls);
    });

    [Fact]
    public void TransformIsSetOnlyWhenItChanges()
    {
        var fallback = new LoggingFallback();
        var context = new RecordingDrawingContext(new FallbackChannel(fallback), fallback.CreateCanvas(default), Dpi, null);
        var a = Matrix.CreateScale(2, 2);
        var b = Matrix.CreateTranslation(5, 0);

        context.Transform = a;
        context.Transform = a;
        context.DrawRectangle(Brush, null, RoundedRect);
        context.Transform = b;
        context.Dispose();

        AssertCalls([("Transform", [a]), ("DrawRectangle", [Brush, null, RoundedRect, default(BoxShadows)]), ("Transform", [b])], fallback.Calls);
    }

    [Fact]
    public void FeatureRequestDrawsWhatCameBefore()
    {
        var fallback = new LoggingFallback();
        var context = new RecordingDrawingContext(new FallbackChannel(fallback), fallback.CreateCanvas(default), Dpi, null);

        context.DrawRectangle(Brush, null, RoundedRect);
        context.GetFeature(typeof(object));
        context.DrawEllipse(Brush, null, new Rect(0, 0, 5, 5));
        context.Dispose();

        Assert.Equal(["Draw[DrawRectangle]", "GetFeature", "Draw[DrawEllipse]", "Dispose"], fallback.Events);
    }

    [Fact]
    public void DisposeDrawsThenClosesTheCanvasThenReleasesTheFrame()
    {
        var fallback = new LoggingFallback();
        var context = new RecordingDrawingContext(new FallbackChannel(fallback), fallback.CreateCanvas(default), Dpi, () => fallback.Events.Add("Released"));

        context.DrawRectangle(Brush, null, RoundedRect);
        context.Dispose();

        Assert.Equal(["Draw[DrawRectangle]", "Dispose", "Released"], fallback.Events);
    }

    [Fact]
    public void CallsAreCountedByKind()
    {
        var fallback = new LoggingFallback();
        var channel = new FallbackChannel(fallback);
        var context = new RecordingDrawingContext(channel, fallback.CreateCanvas(default), Dpi, null);

        context.DrawRectangle(Brush, null, RoundedRect);
        context.DrawRectangle(Brush, Pen, RoundedRect);
        context.PushClip(new Rect(0, 0, 5, 5));
        context.PopClip();
        context.Dispose();

        Assert.Equal(2, channel.GetCount(DrawCallKind.DrawRectangle));
        Assert.Equal(1, channel.GetCount(DrawCallKind.PushClip));
        Assert.Equal(1, channel.GetCount(DrawCallKind.Pop));
        Assert.Equal(0, channel.GetCount(DrawCallKind.DrawEllipse));
    }

    [Fact]
    public void RegionsAreNotSupported()
    {
        var fallback = new LoggingFallback();
        using var context = new RecordingDrawingContext(new FallbackChannel(fallback), fallback.CreateCanvas(default), Dpi, null);

        Assert.Throws<NotSupportedException>(() => context.PushClip((IPlatformRenderInterfaceRegion)null!));
        Assert.Throws<NotSupportedException>(() => context.DrawRegion(Brush, null, null!));
    }

    [Fact]
    public void LayerIsABitmapOfThatSizeAtTheContextsDpi()
    {
        var fallback = new LoggingFallback();
        using var context = new RecordingDrawingContext(new FallbackChannel(fallback), fallback.CreateCanvas(default), new Vector(192, 192), null);

        using var layer = context.CreateLayer(new PixelSize(30, 20));

        var bitmap = Assert.IsType<StrideRenderTargetBitmap>(layer);
        Assert.Equal(new PixelSize(30, 20), bitmap.PixelSize);
        Assert.Equal(new Vector(192, 192), bitmap.Dpi);

        var version = bitmap.Version;
        using (var layerContext = layer.CreateDrawingContext())
            layerContext.DrawRectangle(Brush, null, RoundedRect);
        Assert.True(bitmap.Version > version);
        Assert.Equal(new FallbackTarget(bitmap.Address, new PixelSize(30, 20), 120, StrideRenderInterface.PlatformPixelFormat, new Vector(192, 192), true), fallback.Targets.Last());

        var log = new DrawingCallLog();
        layer.Blit(log);
        var full = new Rect(0, 0, 30, 20);
        AssertCalls([("DrawBitmap", [layer, 1.0, full, full])], log);
    }

    [Fact]
    public void LayerOfNoPixelsDoesNotThrow()
    {
        var fallback = new LoggingFallback();
        using var context = new RecordingDrawingContext(new FallbackChannel(fallback), fallback.CreateCanvas(default), Dpi, null);

        using var layer = context.CreateLayer(new PixelSize(0, 0));
        using (var layerContext = layer.CreateDrawingContext())
            layerContext.DrawRectangle(Brush, null, RoundedRect);
        layer.Blit(new DrawingCallLog());
    }

    [Fact]
    public void OffscreenRenderTargetTakesAScalingNotADpi()
    {
        using var renderContext = new StrideRenderInterfaceContext(new FallbackChannel(new LoggingFallback()));

        using var target = renderContext.CreateOffscreenRenderTarget(new PixelSize(30, 20), new Vector(2, 2), true);

        var bitmap = Assert.IsType<StrideRenderTargetBitmap>(target);
        Assert.Equal(new Vector(192, 192), bitmap.Dpi);
    }

    [Fact]
    public void FrameMemoryAndRetentionGoToTheCanvas()
    {
        var fallback = new LoggingFallback();
        var framebuffer = new FakeFramebuffer();
        using var target = new NativeFramebufferRenderTarget(new FallbackChannel(fallback), framebuffer);

        Assert.True(target.Properties.RetainsPreviousFrameContents);
        Assert.True(target.Properties.IsSuitableForDirectRendering);

        var context = target.CreateDrawingContext(new IRenderTarget.RenderTargetSceneInfo(framebuffer.Size, 1.5, CompositionTransparencyLevel.None), out var properties);

        Assert.True(properties.PreviousFrameIsRetained);
        Assert.Equal(new FallbackTarget(framebuffer.Address, framebuffer.Size, framebuffer.RowBytes, PixelFormat.Rgba8888, framebuffer.Dpi, true), Assert.Single(fallback.Targets));
        Assert.False(framebuffer.Released);
        context.Dispose();
        Assert.True(framebuffer.Released);
    }

    private static void AssertCalls((string Name, object?[] Arguments)[] expected, DrawingCallLog log)
    {
        Assert.Equal(expected.Select(call => call.Name), log.Calls.Select(call => call.Name));
        for (var i = 0; i < expected.Length; i++)
        {
            var (name, arguments) = expected[i];
            Assert.Equal(arguments.Length, log.Calls[i].Arguments.Length);
            for (var j = 0; j < arguments.Length; j++)
            {
                var actual = log.Calls[i].Arguments[j];
                if (arguments[j] is null || arguments[j]!.GetType().IsValueType)
                    Assert.True(Equals(arguments[j], actual), $"{name} argument {j}: expected {arguments[j]}, got {actual}.");
                else
                    Assert.True(ReferenceEquals(arguments[j], actual), $"{name} argument {j}: expected the same {arguments[j]}, got {actual}.");
            }
        }
    }

    // A framebuffer that retains its frames, at a made-up address the logging canvases never touch
    private sealed class FakeFramebuffer : IFramebufferRenderTarget
    {
        public IntPtr Address { get; } = 0x10000;

        public PixelSize Size { get; } = new(40, 30);

        public int RowBytes => 176;

        public Vector Dpi { get; } = new(144, 144);

        public bool Released { get; private set; }

        public bool RetainsFrameContents => true;

        public ILockedFramebuffer Lock(IRenderTarget.RenderTargetSceneInfo sceneInfo, out FramebufferLockProperties properties)
        {
            properties = new FramebufferLockProperties(PreviousFrameIsRetained: true);
            return new LockedFramebuffer(Address, Size, RowBytes, Dpi, PixelFormat.Rgba8888, AlphaFormat.Premul, () => Released = true);
        }

        public void Dispose()
        {
        }
    }
}
