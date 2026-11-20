// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Platform;
using Avalonia.Platform.Surfaces;
using Avalonia.Rendering.Composition;
using Avalonia.Skia;
using Stride.AvaloniaUI.Platform;
using Stride.AvaloniaUI.Rendering;
using Stride.AvaloniaUI.Rendering.Drawing;
using Stride.AvaloniaUI.Rendering.Fallback;
using Stride.AvaloniaUI.Rendering.Geometry;
using Stride.AvaloniaUI.Rendering.Native;
using Stride.AvaloniaUI.Rendering.Text;
using Stride.AvaloniaUI.Tests;
using Stride.Core;
using Stride.Core.Diagnostics;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Graphics;
using Stride.Graphics.Regression;
using Stride.Rendering.Compositing;
using Stride.Shaders;
using Xunit;
using AvaloniaColor = Avalonia.Media.Color;
using AvaloniaPixelFormat = Avalonia.Platform.PixelFormat;
using Color = Stride.Core.Mathematics.Color;
using Image = Stride.Graphics.Image;
using PixelFormat = Stride.Graphics.PixelFormat;
using Rect = Avalonia.Rect;
using Vector = Avalonia.Vector;

namespace Stride.AvaloniaUI.Native.Tests;

/// <summary>
/// Runs games whose Avalonia views the native canvas draws on the game's graphics device.
/// </summary>
public sealed class NativeCanvasTests : GameTestBase
{
    private const int Width = 400;
    private const int Height = 300;

    private static readonly Color BackgroundColor = new(32, 64, 96, 255);
    private static readonly Color ButtonColor = new(255, 128, 0, 255);

    public NativeCanvasTests()
    {
        GraphicsDeviceManager.PreferredBackBufferWidth = Width;
        GraphicsDeviceManager.PreferredBackBufferHeight = Height;
        // The canvas needs the features of Direct3D 10 (instancing, texel loads)
        GraphicsDeviceManager.PreferredGraphicsProfile = [GraphicsProfile.Level_11_0];
    }

    /// <summary>
    /// Gets the names of the reference views, one test case each.
    /// </summary>
    public static IEnumerable<object[]> PageNames => ReferenceViews.Pages.Select(page => new object[] { page });

    /// <summary>
    /// Gets the names of the shape cases, one test case each.
    /// </summary>
    public static IEnumerable<object[]> ShapeCaseNames => ShapeCases.Names.Select(name => new object[] { name });

    /// <summary>
    /// Gets the names of the text cases, one test case each.
    /// </summary>
    public static IEnumerable<object[]> TextCaseNames => TextCases.Names.Select(name => new object[] { name });

    [SkippableFact]
    public void CanvasShaderIsEmbeddedForTheDevice() => TestAvalonia.Run(() =>
    {
        // Direct3D has no bytecode until it is generated on Windows
        SkipWithoutBytecode();
        Assert.NotNull(AvaloniaCanvasEffect.Bytecode);
        Assert.Contains(AvaloniaCanvasEffect.Bytecode!.Stages, s => s.Stage == ShaderStage.Vertex);
        Assert.Contains(AvaloniaCanvasEffect.Bytecode!.Stages, s => s.Stage == ShaderStage.Pixel);
    });

    [Fact]
    public void NativeGraphicsNeedsLevel10AndBytecode()
    {
        Assert.False(NativeGraphics.CanDraw(GraphicsProfile.Level_9_3, hasBytecode: true));
        Assert.False(NativeGraphics.CanDraw(GraphicsProfile.Level_11_0, hasBytecode: false));
        Assert.True(NativeGraphics.CanDraw(GraphicsProfile.Level_10_0, hasBytecode: true));
    }

    [Fact]
    public void ReasonNotToDrawNamesWhatIsMissing()
    {
        Assert.Contains("Level_10_0", NativeGraphics.GetReasonNotToDraw(GraphicsProfile.Level_9_3, hasBytecode: true));
        Assert.Contains("bytecode", NativeGraphics.GetReasonNotToDraw(GraphicsProfile.Level_11_0, hasBytecode: false));
        Assert.Null(NativeGraphics.GetReasonNotToDraw(GraphicsProfile.Level_10_0, hasBytecode: true));
    }

    [SkippableFact]
    public void NativeGraphicsOfOneDeviceSharesItsPipelines() => TestAvalonia.Run(() =>
    {
        SkipWithoutBytecode();

        var game = CreateGame(new Canvas());
        (PipelineState Blended, PipelineState Opaque)? first = null;
        (PipelineState Blended, PipelineState Opaque)? second = null;
        game.FrameGameSystem.Draw(10, () =>
        {
            // One after the other on the game's device, as each move between the GPU and the CPU makes one
            using (var graphics = new NativeGraphics(game.GraphicsDevice))
                first = (graphics.Resources.Blended, graphics.Resources.Opaque);
            using (var graphics = new NativeGraphics(game.GraphicsDevice))
                second = (graphics.Resources.Blended, graphics.Resources.Opaque);
        });
        RunGameTest(game);

        Assert.Same(first!.Value.Blended, second!.Value.Blended);
        Assert.Same(first.Value.Opaque, second.Value.Opaque);
    });

