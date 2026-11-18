// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core.Collections;
using Stride.Input;
using Stride.PlayMode.Agent;
using Stride.PlayMode.Protocol;
using FakeGame = Stride.PlayMode.Tests.PlayPauseControllerTests.FakeGame;
using FakeMouse = Stride.PlayMode.Tests.PlayPauseControllerTests.FakeMouse;

namespace Stride.PlayMode.Tests;

/// <summary>
/// Tests the key of the game's window, the mouse it frees, and the reasons of a pause, on the fake game loop of
/// <see cref="PlayPauseControllerTests"/>.
/// </summary>
public sealed class PlayGameHotkeyTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(16);

    private readonly FakeGame game = new();

    [Fact]
    public void APauseFreesTheLockedMouseAndResumeLocksItAgain()
    {
        // A first-person game: the mouse is locked and the cursor hidden
        game.Mouse.IsLocked = true;
        game.Mouse.IsVisible = false;
        game.Tick(Frame);

        game.Controller.Pause(PlayPauseReason.Hotkey);

        Assert.False(game.Mouse.IsLocked);
        Assert.True(game.Mouse.IsVisible);
        Assert.Equal(PlayPauseReason.Hotkey, game.Controller.PauseReason);
        Assert.Equal(PlayPauseReason.Hotkey, game.Controller.CreateState(1, 60, 0).PauseReason);

        game.Tick(Frame, times: 3);
        game.Controller.Resume();

        Assert.True(game.Mouse.IsLocked);
        Assert.False(game.Mouse.IsVisible);
        Assert.Equal(PlayPauseReason.None, game.Controller.PauseReason);
    }

    [Fact]
    public void AGameThatHadTheMouseFreeKeepsItFree()
    {
        game.Controller.Pause();
        game.Controller.Resume();

        Assert.False(game.Mouse.IsLocked);
        Assert.True(game.Mouse.IsVisible);
        Assert.Equal(0, game.Mouse.Locks);
    }

    [Fact]
    public void AGameResumedFromTheEditorGetsItsLockBackWhenItsWindowIsActivated()
    {
        game.Mouse.IsLocked = true;
        game.Controller.Pause();
        // The user went to the editor and resumed from there
        game.Mouse.IsWindowActive = false;
        game.Controller.Resume();

        Assert.False(game.Mouse.IsLocked);
        Assert.True(game.MouseKeeper.RelocksWhenActive);

        game.Mouse.Activate();

        Assert.True(game.Mouse.IsLocked);
        Assert.Equal(1, game.Mouse.Locks);
        Assert.Equal(0, game.Mouse.ActivatedHandlers);
    }

    [Fact]
    public void AGamePausedAgainBeforeItsWindowIsActivatedStillGetsItsLockAtTheNextResume()
    {
        game.Mouse.IsLocked = true;
        game.Controller.Pause();
        game.Mouse.IsWindowActive = false;
        game.Controller.Resume();

        game.Controller.Pause();
        game.Mouse.Activate();
        Assert.False(game.Mouse.IsLocked);
        Assert.Equal(0, game.Mouse.ActivatedHandlers);

        game.Controller.Resume();
        Assert.True(game.Mouse.IsLocked);
    }

    [Fact]
    public void AScriptThatLocksTheMouseInAStepHasItLockedAtResume()
    {
        game.Controller.Pause();
        // The step's script locks the mouse, as the first-person template does on a click
        game.SystemAction = () =>
        {
            if (game.Scripts.IsRunning)
                game.Mouse.IsLocked = true;
        };
        game.Controller.Step();
        game.Tick(Frame, times: 3);
        game.SystemAction = null;

        Assert.False(game.Mouse.IsLocked);

        game.Controller.Resume();
        Assert.True(game.Mouse.IsLocked);
    }

    [Fact]
    public void StartPausedRunsNoUpdateBeforeResumeOrStep()
    {
        // As the agent does when it is created, before the game's first update
        game.Controller.Pause(PlayPauseReason.StartPaused);
        game.Tick(Frame, times: 10);

        Assert.Equal(0, game.ScriptUpdates);
        Assert.Equal(0, game.Controller.PlayedFrames);
        Assert.Equal(TimeSpan.Zero, game.GameTime);
        Assert.Equal(PlayPauseReason.StartPaused, game.Controller.PauseReason);

        game.Controller.Step();
        game.Tick(Frame, times: 3);
        Assert.Equal(1, game.ScriptUpdates);
        // A step keeps the reason: the game has still not been resumed
        Assert.Equal(PlayPauseReason.StartPaused, game.Controller.PauseReason);

        game.Controller.Resume();
        game.Tick(Frame, times: 2);
        Assert.Equal(3, game.ScriptUpdates);
    }

    [Fact]
    public void AScriptExceptionWhilePausedBecomesTheReasonAndStopsTheSteps()
    {
        game.Controller.Pause();
        game.Controller.Step(5);
        game.Tick(Frame);

        game.Controller.Pause(PlayPauseReason.ScriptException);

        Assert.Equal(PlayPauseReason.ScriptException, game.Controller.PauseReason);
        Assert.True(game.Controller.StepsPending <= 1);
        game.Tick(Frame, times: 5);
        Assert.Equal(0, game.Controller.StepsPending);
        Assert.True(game.Controller.IsPaused);
        // Another reason does not replace the exception
        game.Controller.Pause(PlayPauseReason.Hotkey);
        Assert.Equal(PlayPauseReason.ScriptException, game.Controller.PauseReason);
    }

    [Fact]
    public void APauseHasAReason() => Assert.Throws<ArgumentOutOfRangeException>(() => game.Controller.Pause(PlayPauseReason.None));

    [Fact]
    public void TheKeyCallsBackOnItsFirstPressWithExactlyItsModifiers()
    {
        var presses = 0;
        var listener = Assert.IsType<PlayHotkeyListener>(PlayHotkeyListener.TryCreate(new PlayHotkey("f7"), () => presses++, _ => Assert.Fail("No warning expected.")));
        Assert.Equal(Keys.F7, listener.Key);

        listener.OnKey(Keys.F7, isDown: true, repeatCount: 0, Down());
        Assert.Equal(1, presses);

        // Held down (repeats), released, another key, or with a modifier the key does not have
        listener.OnKey(Keys.F7, isDown: true, repeatCount: 1, Down());
        listener.OnKey(Keys.F7, isDown: false, repeatCount: 0, Down());
        listener.OnKey(Keys.F8, isDown: true, repeatCount: 0, Down());
        listener.OnKey(Keys.F7, isDown: true, repeatCount: 0, Down(Keys.LeftShift));
        Assert.Equal(1, presses);
    }

    [Fact]
    public void AKeyWithModifiersNeedsThemHeld()
    {
        var presses = 0;
        var listener = PlayHotkeyListener.TryCreate(new PlayHotkey("F7", PlayHotkeyModifiers.Shift | PlayHotkeyModifiers.Ctrl), () => presses++, _ => { })!;

        listener.OnKey(Keys.F7, isDown: true, repeatCount: 0, Down());
        listener.OnKey(Keys.F7, isDown: true, repeatCount: 0, Down(Keys.LeftShift));
        Assert.Equal(0, presses);

        listener.OnKey(Keys.F7, isDown: true, repeatCount: 0, Down(Keys.RightShift, Keys.LeftCtrl));
        Assert.Equal(1, presses);
    }

    [Theory]
    [InlineData("Banana")]
    [InlineData("118")]
    [InlineData("None")]
    public void AKeyThatIsNotAStrideKeyIsWarnedAndTakesNothing(string name)
    {
        var warnings = new List<string>();

        Assert.Null(PlayHotkeyListener.TryCreate(new PlayHotkey(name), () => { }, warnings.Add));

        Assert.Contains(name, Assert.Single(warnings));
    }

    [Fact]
    public void NoKeyTakesNothing()
        => Assert.Null(PlayHotkeyListener.TryCreate(null, () => { }, _ => Assert.Fail("No warning expected.")));

    [Fact]
    public void TheMouseKeeperRestoresOnlyWhatItFreed()
    {
        var mouse = new FakeMouse { IsLocked = false, IsVisible = false };
        var keeper = new PlayMouseKeeper(mouse);

        keeper.Restore();
        Assert.False(mouse.IsVisible);

        keeper.Free();
        Assert.True(keeper.IsFreed);
        Assert.True(mouse.IsVisible);
        keeper.Restore();
        Assert.False(keeper.IsFreed);
        Assert.False(mouse.IsVisible);
        Assert.False(mouse.IsLocked);
    }

    private static Core.Collections.IReadOnlySet<Keys> Down(params Keys[] keys) => new ReadOnlySet<Keys>(new HashSet<Keys>(keys));
}
