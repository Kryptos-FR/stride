// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Stride.AvaloniaUI.Platform;
using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Graphics;
using Stride.Graphics.Regression;
using Stride.Input;
using Stride.Rendering.Compositing;
using Xunit;
using Color = Stride.Core.Mathematics.Color;
using Image = Stride.Graphics.Image;

namespace Stride.AvaloniaUI.Tests;

/// <summary>
/// Runs a game with an Avalonia view on the GPU: Skia renders the view on the game's device (or on the CPU, and the
/// frame is uploaded), the game draws it over the scene, and the view takes its clicks.
/// </summary>
public sealed class AvaloniaUIGameTests : GameTestBase
{
    private const int Width = 400;
    private const int Height = 300;

    private static readonly Color BackgroundColor = new(32, 64, 96, 255);
    private static readonly Color ButtonColor = new(255, 128, 0, 255);

    // A game registers the plugins it references before it creates its systems; the test runner loads the plugin lazily
    static AvaloniaUIGameTests() => RuntimeHelpers.RunModuleConstructor(typeof(AvaloniaUISystem).Module.ModuleHandle);

    // Skia renders on the game's device only when it is a Vulkan one
    private static bool SkiaSharesTheDevice => GraphicsDevice.Platform == GraphicsPlatform.Vulkan;

    public AvaloniaUIGameTests()
    {
        GraphicsDeviceManager.PreferredBackBufferWidth = Width;
        GraphicsDeviceManager.PreferredBackBufferHeight = Height;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ViewIsDrawnOverTheSceneAndTakesItsClicks(bool renderOnGpu)
    {
        AvaloniaThread.Run(() =>
        {
            var run = RunGame(renderOnGpu);

            // The system comes from its [GameSystem] declaration, as in any game
            Assert.NotNull(run.System);
            Assert.NotNull(run.TopLevel);
            // The view goes with the scene
            Assert.Null(run.Component.TopLevel);
            Assert.Equal(1, run.Clicks);
            Assert.False(run.GameSawThePress);
            // Skia shares a Vulkan device (Lavapipe on CI); with the other graphics APIs it renders on the CPU
            Assert.Equal(renderOnGpu && SkiaSharesTheDevice, run.RenderedOnGpu);

            using var frame = run.Frame;
            // The scene shows where no control is, the button is drawn over it
            AssertColor(BackgroundColor, frame, 5, 5);
            AssertColor(BackgroundColor, frame, Width - 5, Height - 5);
            AssertColor(ButtonColor, frame, Width / 2 - 60, Height / 2);
        });
    }

    [SkippableFact]
    public void SkiaDrawsTheSameFrameOnTheGpuAsOnTheCpu()
    {
        SkipUnlessSkiaSharesTheDevice();
        AvaloniaThread.Run(() =>
        {
            using var cpuFrame = RunGame(renderOnGpu: false).Frame;
            using var gpuFrame = RunGame(renderOnGpu: true).Frame;

            // Skia's GPU backend anti-aliases the edges of shapes a little differently from its CPU one; colors that are
            // off (sRGB decoded twice or not at all, subpixel text) would change the whole button and its text
            const int Tolerance = 12;
            const int MaxEdgePixels = 100;
            var edgePixels = 0;
            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    if (Difference(GetPixel(cpuFrame, x, y), GetPixel(gpuFrame, x, y)) > Tolerance)
                        edgePixels++;
                }
            }
            Assert.True(edgePixels <= MaxEdgePixels, $"{edgePixels} pixels of the GPU frame are more than {Tolerance} away from the CPU frame.");
        });