    [SkippableFact]
    public void DeclinedDeviceIsLoggedOnceWithItsReason() => TestAvalonia.Run(() =>
    {
        SkipWithoutBytecode();

        var backend = StrideAvaloniaPlatform.NativeBackend!;
        var game = CreateGame(new Canvas());
        game.Services.GetSafeServiceAs<AvaloniaUISystem>().RenderOnGpu = false;
        var messages = new List<ILogMessage>();
        var attached = new List<bool>();
        NativeGraphics? graphics = null;
        game.FrameGameSystem.Draw(10, () =>
        {
            using (CollectMessages(messages))
            {
                attached.Add(backend.AttachGraphicsDevice(game.GraphicsDevice, "the test declines it"));
                attached.Add(backend.AttachGraphicsDevice(game.GraphicsDevice, "the test declines it"));
            }
            graphics = backend.Graphics;
        });
        RunGameTest(game);

        Assert.Equal(new[] { false, false }, attached);
        var message = Assert.Single(messages);
        Assert.Equal(LogMessageType.Info, message.Type);
        Assert.Contains("the test declines it", message.Text);
        Assert.Contains("CPU", message.Text);
        Assert.Null(graphics);
    });

    [SkippableFact]
    public void FeatureUnderAnEmptyScissorIsNullAndLogsNothing() => TestAvalonia.Run(() =>
    {
        SkipWithoutBytecode();

        // Avalonia leaves out custom drawing under an empty clip of its own, so the frames' contexts are driven directly:
        // custom drawing that asks for Skia's canvas under a clip of no width, a scissor of no pixels, for 10 frames
        var game = CreateGame(new Canvas());
        var channel = new FallbackChannel(StrideAvaloniaPlatform.NativeChannel!.Fallback);
        var messages = new List<ILogMessage>();
        var leasesGranted = 0;
        var asked = 0;
        for (var frame = 10; frame < 20; frame++)
        {
            game.FrameGameSystem.Draw(frame, () =>
            {
                var graphics = StrideAvaloniaPlatform.NativeGraphics!;
                using var texture = Texture.New2D(game.GraphicsDevice, 32, 32, PixelFormat.R8G8B8A8_UNorm, TextureFlags.RenderTarget | TextureFlags.ShaderResource);
                graphics.Context = game.GraphicsContext;
                try
                {
                    using (CollectMessages(messages))
                    using (var context = new NativeDrawingContext(graphics, channel, texture, new Vector(96, 96), null))
                    {
                        context.PushClip(new Rect(10, 10, 0, 40));
                        asked++;
                        if (context.GetFeature(typeof(ISkiaSharpApiLeaseFeature)) is ISkiaSharpApiLeaseFeature feature)
                        {
                            leasesGranted++;
                            using var lease = feature.Lease();
                        }
                        context.PopClip();
                    }
                }
                finally
                {
                    graphics.Context = null;
                }
            });
        }
        RunGameTest(game);

        Assert.Equal(10, asked);
        Assert.Equal(0, leasesGranted);
        // Nothing is counted, and no run is made
        Assert.Equal(0, channel.GetCount(DrawCallKind.Feature));
        Assert.Empty(channel.GetReasons());
        Assert.DoesNotContain(messages, message => message.Type >= LogMessageType.Warning);
    });

    [SkippableFact]
    public void TwoViewsInOneFrameMatchTheFallback() => TestAvalonia.Run(() =>
    {
        SkipWithoutBytecode();

        // Black text on white, and the Fluent page's text, white on dark included, drawn in the same frames
        string[] pages = ["Text", "Fluent"];
        var resolution = new Vector2(ReferenceViews.Width, ReferenceViews.Height);
        var game = CreateGame(ReferenceViews.Create(pages[0]), resolution, ReferenceViews.Width, ReferenceViews.Height);
        var components = new[]
        {
            game.SceneSystem.SceneInstance.RootScene.Entities[0].Get<AvaloniaUIComponent>(),
            new AvaloniaUIComponent { Content = ReferenceViews.Create(pages[1]), Resolution = resolution, DrawOrder = 1 },
        };
        game.SceneSystem.SceneInstance.RootScene.Entities.Add(new Entity("UI 2") { components[1] });
        var system = game.Services.GetSafeServiceAs<AvaloniaUISystem>();
        system.RenderOnGpu = false;
        var fallback = new Image?[2];
        var native = new Image?[2];
        GraphicsAdapter? adapter = null;
        bool? onGpu = null;

        game.FrameGameSystem
            .Draw(30, () =>
            {
                for (var i = 0; i < 2; i++)
                    fallback[i] = ReadView(game, components[i]);
            })
            .Update(31, () => system.RenderOnGpu = true)
            .Draw(45, () =>
            {
                onGpu = system.IsRenderingOnGpu;
                for (var i = 0; i < 2; i++)
                    native[i] = ReadView(game, components[i]);
                adapter = game.GraphicsDevice.Adapter;
            });
        RunGameTest(game);

        try
        {
            Assert.True(onGpu, "The views aren't drawn on the GPU.");
            for (var i = 0; i < 2; i++)
                AssertWithinAllowance(fallback[i]!, native[i]!, PageAllowance(adapter!, pages[i]), $"TwoViews.{pages[i]}");
        }
        finally
        {
            foreach (var image in fallback.Concat(native))
                image?.Dispose();
        }
    });

