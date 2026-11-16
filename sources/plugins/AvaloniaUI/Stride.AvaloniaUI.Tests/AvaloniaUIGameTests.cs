// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
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
/// Runs a game with an Avalonia view on the GPU: the view is uploaded and drawn over the scene, and takes its clicks.
/// </summary>
public sealed class AvaloniaUIGameTests : GameTestBase
{
    private const int Width = 400;
    private const int Height = 300;

    private static readonly Color BackgroundColor = new(32, 64, 96, 255);
    private static readonly Color ButtonColor = new(255, 128, 0, 255);

    // A game registers the plugins it references before it creates its systems; the test runner loads the plugin lazily
    static AvaloniaUIGameTests() => RuntimeHelpers.RunModuleConstructor(typeof(AvaloniaUISystem).Module.ModuleHandle);

    public AvaloniaUIGameTests()
    {
        GraphicsDeviceManager.PreferredBackBufferWidth = Width;
        GraphicsDeviceManager.PreferredBackBufferHeight = Height;
    }

    [Fact]
    public void ViewIsDrawnOverTheSceneAndTakesItsClicks()
    {
        AvaloniaThread.Run(() =>
        {
            var game = new AvaloniaUIGameTests();
            var clicks = 0;
            var button = new Button
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
            };
            button.Click += (_, _) => clicks++;

            var component = new AvaloniaUIComponent { Content = button, Resolution = new Vector2(Width, Height) };
            var scene = new Scene();
            scene.Entities.Add(new Entity("UI") { component });
            game.SceneSystem.SceneInstance = new SceneInstance(game.Services, scene);
            game.SceneSystem.GraphicsCompositor = new GraphicsCompositor { Game = new ClearRenderer { Color = BackgroundColor } };

            Image? frame = null;
            AvaloniaUISystem? system = null;
            Avalonia.Controls.TopLevel? topLevel = null;
            var gameSawThePress = false;
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
                .Draw(14, () => frame = game.GraphicsDevice.Presenter.BackBuffer.GetDataAsImage(game.GraphicsContext.CommandList));

            RunGameTest(game);

            // The system comes from its [GameSystem] declaration, as in any game
            Assert.NotNull(system);
            Assert.NotNull(topLevel);
            // The view goes with the scene
            Assert.Null(component.TopLevel);
            Assert.Equal(1, clicks);
            Assert.False(gameSawThePress);

            Assert.NotNull(frame);
            using (frame)
            {
                // The scene shows where no control is, the button is drawn over it
                AssertColor(BackgroundColor, frame, 5, 5);
                AssertColor(BackgroundColor, frame, Width - 5, Height - 5);
                AssertColor(ButtonColor, frame, Width / 2 - 60, Height / 2);
            }
        });
    }

    private static void AssertColor(Color expected, Image image, int x, int y)
    {
        var pixel = image.PixelBuffer[0].GetPixel<Color>(x, y);
        if (image.Description.Format is PixelFormat.B8G8R8A8_UNorm or PixelFormat.B8G8R8A8_UNorm_SRgb)
            pixel = new Color(pixel.B, pixel.G, pixel.R, pixel.A);

        const int Tolerance = 3;
        Assert.True(Math.Abs(expected.R - pixel.R) <= Tolerance && Math.Abs(expected.G - pixel.G) <= Tolerance && Math.Abs(expected.B - pixel.B) <= Tolerance,
            $"Expected {expected} at ({x}, {y}), got {pixel}.");
    }
}