        static int Difference(Color a, Color b) => Math.Max(Math.Abs(a.R - b.R), Math.Max(Math.Abs(a.G - b.G), Math.Abs(a.B - b.B)));
    }

    [SkippableFact]
    public void ViewIsRedrawnWhenItMovesBetweenTheGpuAndTheCpu()
    {
        SkipUnlessSkiaSharesTheDevice();
        AvaloniaThread.Run(() =>
        {
            var (game, component) = CreateGame(CreateButton(), renderOnGpu: true);
            var system = game.Services.GetSafeServiceAs<AvaloniaUISystem>();
            StrideTopLevelImpl? impl = null;
            bool? onGpuFirst = null, onGpuAfterDetach = null, onGpuAfterAttach = null;
            Image? cpuFrame = null, gpuFrame = null;

            // Nothing changes in the view once it is drawn: only the move redraws it
            game.FrameGameSystem.IsUnitTestFeeding = true;
            game.FrameGameSystem.DrawOrder = int.MaxValue;
            game.FrameGameSystem
                .Draw(6, () =>
                {
                    impl = (StrideTopLevelImpl)component.TopLevel!.PlatformImpl!;
                    onGpuFirst = impl.GpuFrame != null;
                })
                .Update(8, () => system.RenderOnGpu = false)
                .Draw(10, () =>
                {
                    onGpuAfterDetach = impl!.GpuFrame != null || system.IsRenderingOnGpu;
                    cpuFrame = game.GraphicsDevice.Presenter.BackBuffer.GetDataAsImage(game.GraphicsContext.CommandList);
                })
                .Update(12, () => system.RenderOnGpu = true)
                .Draw(14, () =>
                {
                    onGpuAfterAttach = impl!.GpuFrame != null && system.IsRenderingOnGpu;
                    gpuFrame = game.GraphicsDevice.Presenter.BackBuffer.GetDataAsImage(game.GraphicsContext.CommandList);
                });

            RunGameTest(game);

            Assert.True(onGpuFirst);
            Assert.False(onGpuAfterDetach);
            Assert.True(onGpuAfterAttach);
            using (cpuFrame)
            using (gpuFrame)
            {
                foreach (var frame in new[] { cpuFrame!, gpuFrame! })
                {
                    AssertColor(BackgroundColor, frame, 5, 5);
                    AssertColor(ButtonColor, frame, Width / 2 - 60, Height / 2);
                }
            }
        });
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
        Background = new SolidColorBrush(Avalonia.Media.Color.FromRgb(ButtonColor.R, ButtonColor.G, ButtonColor.B)),
        // The theme's press animation runs on the wall clock, which the frames of a test don't follow
        Transitions = [],
    };

    /// <summary>
    /// Creates a game that shows a view over a cleared scene.
    /// </summary>
    private static (AvaloniaUIGameTests Game, AvaloniaUIComponent Component) CreateGame(Control content, bool renderOnGpu)
    {
        var game = new AvaloniaUIGameTests();
        var component = new AvaloniaUIComponent { Content = content, Resolution = new Vector2(Width, Height) };
        var scene = new Scene();
        scene.Entities.Add(new Entity("UI") { component });
        game.SceneSystem.SceneInstance = new SceneInstance(game.Services, scene);
        game.SceneSystem.GraphicsCompositor = new GraphicsCompositor { Game = new ClearRenderer { Color = BackgroundColor } };
        game.Services.GetSafeServiceAs<AvaloniaUISystem>().RenderOnGpu = renderOnGpu;
        return (game, component);
    }

    /// <summary>
    /// Runs a game that shows a button over a cleared scene, clicks it, and reads the back buffer.
    /// </summary>
    private static GameRun RunGame(bool renderOnGpu)
    {
        var clicks = 0;
        var button = CreateButton();
        button.Click += (_, _) => clicks++;
        var (game, component) = CreateGame(button, renderOnGpu);

        Image? frame = null;
        AvaloniaUISystem? system = null;
        Avalonia.Controls.TopLevel? topLevel = null;
        var gameSawThePress = false;
        var renderedOnGpu = false;
        game.FrameGameSystem.IsUnitTestFeeding = true;
        // Read the back buffer once everything else is drawn
        game.FrameGameSystem.DrawOrder = int.MaxValue;
        game.FrameGameSystem
            .Update(5, () =>
            {
                system = game.Services.GetService<AvaloniaUISystem>();
                topLevel = component.TopLevel;
                game.MouseSimulated.SetPosition(new Vector2(0.5f, 0.5f));
            })
            .Update(7, () => game.MouseSimulated.SimulateMouseDown(MouseButton.Left))
            .Update(8, () => gameSawThePress = game.Input.IsMouseButtonDown(MouseButton.Left))
            .Update(9, () => game.MouseSimulated.SimulateMouseUp(MouseButton.Left))
            // Away from the button, so it shows its own color and not the pointer-over one
            .Update(10, () => game.MouseSimulated.SetPosition(new Vector2(0.02f, 0.02f)))
            .Draw(14, () =>
            {
                renderedOnGpu = ((StrideTopLevelImpl)topLevel!.PlatformImpl!).GpuFrame != null;
                frame = game.GraphicsDevice.Presenter.BackBuffer.GetDataAsImage(game.GraphicsContext.CommandList);
            });

        RunGameTest(game);

        Assert.NotNull(frame);
        return new GameRun(component, system, topLevel, clicks, gameSawThePress, renderedOnGpu, frame);
    }

    private sealed record GameRun(AvaloniaUIComponent Component, AvaloniaUISystem? System, Avalonia.Controls.TopLevel? TopLevel,
        int Clicks, bool GameSawThePress, bool RenderedOnGpu, Image Frame);

    private static Color GetPixel(Image image, int x, int y)
    {
        var pixel = image.PixelBuffer[0].GetPixel<Color>(x, y);
        return image.Description.Format is PixelFormat.B8G8R8A8_UNorm or PixelFormat.B8G8R8A8_UNorm_SRgb
            ? new Color(pixel.B, pixel.G, pixel.R, pixel.A)
            : pixel;
    }

    private static void SkipUnlessSkiaSharesTheDevice() =>
        Skip.IfNot(SkiaSharesTheDevice, $"Skia renders on the GPU with Vulkan only, and this game runs on {GraphicsDevice.Platform}.");

    private static void AssertColor(Color expected, Image image, int x, int y)
    {
        var pixel = GetPixel(image, x, y);
        const int Tolerance = 3;
        Assert.True(Math.Abs(expected.R - pixel.R) <= Tolerance && Math.Abs(expected.G - pixel.G) <= Tolerance && Math.Abs(expected.B - pixel.B) <= Tolerance,
            $"Expected {expected} at ({x}, {y}), got {pixel}.");
    }
}