    [SkippableFact]
    public void SmallChangeRedrawsOnlyItsDirtyRectangle() => TestAvalonia.Run(() =>
    {
        SkipWithoutBytecode();

        var magenta = new Color(255, 0, 255, 255);
        var newBorderColor = new Color(178, 34, 34, 255);
        var border = new Border { Width = 16, Height = 16, Background = Brushes.SteelBlue };
        Canvas.SetLeft(border, 300);
        Canvas.SetTop(border, 200);
        var text = new TextBlock { Text = "Text elsewhere", FontSize = 18, Foreground = Brushes.Black };
        Canvas.SetLeft(text, 40);
        Canvas.SetTop(text, 60);
        var game = CreateGame(new Canvas { Background = Brushes.White, Children = { border, text } });
        var component = game.SceneSystem.SceneInstance.RootScene.Entities[0].Get<AvaloniaUIComponent>();
        Color[]? pixels = null;
        var width = 0;

        game.FrameGameSystem
            .Draw(10, () =>
            {
                // A marker far from the border, written into the texture the view keeps between frames
                var frame = GetTopLevelImpl(component).GpuFrame!;
                var marker = Enumerable.Repeat(magenta, 8 * 8).ToArray();
                frame.SetData<Color>(game.GraphicsContext.CommandList, marker, 0, 0, new ResourceRegion(8, 8, 0, 16, 16, 1));
                game.GraphicsContext.CommandList.ResourceBarrierTransition(frame, BarrierLayout.ShaderResource);
            })
            .Update(11, () => border.Background = new SolidColorBrush(AvaloniaColor.FromRgb(newBorderColor.R, newBorderColor.G, newBorderColor.B)))
            .Draw(12, () =>
            {
                var frame = GetTopLevelImpl(component).GpuFrame!;
                width = frame.Width;
                pixels = frame.GetData<Color>(game.GraphicsContext.CommandList);
            });
        RunGameTest(game);

        // Outside the dirty rectangle, not drawn again; the change, drawn
        Assert.Equal(magenta, pixels![12 * width + 12]);
        Assert.Equal(newBorderColor, pixels[208 * width + 308]);
    });

    [SkippableTheory]
    [MemberData(nameof(PageNames))]
    public void PageMatchesTheFallback(string page) => TestAvalonia.Run(() =>
    {
        SkipWithoutBytecode();

        var (fallback, native, _, adapter) = RenderBothWays(ReferenceViews.Create(page), ReferenceViews.Width, ReferenceViews.Height);

        using (fallback)
        using (native)
        {
            if (ReferenceViews.OtherGlyphsAllowance.ContainsKey(page))
                SaveTextFrames(fallback, native, page);
            AssertWithinAllowance(fallback, native, PageAllowance(adapter, page), page);
        }
    });

    [SkippableTheory]
    [MemberData(nameof(PageNames))]
    public void PageFallsBackOnlyForLaterSteps(string page) => TestAvalonia.Run(() =>
    {
        SkipWithoutBytecode();

        var (fallback, native, reasons, _) = RenderBothWays(ReferenceViews.Create(page), ReferenceViews.Width, ReferenceViews.Height);
        fallback.Dispose();
        native.Dispose();
        Assert.Empty(reasons.Except(ReferenceViews.AllowedFallbacks[page]));
    });

    [SkippableTheory]
    [MemberData(nameof(ShapeCaseNames))]
    public void ShapeMatchesTheFallback(string name) => TestAvalonia.Run(() =>
    {
        SkipWithoutBytecode();

        var (fallback, native, reasons, adapter) = RenderBothWays(ShapeCases.Create(name), ShapeCases.Width, ShapeCases.Height, ShapeCases.GetScaling(name));

        FallbackReason[] expectedReasons = name switch
        {
            "GradientRectangle" => [FallbackReason.Brush],
            "ShadowedRectangle" => [FallbackReason.BoxShadow],
            "TwoShaderClips" => [FallbackReason.ClipDepth],
            "GeometryClipCase" => [FallbackReason.GeometryClip],
            _ => [],
        };
        Assert.Equal(expectedReasons, reasons);
        using (fallback)
        using (native)
        {
            // Skia strokes an ellipse geometry along quadratic curves that follow its offset outlines to within about 0.15
            // pixels, on the CPU and on the GPU alike; the native ring is the exact one
            Skip.If(name == "EllipseStroke", "Skia's stroke of an ellipse geometry is a little off the exact ring: 398 pixels differ by more than 12, and Skia on the GPU differs on 258.");
            var measured = ShapeCases.EdgeAllowance[name];
            var allowance = ShapeCases.OtherGlyphsAllowance.TryGetValue(name, out var otherGlyphs)
                ? EdgeAllowances.ForText(adapter, measured, otherGlyphs)
                : EdgeAllowances.On(adapter, measured);
            AssertWithinAllowance(fallback, native, allowance, name);
        }
    });

    [SkippableFact]
    public void PartialRedrawMatchesAFullRedraw() => TestAvalonia.Run(() =>
    {
        SkipWithoutBytecode();

        // Text in and around a border whose background changes: only the border's rectangle is drawn again
        var border = new Border
        {
            Width = 180,
            Height = 70,
            Margin = new Thickness(20),
            Background = Brushes.SteelBlue,
            Child = new TextBlock { Text = "Partial redraw", FontSize = 22, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center },
        };
        var content = new StackPanel
        {
            Children =
            {
                new TextBlock { Text = "Above the border", FontSize = 18, Foreground = Brushes.Gold },
                border,
                new TextBlock { Text = "Below the border", FontSize = 18, Foreground = Brushes.Gold },
            },
        };
        var game = CreateGame(content);
        var system = game.Services.GetSafeServiceAs<AvaloniaUISystem>();
        Image? partial = null;
        Image? full = null;

        game.FrameGameSystem
            .Update(10, () => border.Background = Brushes.Firebrick)
            .Draw(15, () => partial = ReadBackBuffer(game))
            .Update(16, () => system.RenderOnGpu = false)
            .Update(18, () => system.RenderOnGpu = true)
            .Draw(25, () => full = ReadBackBuffer(game));
        RunGameTest(game);

        using (partial)
        using (full)
            AssertClose(full!, partial!, 1, nameof(PartialRedrawMatchesAFullRedraw));
    });

