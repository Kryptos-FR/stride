// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.PlayMode.Agent;
using Stride.PlayMode.Protocol;

namespace Stride.PlayMode.Tests;

/// <summary>
/// Tests <see cref="PlayPauseController"/> on a fake game loop that runs its updates and draws as a Stride game does: the time
/// factor is read at the start of each update and copied to the draw, the agent's update is the last of each update, and its
/// draw the last of each frame.
/// </summary>
public sealed class PlayPauseControllerTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(16);

    private readonly FakeGame game = new();

    [Fact]
    public void PauseStopsTheGameTimeTheScriptsAndTheAudio()
    {
        game.Tick(Frame, times: 3);
        Assert.Equal(3, game.ScriptUpdates);
        var time = game.GameTime;
        var drawn = game.AnimationTime;

        game.Controller.Pause();
        game.Tick(Frame, times: 5);

        Assert.True(game.Controller.IsPaused);
        Assert.Equal(0, game.TimeFactor);
        Assert.Equal(time, game.GameTime);
        Assert.Equal(drawn, game.AnimationTime);
        Assert.Equal(3, game.ScriptUpdates);
        Assert.Equal(3, game.SpriteFrames);
        Assert.False(game.Audio.IsRunning);
        // The game goes on drawing, and the frames played are those before the pause
        Assert.Equal(8, game.Draws);
        Assert.Equal(3, game.Controller.PlayedFrames);
        Assert.True(game.Controller.TakeChanged());
    }

    [Fact]
    public void ResumeDoesNotReplayThePauseAndReleasesTheKeys()
    {
        game.Tick(Frame);
        game.Controller.Pause();
        game.Tick(TimeSpan.FromMilliseconds(100), times: 10);
        var time = game.GameTime;

        game.Controller.Resume();
        Assert.Equal(1, game.InputReleases);
        Assert.Equal(1, game.TimeFactor);
        // The first frame after the resume counts no time: the pause does not come back as one long frame
        game.Tick(TimeSpan.FromMilliseconds(100));
        Assert.Equal(time, game.GameTime);
        game.Tick(Frame);
        Assert.Equal(time + Frame, game.GameTime);
        Assert.True(game.Audio.IsRunning);
        Assert.Equal(3, game.ScriptUpdates);
        Assert.False(game.Controller.IsPaused);
    }

    [Fact]
    public void AfterResumeAtTheGameSpeedTheFactorIsTheGameOwn()
    {
        game.Controller.Pause();
        game.Tick(Frame);
        game.Controller.Resume();
        game.Tick(Frame);

        // The game slows itself down: play mode no longer writes the factor, and says nothing
        game.TimeFactor = 0.5;
        game.Tick(Frame);

        Assert.Equal(0.5, game.TimeFactor);
        Assert.Equal(0.5, game.Controller.GameTimeFactor);
        Assert.Empty(game.Warnings);
    }

    [Fact]
    public void StepRunsExactlyOneUpdateWithTheFixedStepThenPausesAgain()
    {
        game.Tick(Frame);
        game.Controller.Pause();
        game.Tick(Frame);
        var time = game.GameTime;
        var drawn = game.AnimationTime;

        game.Controller.Step();
        // The step is armed at the end of this frame: nothing moves in it
        game.Tick(TimeSpan.FromMilliseconds(100));
        Assert.Equal(time, game.GameTime);
        Assert.Equal(drawn, game.AnimationTime);
        Assert.Equal(1, game.Controller.StepsPending);

        // The step: one update of the fixed step (not the frame's 100 ms), the scripts once, and its draw
        game.Tick(TimeSpan.FromMilliseconds(100));
        Assert.Equal(time + game.FixedStep, game.GameTime);
        Assert.Equal(drawn + game.FixedStep, game.AnimationTime);
        Assert.Equal(2, game.ScriptUpdates);
        Assert.Equal(2, game.SpriteFrames);
        Assert.Equal(2, game.Controller.PlayedFrames);
        Assert.False(game.SingleStep);

        // Paused again, the audio never played
        Assert.True(game.Controller.IsPaused);
        Assert.Equal(0, game.Controller.StepsPending);
        Assert.Equal(0, game.TimeFactor);
        Assert.False(game.Audio.IsRunning);
        Assert.Equal(0, game.Audio.Resumes);
        game.Tick(TimeSpan.FromMilliseconds(100), times: 3);
        Assert.Equal(time + game.FixedStep, game.GameTime);
        Assert.Equal(2, game.ScriptUpdates);
    }

    [Fact]
    public void StepWhilePlayingPausesFirst()
    {
        game.Tick(Frame);
        game.Controller.Step(2);
        Assert.True(game.Controller.IsPaused);

        game.Tick(Frame, times: 6);

        Assert.Equal(Frame + 2 * game.FixedStep, game.GameTime);
        Assert.Equal(3, game.ScriptUpdates);
        Assert.Equal(0, game.Controller.StepsPending);
    }

    [Fact]
    public void AStepEndsInAGameThatDoesNotDraw()
    {
        game.Controller.Pause();
        game.Tick(Frame);
        game.Controller.Step();

        // Minimized: no draw, so the frames end in the updates
        game.Tick(Frame, times: 6, draw: false);

        Assert.Equal(1, game.ScriptUpdates);
        Assert.Equal(game.FixedStep, game.GameTime);
        Assert.Equal(0, game.TimeFactor);
        Assert.Equal(0, game.Controller.StepsPending);
    }

    [Fact]
    public void AStepTakesTheFrameElapsedTimeWhenTheGameCannotForceOneUpdate()
    {
        game.SupportsSingleStep = false;
        game.Controller.Pause();
        game.Tick(Frame);
        game.Controller.Step();
        game.Tick(Frame);

        game.Tick(TimeSpan.FromMilliseconds(40));

        Assert.Equal(TimeSpan.FromMilliseconds(40), game.GameTime);
        Assert.Equal(1, game.ScriptUpdates);
        // Said once
        game.Controller.Step();
        game.Tick(Frame, times: 3);
        Assert.Contains("fixed step", Assert.Single(game.Warnings), StringComparison.Ordinal);
    }

    [Fact]
    public void ResumeDropsTheStepsNotRun()
    {
        game.Controller.Step(5);
        game.Tick(Frame, times: 2);
        game.Controller.Resume();

        game.Tick(Frame, times: 2);

        Assert.Equal(0, game.Controller.StepsPending);
        Assert.False(game.SingleStep);
        Assert.Equal(1, game.TimeFactor);
    }

    [Fact]
    public void TheTimeScaleSlowsTheGameTimeAndIsKeptThroughAPause()
    {
        game.Controller.SetTimeScale(0.25);
        game.Tick(Frame);
        Assert.Equal(Frame * 0.25, game.GameTime);
        Assert.Equal(1, game.ScriptUpdates);

        game.Controller.Pause();
        game.Tick(Frame);
        game.Controller.Resume();
        game.Tick(Frame);
        game.Tick(Frame);

        Assert.Equal(0.25, game.TimeFactor);
        Assert.Equal(Frame * 0.5, game.GameTime);
        Assert.Equal(0.25, game.Controller.TimeScale);
    }

    [Fact]
    public void AStepIsOneFixedStepWhateverTheTimeScale()
    {
        // A slowed step would tick the physics only every few steps
        game.Controller.SetTimeScale(0.25);
        game.Controller.Step();
        game.Tick(Frame, times: 3);

        Assert.Equal(game.FixedStep, game.GameTime);
        Assert.True(game.Controller.IsPaused);
        Assert.Equal(0.25, game.Controller.TimeScale);
    }

    [Fact]
    public void TheTimeScaleMultipliesTheFactorTheGameSets()
    {
        game.Controller.SetTimeScale(0.5);
        game.Tick(Frame);

        game.TimeFactor = 0.8;
        game.Tick(Frame);
        game.Tick(Frame);

        Assert.Equal(0.4, game.TimeFactor);
        Assert.Equal(0.8, game.Controller.GameTimeFactor);
        Assert.Contains("multiplies", Assert.Single(game.Warnings));

        // Back to the game's speed: the factor is the game's
        game.Controller.SetTimeScale(1);
        Assert.Equal(0.8, game.TimeFactor);
    }

    [Fact]
    public void TheTimeScaleIsKeptWithinItsBounds()
    {
        game.Controller.SetTimeScale(100);
        Assert.Equal(PlayGameState.MaxTimeScale, game.Controller.TimeScale);
        game.Controller.SetTimeScale(0);
        Assert.Equal(PlayGameState.MinTimeScale, game.Controller.TimeScale);
        game.Controller.SetTimeScale(double.NaN);
        Assert.Equal(1, game.Controller.TimeScale);
    }

    [Fact]
    public void AGameThatWritesTheFactorWhilePausedIsPausedAgainAndToldOnce()
    {
        game.Controller.Pause();
        game.Tick(Frame);

        // A game system of the game (the scripts do not run) sets the factor in each update
        game.SystemAction = () => game.TimeFactor = 1;
        game.Tick(Frame, times: 3);
        game.SystemAction = null;
        game.Tick(Frame);

        Assert.Equal(0, game.TimeFactor);
        Assert.Contains("while play mode paused it", Assert.Single(game.Warnings));
        Assert.Equal(1, game.Controller.GameTimeFactor);

        // Said again at the next pause only
        game.Controller.Resume();
        game.Controller.Pause();
        game.SystemAction = () => game.TimeFactor = 1;
        game.Tick(Frame);
        Assert.Equal(2, game.Warnings.Count);
    }

    [Fact]
    public void ReleaseEndsThePauseAndTheTimeScale()
    {
        game.Controller.SetTimeScale(2);
        game.Controller.Step(3);
        game.Tick(Frame);

        game.Controller.Release();

        Assert.False(game.Controller.IsPaused);
        Assert.Equal(1, game.Controller.TimeScale);
        Assert.Equal(1, game.TimeFactor);
        Assert.True(game.Scripts.IsRunning);
        Assert.True(game.Audio.IsRunning);
    }

    [Fact]
    public void APartThatAppearsWhilePausedIsPausedToo()
    {
        game.Controller.Pause();
        var late = new FakePausable("Late", runsOnStep: true);
        game.Pausables.Add(late);

        game.Tick(Frame, times: 40);

        Assert.False(late.IsRunning);
        game.Controller.Resume();
        Assert.True(late.IsRunning);
    }

    [Fact]
    public void TheAudioIsHeldWhilePausedAndPlaysAgainOnlyInAnActiveWindow()
    {
        game.Controller.Pause();
        // The audio system resumes the audio when the window is activated
        game.Audio.IsRunning = true;
        game.Tick(Frame);
        Assert.False(game.Audio.IsRunning);

        game.IsActive = false;
        game.Controller.Resume();
        Assert.False(game.Audio.IsRunning);
    }

    [Fact]
    public void TheStateHasTheCounters()
    {
        game.Tick(Frame, times: 2);
        game.Controller.SetTimeScale(0.5);
        game.Controller.Step(2);

        var state = game.Controller.CreateState(10, 59.5, 304);

        Assert.Equal(new PlayGameState(10, 2, true, 0.5, 1, 59.5, 304, 2, PlayPauseReason.Editor), state);
    }

    /// <summary>
    /// A part of the fake game the controller stops.
    /// </summary>
    internal sealed class FakePausable(string key, bool runsOnStep) : IPlayPausable
    {
        public string Key => key;

        public bool RunsOnStep => runsOnStep;

        public bool IsRunning { get; set; } = true;

        public int Resumes { get; private set; }

        public void Pause() => IsRunning = false;

        public void Hold() => IsRunning = false;

        public void Resume(bool isActive)
        {
            // As the audio: an inactive window stays silent
            if (key == "Audio" && !isActive)
                return;
            IsRunning = true;
            Resumes++;
        }
    }

    /// <summary>
    /// The mouse of the fake game.
    /// </summary>
    internal sealed class FakeMouse : IPlayMouse
    {
        public event EventHandler<EventArgs>? Activated;

        public bool IsLocked { get; set; }

        public bool IsVisible { get; set; } = true;

        public bool IsWindowActive { get; set; } = true;

        public int Locks { get; private set; }

        public void Lock()
        {
            IsLocked = true;
            Locks++;
        }

        public void Unlock() => IsLocked = false;

        /// <summary>The user brings the game's window to the front.</summary>
        public void Activate()
        {
            IsWindowActive = true;
            Activated?.Invoke(this, EventArgs.Empty);
        }

        public int ActivatedHandlers => Activated?.GetInvocationList().Length ?? 0;
    }

    /// <summary>
    /// A game loop with no graphics, as <c>GameBase</c> runs it with a variable step: one update per frame with the frame's
    /// elapsed time, or one update of the fixed step when a single step is asked for.
    /// </summary>
    internal sealed class FakeGame : IPlayPauseHost
    {
        private bool resetElapsed;

        public FakeGame()
        {
            Pausables = [Scripts, SpriteAnimation, Audio];
            MouseKeeper = new PlayMouseKeeper(Mouse);
            Controller = new PlayPauseController(this);
        }

        public FakeMouse Mouse { get; } = new();

        public PlayMouseKeeper MouseKeeper { get; }

        public PlayPauseController Controller { get; }

        public FakePausable Scripts { get; } = new("Scripts", runsOnStep: true);

        public FakePausable SpriteAnimation { get; } = new("SpriteAnimation", runsOnStep: true);

        public FakePausable Audio { get; } = new("Audio", runsOnStep: false);

        public List<IPlayPausable> Pausables { get; }

        public List<string> Warnings { get; } = [];

        public TimeSpan FixedStep { get; } = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60);

        public bool SupportsSingleStep { get; set; } = true;

        public bool SingleStep { get; private set; }

        public TimeSpan GameTime { get; private set; }

        public TimeSpan AnimationTime { get; private set; }

        public int ScriptUpdates { get; private set; }

        public int SpriteFrames { get; private set; }

        public int Draws { get; private set; }

        public int InputReleases { get; private set; }

        /// <summary>A game system of the game, run in each update before the agent.</summary>
        public Action? SystemAction { get; set; }

        public double TimeFactor { get; set => field = value > 0 ? value : 0; } = 1;

        public bool IsActive { get; set; } = true;

        public void Tick(TimeSpan elapsed, int times = 1, bool draw = true)
        {
            for (var i = 0; i < times; i++)
                Tick(elapsed, draw);
        }

        public bool BeginSingleStep()
        {
            SingleStep = SupportsSingleStep;
            return SupportsSingleStep;
        }

        public void EndSingleStep() => SingleStep = false;

        public void ResetElapsedTime() => resetElapsed = true;

        public void ReleaseInput() => InputReleases++;

        public void FreeMouse() => MouseKeeper.Free();

        public void RestoreMouse() => MouseKeeper.Restore();

        public IEnumerable<IPlayPausable> FindPausables() => Pausables;

        public void Warn(string message) => Warnings.Add(message);

        private void Tick(TimeSpan elapsed, bool draw)
        {
            if (resetElapsed)
            {
                elapsed = TimeSpan.Zero;
                resetElapsed = false;
            }
            var perUpdate = SingleStep ? FixedStep : elapsed;

            // The update: the factor is read at its start
            var factor = TimeFactor;
            GameTime += perUpdate * factor;
            if (Scripts.IsRunning)
                ScriptUpdates++;
            SystemAction?.Invoke();
            Controller.OnUpdate();

            if (!draw)
                return;
            // The draw copies the update's factor (animations and particles), and the sprite animation reads the raw time
            AnimationTime += perUpdate * TimeFactor;
            if (SpriteAnimation.IsRunning)
                SpriteFrames++;
            Draws++;
            Controller.OnEndOfTick();
        }
    }
}
