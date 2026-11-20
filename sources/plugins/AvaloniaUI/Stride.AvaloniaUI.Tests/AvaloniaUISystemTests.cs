// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform;
using Avalonia.Platform.Surfaces;
using Avalonia.Rendering.Composition;
using Stride.AvaloniaUI.Platform;
using Stride.Input;
using Stride.Rendering;
using Xunit;
using AvaloniaKey = Avalonia.Input.Key;
using AvaloniaKeyEventArgs = Avalonia.Input.KeyEventArgs;
using InputElement = Avalonia.Input.InputElement;
using Vector2 = Stride.Core.Mathematics.Vector2;

namespace Stride.AvaloniaUI.Tests;

/// <summary>
/// Checks the Avalonia UI of a game: its frames, its layout on the back buffer, and the input it takes from the game.
/// </summary>
public sealed class AvaloniaUISystemTests
{
    // A corner where no control is
    private static readonly Vector2 EmptySpace = new(0.02f, 0.02f);

    [Fact]
    public void ViewRendersOnlyItsControls()
    {
        AvaloniaThread.Run(() =>
        {
            using var game = new HeadlessAvaloniaGame();
            var button = new Button { Content = "Play", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var view = game.Show(button);

            Assert.True(view.IsShown);
            Assert.True(view.Impl.TryGetFrame(out var data, out var size, out var rowPitch));
            Assert.Equal(new PixelSize(game.Width, game.Height), size);
            Assert.Equal(0, AlphaAt(data, rowPitch, 2, 2));
            Assert.NotEqual(0, AlphaAt(data, rowPitch, game.Width / 2, game.Height / 2));
        });
    }

    [Fact]
    public void FrameOfNoPixelsIsNotShown()
    {
        TestAvalonia.Run(() =>
        {
            using var impl = new StrideTopLevelImpl();
            var target = ((IFramebufferPlatformSurface)impl).CreateFramebufferRenderTarget();

            // A renderer that draws nothing still releases the frame it locked
            target.Lock(new IRenderTarget.RenderTargetSceneInfo(new PixelSize(0, 0), 1, CompositionTransparencyLevel.None), out _).Dispose();

            Assert.False(impl.TryGetFrame(out _, out _, out _));
        });
    }

    [Fact]
    public void ClickOnAButtonClicksItAndHidesTheMouseFromTheGame()
    {
        AvaloniaThread.Run(() =>
        {
            using var game = new HeadlessAvaloniaGame();
            var clicks = 0;
            var button = new Button { Content = "Play", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            button.Click += (_, _) => clicks++;
            game.Show(button);

            game.MoveTo(button);
            Assert.Same(game.System, game.Mouse.CaptureState.Owner);
            Assert.Equal(InputCapturePriority.Hover, game.Mouse.CaptureState.Priority);

            game.Mouse.SimulateMouseDown(MouseButton.Left);
            game.Frame();
            Assert.False(game.Input.IsMouseButtonPressed(MouseButton.Left));
            game.Mouse.SimulateMouseUp(MouseButton.Left);
            game.Frame();

            Assert.Equal(1, clicks);
        });
    }

    [Fact]
    public void MouseWhereNoControlIsStaysWithTheGame()
    {
        AvaloniaThread.Run(() =>
        {
            using var game = new HeadlessAvaloniaGame();
            var button = new Button { Content = "Play", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            game.Show(button);

            game.MoveTo(button);
            game.MoveTo(EmptySpace);
            Assert.False(game.Mouse.CaptureState.IsCaptured);

            game.Mouse.SimulateMouseDown(MouseButton.Left);
            game.Frame();
            Assert.True(game.Input.IsMouseButtonPressed(MouseButton.Left));
        });
    }

    [Fact]
    public void GameDragAcrossTheUIStaysWithTheGame()
    {
        AvaloniaThread.Run(() =>
        {
            using var game = new HeadlessAvaloniaGame();
            var clicks = 0;
            var button = new Button { Content = "Play", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            button.Click += (_, _) => clicks++;
            game.Show(button);

            game.MoveTo(EmptySpace);
            game.Mouse.SimulateMouseDown(MouseButton.Left);
            game.Frame();
            game.MoveTo(button);
            Assert.False(game.Mouse.CaptureState.IsCaptured);
            Assert.True(game.Input.IsMouseButtonDown(MouseButton.Left));

            game.Mouse.SimulateMouseUp(MouseButton.Left);
            game.Frame();
            Assert.Equal(0, clicks);
        });
    }

    [Fact]
    public void TypingInAFocusedTextBoxReachesItButNotTheGame()
    {
        AvaloniaThread.Run(() =>
        {
            using var game = new HeadlessAvaloniaGame();
            var textBox = new TextBox { Text = "Strider", Width = 200, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            game.Show(textBox);

            game.MoveTo(textBox);
            game.Click();
            Assert.True(textBox.IsFocused);
            Assert.Same(game.System, game.Keyboard.CaptureState.Owner);
            Assert.Equal(InputCapturePriority.Focus, game.Keyboard.CaptureState.Priority);

            textBox.CaretIndex = textBox.Text!.Length;
            game.Keyboard.SimulateDown(Keys.Back);
            game.Frame();
            Assert.False(game.Input.IsKeyDown(Keys.Back));
            game.Keyboard.SimulateUp(Keys.Back);
            game.Frame();

            Assert.Equal("Stride", textBox.Text);
        });
    }

    [Fact]
    public void ClickInTheGameGivesTheKeyboardBack()
    {
        AvaloniaThread.Run(() =>
        {
            using var game = new HeadlessAvaloniaGame();
            var textBox = new TextBox { Width = 200, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            game.Show(textBox);
            game.MoveTo(textBox);
            game.Click();
            Assert.True(textBox.IsFocused);

            game.MoveTo(EmptySpace);
            game.Click();
            Assert.False(textBox.IsFocused);
            Assert.False(game.Keyboard.CaptureState.IsCaptured);

            game.Keyboard.SimulateDown(Keys.W);
            game.Frame();
            Assert.True(game.Input.IsKeyDown(Keys.W));
        });
    }

    [Fact]
    public void KeysGoToAFocusedButtonWithoutTakingTheKeyboard()
    {
        AvaloniaThread.Run(() =>
        {
            using var game = new HeadlessAvaloniaGame();
            var button = new Button { Content = "Play", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var keys = new List<AvaloniaKey>();
            button.AddHandler(InputElement.KeyDownEvent, (object? _, AvaloniaKeyEventArgs e) => keys.Add(e.Key), RoutingStrategies.Tunnel);
            game.Show(button);
            game.MoveTo(button);
            game.Click();

            game.Keyboard.SimulateDown(Keys.Space);
            game.Frame();

            Assert.Equal([AvaloniaKey.Space], keys);
            Assert.False(game.Keyboard.CaptureState.IsCaptured);
            Assert.True(game.Input.IsKeyDown(Keys.Space));
        });
    }

    [Fact]
    public void TopViewGetsTheClick()
    {
        AvaloniaThread.Run(() =>
        {
            using var game = new HeadlessAvaloniaGame();
            int bottomClicks = 0, topClicks = 0;
            var bottom = new Button { Content = "Bottom", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            bottom.Click += (_, _) => bottomClicks++;
            var top = new Button { Content = "Top", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            top.Click += (_, _) => topClicks++;
            game.Show(top, drawOrder: 1);
            game.Show(bottom, drawOrder: 0);

            game.MoveTo(top);
            game.Click();

            Assert.Equal(1, topClicks);
            Assert.Equal(0, bottomClicks);
        });
    }

    [Fact]
    public void DisabledComponentHidesTheViewAndReleasesTheMouse()
    {
        AvaloniaThread.Run(() =>
        {
            using var game = new HeadlessAvaloniaGame();
            var button = new Button { Content = "Play", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var view = game.Show(button);
            game.MoveTo(button);
            Assert.True(game.Mouse.CaptureState.IsCaptured);

            view.Component.Enabled = false;
            game.MoveTo(button);

            Assert.False(view.IsShown);
            Assert.False(game.Mouse.CaptureState.IsCaptured);
        });
    }

    [Theory]
    [InlineData(ResolutionStretch.FixedWidthAdaptableHeight, 2.0, 200.0, 150.0, 400, 300)]
    [InlineData(ResolutionStretch.FixedHeightAdaptableWidth, 3.0, 133.33, 100.0, 400, 300)]
    [InlineData(ResolutionStretch.FixedWidthFixedHeight, 2.0, 200.0, 100.0, 400, 200)]
    public void ResolutionStretchScalesTheView(ResolutionStretch stretch, double scaling, double width, double height, int pixelWidth, int pixelHeight)
    {
        AvaloniaThread.Run(() =>
        {
            using var game = new HeadlessAvaloniaGame(400, 300);
            var component = new AvaloniaUIComponent { Content = new Border(), Resolution = new Vector2(200, 100), ResolutionStretch = stretch };
            var view = game.Show(component);

            Assert.Equal(scaling, view.Impl.RenderScaling, 3);
            Assert.Equal(width, view.Impl.ClientSize.Width, 2);
            Assert.Equal(height, view.Impl.ClientSize.Height, 2);
            Assert.True(view.Impl.TryGetFrame(out _, out var size, out _));
            Assert.Equal(new PixelSize(pixelWidth, pixelHeight), size);
        });
    }

    [Fact]
    public void ViewTypeCreatesTheView()
    {
        AvaloniaThread.Run(() =>
        {
            using var game = new HeadlessAvaloniaGame();
            var component = new AvaloniaUIComponent { ViewType = typeof(TestView).AssemblyQualifiedName };
            game.Show(component);

            Assert.IsType<TestView>(component.Content);
            Assert.Same(component.TopLevel, TopLevel.GetTopLevel(component.Content));
        });
    }

    private static unsafe byte AlphaAt(nint data, int rowPitch, int x, int y) => ((byte*)data)[y * rowPitch + x * 4 + 3];

    public sealed class TestView : UserControl
    {
        public TestView() => Content = new TextBlock { Text = "Main menu" };
    }
}