    [SkippableFact]
    public void UNormBackBufferShowsTheColorsOfTheCpuPath() => TestAvalonia.Run(() =>
    {
        SkipWithoutBytecode();

        var game = CreateGame(CreateButton());
        // Gamma space, or the back buffer becomes sRGB
        game.GraphicsDeviceManager.PreferredColorSpace = ColorSpace.Gamma;
        game.GraphicsDeviceManager.PreferredBackBufferFormat = PixelFormat.R8G8B8A8_UNorm;
        var system = game.Services.GetSafeServiceAs<AvaloniaUISystem>();
        Image? gpu = null;
        Image? cpu = null;
        var onGpu = new List<bool>();

        game.FrameGameSystem
            .Draw(10, () =>
            {
                onGpu.Add(system.IsRenderingOnGpu);
                gpu = ReadBackBuffer(game);
            })
            .Update(11, () => system.RenderOnGpu = false)
            .Draw(20, () =>
            {
                onGpu.Add(system.IsRenderingOnGpu);
                cpu = ReadBackBuffer(game);
            });
        RunGameTest(game);

        using (gpu)
        using (cpu)
        {
            Assert.Equal(new[] { true, false }, onGpu);
            Assert.False(gpu!.Description.Format.IsSRgb);
            AssertColor(ButtonColor, gpu, Width / 2 - 60, Height / 2);
            AssertColor(ButtonColor, cpu!, Width / 2 - 60, Height / 2);
            AssertColor(BackgroundColor, gpu, 5, 5);
        }
    });

    [SkippableFact]
    public void ViewMovesBetweenTheGpuAndTheCpu() => TestAvalonia.Run(() =>
    {
        SkipWithoutBytecode();

        var game = CreateGame(CreateButton());
        var system = game.Services.GetSafeServiceAs<AvaloniaUISystem>();
        var frames = new List<(bool OnGpu, Image Frame)>();
        void Read() => frames.Add((system.IsRenderingOnGpu, ReadBackBuffer(game)));

        game.FrameGameSystem
            .Draw(10, Read)
            .Update(11, () => system.RenderOnGpu = false)
            .Draw(20, Read)
            .Update(21, () => system.RenderOnGpu = true)
            .Draw(30, Read);
        RunGameTest(game);

        try
        {
            Assert.Equal(new[] { true, false, true }, frames.Select(frame => frame.OnGpu));
            foreach (var (_, frame) in frames)
            {
                AssertColor(BackgroundColor, frame, 5, 5);
                AssertColor(ButtonColor, frame, Width / 2 - 60, Height / 2);
            }
        }
        finally
        {
            foreach (var (_, frame) in frames)
                frame.Dispose();
        }
    });

    [SkippableFact]
    public void ResizedViewIsRedrawnWhole() => TestAvalonia.Run(() =>
    {
        SkipWithoutBytecode();

        // One pixel of the view to one of the back buffer, whatever its size
        var game = CreateGame(CreateButton(), resolution: Vector2.Zero);
        Image? small = null;
        Image? large = null;

        game.FrameGameSystem
            .Draw(10, () => small = ReadBackBuffer(game))
            .Update(11, () =>
            {
                game.GraphicsDeviceManager.PreferredBackBufferWidth = 640;
                game.GraphicsDeviceManager.PreferredBackBufferHeight = 480;
                game.GraphicsDeviceManager.ApplyChanges();
            })
            .Draw(20, () => large = ReadBackBuffer(game));
        RunGameTest(game);

        using (small)
        using (large)
        {
            AssertColor(ButtonColor, small!, 130, 150);
            Assert.Equal(640, large!.Description.Width);
            // The button is 160×60 at the center, (240, 210) to (400, 270), and the rest is the scene
            AssertColor(ButtonColor, large, 250, 240);
            AssertColor(ButtonColor, large, 390, 240);
            AssertColor(BackgroundColor, large, 230, 240);
            AssertColor(BackgroundColor, large, 410, 240);
            AssertColor(BackgroundColor, large, 5, 5);
            AssertColor(BackgroundColor, large, 600, 400);
            AssertColor(BackgroundColor, large, 635, 475);
        }
    });

    [SkippableFact]
    public void EmptyViewOnTheGpuDrawsNothing() => TestAvalonia.Run(() =>
    {
        SkipWithoutBytecode();

        var game = CreateGame(new Canvas());
        var system = game.Services.GetSafeServiceAs<AvaloniaUISystem>();
        bool? onGpu = null;
        Image? frame = null;
        (Texture? Frame, int Version)? emptyScene = null;

        game.FrameGameSystem
            .Draw(10, () =>
            {
                onGpu = system.IsRenderingOnGpu;
                frame = ReadBackBuffer(game);

                // A scene of no pixels: the drawing calls go nowhere, and no frame is handed to the top level
                var graphics = StrideAvaloniaPlatform.NativeGraphics!;
                using var topLevel = new StrideTopLevelImpl();
                using var target = new NativeTextureRenderTarget(graphics, StrideAvaloniaPlatform.NativeChannel!, topLevel);
                graphics.Context = game.GraphicsContext;
                try
                {
                    using var context = target.CreateDrawingContext(new IRenderTarget.RenderTargetSceneInfo(new PixelSize(0, 0), 1, CompositionTransparencyLevel.None), out _);
                    context.Clear(Colors.Transparent);
                    context.DrawRectangle(Brushes.Red.ToImmutable(), null, new RoundedRect(new Rect(0, 0, 10, 10)));
                }
                finally
                {
                    graphics.Context = null;
                }
                emptyScene = (topLevel.GpuFrame, topLevel.FrameVersion);
            });
        RunGameTest(game);

        using (frame)
        {
            Assert.True(onGpu);
            Assert.Null(emptyScene!.Value.Frame);
            Assert.Equal(0, emptyScene.Value.Version);
            AssertColor(BackgroundColor, frame!, 5, 5);
            AssertColor(BackgroundColor, frame!, Width / 2, Height / 2);
            AssertColor(BackgroundColor, frame!, Width - 5, Height - 5);
        }
    });

