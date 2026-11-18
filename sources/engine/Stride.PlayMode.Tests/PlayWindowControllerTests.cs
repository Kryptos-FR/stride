// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.PlayMode.Agent;

namespace Stride.PlayMode.Tests;

/// <summary>
/// Tests the game's window kept on top while the editor or the game is the active app, on a fake window and clock.
/// </summary>
public sealed class PlayWindowControllerTests : IDisposable
{
    private readonly FakeWindow window = new();
    private readonly List<string> warnings = [];
    private TimeSpan now;
    private PlayWindowController? controller;

    public void Dispose() => controller?.Dispose();

    [Fact]
    public void TheWindowIsOnTopWhileTheGameIsActive()
    {
        var windowController = Create(enabled: true);
        Assert.False(windowController.IsOnTop);

        window.Activate();

        Assert.True(windowController.IsOnTop);
        Assert.True(window.OnTop);
        Assert.Equal(1, window.Calls);
    }

    [Fact]
    public void AWindowThatDoesNotSayItsFocusChangedIsReadEachFrame()
    {
        // A Windows Forms game raises no activation: only its focus changes
        var windowController = Create(enabled: true);
        window.SetFocusSilently(true);
        Assert.False(window.OnTop);

        windowController.Update();
        Assert.True(window.OnTop);

        // The user switches from the game to a browser
        window.SetFocusSilently(false);
        windowController.Update();
        Assert.True(window.OnTop);
        now += PlayWindowController.ReleaseDelay;
        windowController.Update();

        Assert.False(window.OnTop);
        Assert.False(windowController.IsOnTop);
        Assert.Equal(2, window.Calls);
    }

    [Fact]
    public void GoingFromTheGameToTheEditorKeepsTheWindowOnTopWithoutDroppingIt()
    {
        var windowController = Create(enabled: true);
        window.Activate();

        // The game is deactivated first, the editor's message arrives a few frames later
        window.Deactivate();
        now += TimeSpan.FromMilliseconds(100);
        windowController.Update();
        windowController.SetEditorActive(true);
        now += TimeSpan.FromSeconds(5);
        windowController.Update();

        Assert.True(window.OnTop);
        // Never dropped in between
        Assert.Equal(1, window.Calls);
    }

    [Fact]
    public void AnotherAppComingForwardLetsTheWindowGoAfterTheDelay()
    {
        var windowController = Create(enabled: true);
        windowController.SetEditorActive(true);
        Assert.True(window.OnTop);

        // The user switches from the editor to a browser
        windowController.SetEditorActive(false);
        now += PlayWindowController.ReleaseDelay - TimeSpan.FromMilliseconds(1);
        windowController.Update();
        Assert.True(window.OnTop);

        now += TimeSpan.FromMilliseconds(1);
        windowController.Update();
        Assert.False(window.OnTop);
        Assert.False(windowController.IsOnTop);

        // Back to the editor: on top again at once
        windowController.SetEditorActive(true);
        Assert.True(window.OnTop);
    }

    [Fact]
    public void ThePinTurnsItOffAtOnceAndOnAgain()
    {
        var windowController = Create(enabled: true);
        windowController.SetEditorActive(true);

        windowController.SetEnabled(false);
        Assert.False(window.OnTop);

        // While off, the activations change nothing
        window.Activate();
        windowController.SetEditorActive(true);
        Assert.False(window.OnTop);

        windowController.SetEnabled(true);
        Assert.True(window.OnTop);
    }

    [Fact]
    public void OffAtTheLaunchTheWindowStaysAmongTheOthers()
    {
        Create(enabled: false);

        window.Activate();
        controller!.SetEditorActive(true);

        Assert.False(window.OnTop);
        Assert.Equal(0, window.Calls);
    }

    [Fact]
    public void ReleasedTheWindowGoesBackAndStays()
    {
        var windowController = Create(enabled: true);
        window.Activate();

        windowController.Release();

        Assert.False(window.OnTop);
        Assert.True(windowController.IsReleased);
        windowController.SetEditorActive(true);
        windowController.SetEnabled(true);
        Assert.False(window.OnTop);
    }

    [Fact]
    public void AWindowThatCannotBeOnTopIsToldOnceAndLeftAlone()
    {
        window.Supported = false;
        var windowController = Create(enabled: true);

        window.Activate();
        window.Deactivate();
        windowController.SetEditorActive(true);

        Assert.False(windowController.IsOnTop);
        Assert.Single(warnings);
        Assert.Equal(1, window.Calls);
    }

    [Fact]
    public void DisposedItFollowsTheWindowNoMore()
    {
        var windowController = Create(enabled: true);
        windowController.Dispose();

        Assert.Equal(0, window.Handlers);
    }

    private PlayWindowController Create(bool enabled)
    {
        controller = new PlayWindowController(window, enabled, () => now, warnings.Add);
        return controller;
    }

    private sealed class FakeWindow : IPlayWindow
    {
        public event EventHandler<EventArgs>? Activated;

        public event EventHandler<EventArgs>? Deactivated;

        public bool IsActive { get; private set; }

        public bool OnTop { get; private set; }

        public bool Supported { get; set; } = true;

        public int Calls { get; private set; }

        public int Handlers => (Activated?.GetInvocationList().Length ?? 0) + (Deactivated?.GetInvocationList().Length ?? 0);

        public bool TrySetOnTop(bool onTop)
        {
            Calls++;
            if (!Supported)
                return false;
            OnTop = onTop;
            return true;
        }

        public void Activate()
        {
            IsActive = true;
            Activated?.Invoke(this, EventArgs.Empty);
        }

        public void SetFocusSilently(bool focused) => IsActive = focused;

        public void Deactivate()
        {
            IsActive = false;
            Deactivated?.Invoke(this, EventArgs.Empty);
        }
    }
}