    [SkippableFact]
    public void ShaderClipLeavesWhatItDoesNotCover() => TestAvalonia.Run(() =>
    {
        SkipWithoutBytecode();

        var blue = AvaloniaColor.FromRgb(0, 0, 255);
        var red = AvaloniaColor.FromRgb(255, 0, 0);
        var game = CreateGame(new Canvas());
        Color[]? pixels = null;
        game.FrameGameSystem.Draw(10, () =>
        {
            var graphics = StrideAvaloniaPlatform.NativeGraphics!;
            using var texture = Texture.New2D(game.GraphicsDevice, 32, 32, PixelFormat.R8G8B8A8_UNorm, TextureFlags.RenderTarget | TextureFlags.ShaderResource);
            graphics.Context = game.GraphicsContext;
            try
            {
                using (var context = new NativeDrawingContext(graphics, StrideAvaloniaPlatform.NativeChannel!, texture, new Vector(96, 96), null))
                {
                    context.Clear(blue);

                    // A clear under a rounded clip replaces what the clip covers, and leaves the corners
                    context.PushClip(new RoundedRect(new Rect(0, 0, 32, 16), 8));
                    context.Clear(red);
                    context.PopClip();

                    // A rounded clip of no width covers no pixel, not even half of one
                    context.PushClip(new RoundedRect(new Rect(16.5, 16, 0, 16), 4));
                    context.Clear(red);
                    context.PopClip();
                }
                pixels = texture.GetData<Color>(game.GraphicsContext.CommandList);
            }
            finally
            {
                graphics.Context = null;
            }
        });
        RunGameTest(game);

        Assert.Equal(new Color(255, 0, 0, 255), pixels![8 * 32 + 16]);
        Assert.Equal(new Color(0, 0, 255, 255), pixels[0]);
        Assert.Equal(new Color(0, 0, 255, 255), pixels[24 * 32 + 16]);
    });

    [SkippableFact]
    public void ThrowingRunDoesNotLoseTheFrame() => TestAvalonia.Run(() =>
    {
        SkipWithoutBytecode();

        var fallback = new ThrowingFallback(StrideAvaloniaPlatform.NativeChannel!.Fallback);
        var channel = new FallbackChannel(fallback);
        var red = Brushes.Red.ToImmutable();
        // A gradient goes to the fallback, a solid color doesn't
        var redGradient = new LinearGradientBrush { GradientStops = { new GradientStop(Colors.Red, 0), new GradientStop(Colors.Red, 1) } }.ToImmutable();
        var blue = AvaloniaColor.FromRgb(0, 0, 255);

        // On the GPU: the run whose canvas throws is lost, not the frame, and the next frame draws
        var game = CreateGame(new Canvas());
        var ended = 0;
        Color[]? lostRun = null;
        Color[]? nextFrame = null;
        game.FrameGameSystem.Draw(10, () =>
        {
            var graphics = StrideAvaloniaPlatform.NativeGraphics!;
            using var texture = Texture.New2D(game.GraphicsDevice, 32, 32, PixelFormat.R8G8B8A8_UNorm, TextureFlags.RenderTarget | TextureFlags.ShaderResource);
            graphics.Context = game.GraphicsContext;
            try
            {
                fallback.Throws = true;
                using (var context = new NativeDrawingContext(graphics, channel, texture, new Vector(96, 96), () => ended++))
                {
                    context.Clear(blue);
                    context.DrawRectangle(redGradient, null, new RoundedRect(new Rect(8, 8, 16, 16)));
                }
                lostRun = texture.GetData<Color>(game.GraphicsContext.CommandList);

                fallback.Throws = false;
                using (var context = new NativeDrawingContext(graphics, channel, texture, new Vector(96, 96), () => ended++))
                    context.DrawRectangle(redGradient, null, new RoundedRect(new Rect(8, 8, 16, 16)));
                nextFrame = texture.GetData<Color>(game.GraphicsContext.CommandList);
            }
            finally
            {
                graphics.Context = null;
            }
        });
        RunGameTest(game);

        Assert.Equal(2, ended);
        Assert.Equal(new Color(0, 0, 255, 255), lostRun![16 * 32 + 16]);
        Assert.Equal(new Color(255, 0, 0, 255), nextFrame![16 * 32 + 16]);
        Assert.Equal(new Color(0, 0, 255, 255), nextFrame![2 * 32 + 2]);

        // On the CPU: the recording context releases its target whatever its canvas throws
        var released = 0;
        fallback.Throws = true;
        var memory = new byte[32 * 32 * 4];
        unsafe
        {
            fixed (byte* address = memory)
            {
                var target = new FallbackTarget((IntPtr)address, new PixelSize(32, 32), 32 * 4, AvaloniaPixelFormat.Rgba8888, new Vector(96, 96), false);
                var recording = new RecordingDrawingContext(channel, channel.Fallback.CreateCanvas(target), new Vector(96, 96), () => released++);
                recording.DrawRectangle(red, null, new RoundedRect(new Rect(8, 8, 16, 16)));
                Assert.Throws<ThrowingFallback.Thrown>(recording.Dispose);
            }
        }
        Assert.Equal(1, released);

        // The framebuffer is unlocked when the canvas can't be made, and when the frame's canvas throws
        var framebuffer = new CountingFramebuffer();
        using var renderTarget = new NativeFramebufferRenderTarget(channel, framebuffer);
        var sceneInfo = new IRenderTarget.RenderTargetSceneInfo(new PixelSize(32, 32), 1, CompositionTransparencyLevel.None);
        fallback.ThrowsOnCreate = true;
        Assert.Throws<ThrowingFallback.Thrown>(() => renderTarget.CreateDrawingContext(sceneInfo, out _));
        Assert.Equal((1, 1), (framebuffer.Locks, framebuffer.Unlocks));
        fallback.ThrowsOnCreate = false;
        var drawing = renderTarget.CreateDrawingContext(sceneInfo, out _);
        drawing.DrawRectangle(red, null, new RoundedRect(new Rect(8, 8, 16, 16)));
        Assert.Throws<ThrowingFallback.Thrown>(drawing.Dispose);
        Assert.Equal((2, 2), (framebuffer.Locks, framebuffer.Unlocks));
        fallback.Throws = false;
        using (var next = renderTarget.CreateDrawingContext(sceneInfo, out _))
            next.Clear(Colors.Red);
        Assert.Equal((3, 3), (framebuffer.Locks, framebuffer.Unlocks));
    });

    private static void SkipWithoutBytecode()
        => Skip.If(AvaloniaCanvasEffect.Bytecode == null && GraphicsDevice.Platform != GraphicsPlatform.Vulkan, $"The canvas shader has no {GraphicsDevice.Platform} bytecode yet.");

    /// <summary>
    /// Draws a view on the CPU through the fallback, then on the GPU, in one game.
    /// </summary>
    /// <returns>
    /// The frame on the CPU, read at draw 30, the frame on the GPU, read at draw 45 after the full redraw the move causes, the
    /// reasons of the fallbacks on the GPU, and the adapter the game ran on.
    /// </returns>
    [SkippableTheory]
    [MemberData(nameof(TextCaseNames))]
    public void TextMatchesTheFallback(string name) => TestAvalonia.Run(() =>
    {
        SkipWithoutBytecode();

        var (fallback, native, reasons, adapter) = RenderBothWays(TextCases.Create(name), TextCases.Width, TextCases.Height, TextCases.GetScaling(name));

        Assert.Equal(TextCases.ExpectedReasons[name], reasons);
        using (fallback)
        using (native)
        {
            Assert.True(CountColors(fallback) > 2, $"{name} shows no text.");
            SaveTextFrames(fallback, native, name);
            // Native glyphs are the CPU's masks where Skia's glyphs are FreeType's, and a run that falls back is drawn on
            // the CPU: what Skia on the GPU draws apart (TextCases.EdgeAllowance) isn't allowed here
            AssertWithinAllowance(fallback, native, EdgeAllowances.ForText(adapter, 0, TextCases.OtherGlyphsAllowance[name]), name);
        }
    });

    [SkippableFact]
    public void TextIsRedrawnAfterAMoveBetweenGpuAndCpu() => TestAvalonia.Run(() =>
    {
        SkipWithoutBytecode();

        var backend = StrideAvaloniaPlatform.NativeBackend!;
        var game = CreateGame(ReferenceViews.Create("Text"), new Vector2(ReferenceViews.Width, ReferenceViews.Height), ReferenceViews.Width, ReferenceViews.Height);
        var system = game.Services.GetSafeServiceAs<AvaloniaUISystem>();
        NativeGraphics? before = null;
        NativeGraphics? after = null;
        Image? fallback = null;
        Image? native = null;
        GraphicsAdapter? adapter = null;
        bool? onGpu = null;

        game.FrameGameSystem
            .Draw(15, () => before = backend.Graphics)
            .Update(16, () => system.RenderOnGpu = false)
            .Draw(30, () => fallback = ReadBackBuffer(game))
            .Update(31, () => system.RenderOnGpu = true)
            .Draw(46, () =>
            {
                onGpu = system.IsRenderingOnGpu;
                after = backend.Graphics;
                native = ReadBackBuffer(game);
                adapter = game.GraphicsDevice.Adapter;
            });
        RunGameTest(game);

        Assert.True(onGpu, "The view isn't drawn on the GPU after the move back.");
        Assert.NotNull(before);
        Assert.NotNull(after);
        Assert.NotSame(before, after);
        Assert.True(before.IsDisposed);
        using (fallback)
        using (native)
            AssertWithinAllowance(fallback!, native!, PageAllowance(adapter!, "Text"), "TextAfterTheMove");
    });

    [SkippableFact]
    public void FullAtlasFallsBackForTheFrameOnly() => TestAvalonia.Run(() =>
    {
        SkipWithoutBytecode();

        // One page of 64 pixels can't hold "Quartz" at 48 pixels, and holds it at 12
        var backend = StrideAvaloniaPlatform.NativeBackend!;
        backend.AtlasPageSize = 64;
        backend.AtlasMaxPages = 1;
        try
        {
            var text = new TextBlock { Text = "Quartz", FontSize = 48, Foreground = Brushes.Black };
            var game = CreateGame(new Canvas { Background = Brushes.White, Children = { text } }, new Vector2(240, 160), 240, 160);
            var system = game.Services.GetSafeServiceAs<AvaloniaUISystem>();
            var channel = StrideAvaloniaPlatform.NativeChannel!;
            system.RenderOnGpu = false;
            Image? fallback = null;
            Image? full = null;
            Image? small = null;
            IReadOnlyList<FallbackReason>? fullReasons = null;
            IReadOnlyList<FallbackReason>? smallReasons = null;
            GraphicsAdapter? adapter = null;

            game.FrameGameSystem
                .Draw(30, () => fallback = ReadBackBuffer(game))
                .Update(31, () =>
                {
                    system.RenderOnGpu = true;
                    channel.ResetCounts();
                })
                .Draw(45, () =>
                {
                    full = ReadBackBuffer(game);
                    fullReasons = channel.GetReasons();
                    adapter = game.GraphicsDevice.Adapter;
                })
                .Update(46, () =>
                {
                    text.FontSize = 12;
                    channel.ResetCounts();
                })
                .Draw(60, () =>
                {
                    small = ReadBackBuffer(game);
                    smallReasons = channel.GetReasons();
                });
            RunGameTest(game);

            Assert.Equal([FallbackReason.GlyphAtlasFull], fullReasons);
            Assert.Empty(smallReasons!);
            using (fallback)
            using (full)
            using (small)
            {
                AssertWithinAllowance(fallback!, full!, EdgeAllowances.On(adapter!, 0), "FullAtlas");
                Assert.True(FrameComparison.CountBeyond(full!, small!, 0) > 0, "The text at 12 pixels isn't drawn.");
            }
        }
        finally
        {
            backend.AtlasPageSize = GlyphAtlas.DefaultPageSize;
            backend.AtlasMaxPages = GlyphAtlas.DefaultMaxPages;
        }
    });

    private static (Image Fallback, Image Native, IReadOnlyList<FallbackReason> Reasons, GraphicsAdapter Adapter) RenderBothWays(Control content, int width, int height, double scaling = 1)
    {
        var game = CreateGame(content, new Vector2((float)(width / scaling), (float)(height / scaling)), width, height);
        var system = game.Services.GetSafeServiceAs<AvaloniaUISystem>();
        var channel = StrideAvaloniaPlatform.NativeChannel!;
        system.RenderOnGpu = false;
        Image? fallback = null;
        Image? native = null;
        IReadOnlyList<FallbackReason>? reasons = null;
        GraphicsAdapter? adapter = null;
        bool? onGpu = null;

        game.FrameGameSystem
            .Draw(30, () => fallback = ReadBackBuffer(game))
            .Update(31, () =>
            {
                system.RenderOnGpu = true;
                channel.ResetCounts();
            })
            .Draw(45, () =>
            {
                onGpu = system.IsRenderingOnGpu;
                native = ReadBackBuffer(game);
                reasons = channel.GetReasons();
                adapter = game.GraphicsDevice.Adapter;
            });
        RunGameTest(game);

        Assert.True(onGpu, "The view isn't drawn on the GPU.");
        return (fallback!, native!, reasons!, adapter!);
    }

    /// <summary>
    /// Makes a game that shows a view over a cleared scene, on the GPU, with the mouse away from the view.
    /// </summary>
    private static NativeCanvasTests CreateGame(Control content, Vector2? resolution = null, int width = Width, int height = Height)
    {
        var game = new NativeCanvasTests();
        game.GraphicsDeviceManager.PreferredBackBufferWidth = width;
        game.GraphicsDeviceManager.PreferredBackBufferHeight = height;
        var component = new AvaloniaUIComponent { Content = content, Resolution = resolution ?? new Vector2(width, height) };
        var scene = new Scene();
        scene.Entities.Add(new Entity("UI") { component });
        game.SceneSystem.SceneInstance = new SceneInstance(game.Services, scene);
        game.SceneSystem.GraphicsCompositor = new GraphicsCompositor { Game = new ClearRenderer { Color = BackgroundColor } };
        game.Services.GetSafeServiceAs<AvaloniaUISystem>().RenderOnGpu = true;

        game.FrameGameSystem.IsUnitTestFeeding = true;
        // Read the back buffer once everything else is drawn
        game.FrameGameSystem.DrawOrder = int.MaxValue;
        // Away from the view, so that no control shows its pointer-over look
        game.FrameGameSystem.Update(1, () => game.MouseSimulated.SetPosition(new Vector2(2, 2)));
        return game;
    }

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
        Transitions = [],
    };

    private static StrideTopLevelImpl GetTopLevelImpl(AvaloniaUIComponent component)
        => (StrideTopLevelImpl)component.TopLevel!.PlatformImpl!;

    // The last frame of a view: its texture on the GPU, or its memory on the CPU; premultiplied RGBA either way
    private static unsafe Image ReadView(NativeCanvasTests game, AvaloniaUIComponent component)
    {
        var impl = GetTopLevelImpl(component);
        if (impl.GpuFrame is { } texture)
            return texture.GetDataAsImage(game.GraphicsContext.CommandList);

        Assert.True(impl.TryGetFrame(out var data, out var size, out var rowPitch), "The view has no frame.");
        var image = Image.New2D(size.Width, size.Height, 1, PixelFormat.R8G8B8A8_UNorm);
        var buffer = image.PixelBuffer[0];
        for (var y = 0; y < size.Height; y++)
            System.Buffer.MemoryCopy((byte*)data + y * rowPitch, (byte*)buffer.DataPointer + y * buffer.RowStride, buffer.RowStride, size.Width * 4);
        return image;
    }

    // Collects the messages of the Avalonia UI's logger until disposed
    private static IDisposable CollectMessages(List<ILogMessage> messages)
    {
        void Collect(ILogMessage message)
        {
            if (message.Module == "AvaloniaUI")
            {
                lock (messages)
                    messages.Add(message);
            }
        }

        GlobalLogger.GlobalMessageLogged += Collect;
        return new AnonymousDisposable(() => GlobalLogger.GlobalMessageLogged -= Collect);
    }

    private static Image ReadBackBuffer(NativeCanvasTests game)
        => game.GraphicsDevice.Presenter.BackBuffer.GetDataAsImage(game.GraphicsContext.CommandList);

    // The number of colors of a frame: two at most for a plain page with a band, more with antialiased text
    private static int CountColors(Image image)
    {
        var colors = new HashSet<Color>();
        var size = image.Description;
        for (var y = 0; y < size.Height; y++)
        {
            for (var x = 0; x < size.Width; x++)
                colors.Add(GetPixel(image, x, y));
        }
        return colors.Count;
    }

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

    // Two frames no channel of which differs by more than a tolerance, but for a few outliers within a larger one; both
    // are saved to the temporary folder when they differ more
    // The native frame may differ from the fallback's by more than 12 on as many pixels as Skia on the GPU does
    // The allowance of a page on an adapter: with native text, as EdgeAllowances.ForText gives it
    private static int PageAllowance(GraphicsAdapter adapter, string page)
        => ReferenceViews.OtherGlyphsAllowance.TryGetValue(page, out var otherGlyphs)
            ? EdgeAllowances.ForText(adapter, ReferenceViews.EdgeAllowance[page], otherGlyphs)
            : EdgeAllowances.On(adapter, ReferenceViews.EdgeAllowance[page]);

    // Where Skia's glyphs aren't FreeType's, saves both frames of a comparison with native text with the renders CI uploads,
    // for a look by eye: two folders deeper than the renders CompareGold promotes to golds
    private static void SaveTextFrames(Image fallback, Image native, string name)
    {
        if (EdgeAllowances.UsesFreeType(Stride.Core.Platform.Type) || TestsFolder.Find() is not { } tests)
            return;

        var folder = Path.Combine(tests, "local", "Stride.AvaloniaUI.Native.Tests", "TextFrames", $"{Stride.Core.Platform.Type}.{GraphicsDevice.Platform}", name);
        Directory.CreateDirectory(folder);
        foreach (var (image, file) in new[] { (fallback, "fallback.png"), (native, "native.png") })
        {
            using var stream = File.Create(Path.Combine(folder, file));
            image.Save(stream, ImageFileType.Png);
        }
    }

    private static void AssertWithinAllowance(Image fallback, Image native, int allowance, string name)
    {
        const int Tolerance = 12;
        var beyond = FrameComparison.CountBeyond(fallback, native, Tolerance);
        if (beyond <= allowance)
            return;

        var folder = SaveFrames(fallback, native, name);
        Assert.Fail($"{name}: {beyond} pixels differ from the fallback by more than {Tolerance}, and the allowance is {allowance}; the frames are in {folder}.");
    }

    private static void AssertClose(Image expected, Image actual, int tolerance, string name)
    {
        var difference = FrameComparison.MaxDifference(expected, actual);
        if (difference <= tolerance)
            return;

        var folder = SaveFrames(expected, actual, name);
        Assert.Fail($"{name}: a channel differs by {difference}, more than {tolerance}; the frames are in {folder}.");
    }

    // Saves the expected and the actual frame as PNG files, and gives their folder
    private static string SaveFrames(Image expected, Image actual, string name)
    {
        var folder = Path.Combine(Path.GetTempPath(), "Stride.AvaloniaUI.Native.Tests");
        Directory.CreateDirectory(folder);
        foreach (var (image, suffix) in new[] { (expected, "expected"), (actual, "actual") })
        {
            using var stream = File.Create(Path.Combine(folder, $"{name}.{suffix}.png"));
            image.Save(stream, ImageFileType.Png);
        }
        return folder;
    }

    // The fallback of the game, whose canvases throw when asked to
    private sealed class ThrowingFallback(IRenderFallback fallback) : IRenderFallback
    {
        public bool Throws { get; set; }

        public bool ThrowsOnCreate { get; set; }

        public IFontManagerImpl FontManager => fallback.FontManager;

        public IGeometryImpl GetGeometry(StrideGeometry geometry) => fallback.GetGeometry(geometry);

        public IGlyphRunImpl GetGlyphRun(StrideGlyphRun glyphRun) => fallback.GetGlyphRun(glyphRun);

        public IGeometryImpl BuildGlyphRunGeometry(GlyphRun glyphRun) => fallback.BuildGlyphRunGeometry(glyphRun);

        public IFallbackCanvas CreateCanvas(FallbackTarget target)
        {
            if (ThrowsOnCreate)
                throw new Thrown();
            var canvas = fallback.CreateCanvas(target);
            return Throws ? new ThrowingCanvas(canvas) : canvas;
        }

        public sealed class Thrown() : Exception("A canvas of the test throws.");

        private sealed class ThrowingCanvas(IFallbackCanvas canvas) : IFallbackCanvas
        {
            public void Draw(DrawCommandList commands)
            {
                if (commands.Commands.Count > 0)
                    throw new Thrown();
            }

            public object? GetFeature(Type featureType) => canvas.GetFeature(featureType);

            public void Dispose() => canvas.Dispose();
        }
    }

    // A framebuffer of premultiplied memory that counts its locks and unlocks
    private sealed class CountingFramebuffer : IFramebufferRenderTarget
    {
        private readonly byte[] pixels = GC.AllocateArray<byte>(64 * 64 * 4, pinned: true);

        public int Locks { get; private set; }

        public int Unlocks { get; private set; }

        public bool RetainsFrameContents => true;

        public ILockedFramebuffer Lock(IRenderTarget.RenderTargetSceneInfo sceneInfo, out FramebufferLockProperties properties)
        {
            Locks++;
            properties = new FramebufferLockProperties(false);
            return new LockedFramebuffer(System.Runtime.InteropServices.Marshal.UnsafeAddrOfPinnedArrayElement(pixels, 0), sceneInfo.Size, sceneInfo.Size.Width * 4,
                new Vector(96, 96), AvaloniaPixelFormat.Rgba8888, AlphaFormat.Premul, () => Unlocks++);
        }

        public void Dispose()
        {
        }
    }
}
