// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.PlayMode.Protocol;

namespace Stride.PlayMode.Agent;

/// <summary>
/// What the pause controller changes in a game: its time factor, its fixed step, its elapsed time and input, and the parts it
/// stops while paused. <see cref="PlayGamePauseHost"/> for a running game; a fake in tests.
/// </summary>
internal interface IPlayPauseHost
{
    /// <summary>The factor of the game time (the game's update time factor).</summary>
    double TimeFactor { get; set; }

    /// <summary>Whether the game's window is active: the audio of an inactive game stays paused (the audio system's rule).</summary>
    bool IsActive { get; }

    /// <summary>Makes the next tick run exactly one update with the game's fixed step, its elapsed time not counted.</summary>
    /// <returns><c>false</c> when the game cannot be made to: the step then runs with the tick's own elapsed time.</returns>
    bool BeginSingleStep();

    /// <summary>Gives the game back its own fixed step settings after <see cref="BeginSingleStep"/>.</summary>
    void EndSingleStep();

    /// <summary>Makes the next tick's elapsed time zero, so that the time paused does not come back as one long frame.</summary>
    void ResetElapsedTime();

    /// <summary>Releases the keys the game's input still holds down (the window lost the key up while the editor had the focus).</summary>
    void ReleaseInput();

    /// <summary>
    /// Frees a locked mouse and shows the cursor, and remembers how they were. Called at each pause, and again after each step
    /// (a script that ran in the step may have locked the mouse again: then it is locked at resume).
    /// </summary>
    void FreeMouse();

    /// <summary>
    /// Gives the game back the mouse lock and the cursor as they were before <see cref="FreeMouse"/>. A game whose window is not
    /// active (the user resumed it from the editor) gets its lock back when its window is activated.
    /// </summary>
    void RestoreMouse();

    /// <summary>The parts of the game to stop while paused, as of now (systems and processors come and go).</summary>
    IEnumerable<IPlayPausable> FindPausables();

    /// <summary>Writes a warning in the game's log (the editor's Output gets it).</summary>
    void Warn(string message);
}

/// <summary>
/// A part of the game the pause stops: a game system, an entity processor, the audio.
/// </summary>
internal interface IPlayPausable
{
    /// <summary>What it is, to find it again (a type's full name).</summary>
    string Key { get; }

    /// <summary>Whether it runs during a step (the scripts do; the audio and the input of the game's UI do not).</summary>
    bool RunsOnStep { get; }

    /// <summary>Stops it; does nothing when the game had stopped it itself (it is then not restarted).</summary>
    void Pause();

    /// <summary>Keeps it stopped while paused (the audio system restarts the audio when the window is activated).</summary>
    void Hold();

    /// <summary>Restarts what <see cref="Pause"/> stopped.</summary>
    void Resume(bool isActive);
}

/// <summary>
/// Pauses, resumes and steps a played game, and sets the speed of its time, for the play agent. Agent only (D9 A): the game
/// time factor goes to 0, and the parts that follow the real time are stopped by name (<see cref="IPlayPauseHost.FindPausables"/>).
/// </summary>
/// <remarks>
/// <para>
/// Called on the game thread: the commands and <see cref="OnUpdate"/> from the agent's update, the last of each update, and
/// <see cref="OnEndOfTick"/> from the agent's draw, the last of each frame. A pause takes effect at the next update; a step is
/// armed at the end of a frame (so that the frame drawn then does not move), runs in the next frame (one update with the fixed
/// step, and its draw, so that animations and particles move by one step too) and pauses again at the end of that frame.
/// </para>
/// <para>
/// The game's own time factor is kept: the time scale multiplies it. A game that writes the factor while paused defeats the
/// pause for an update; the controller writes 0 again and says so in the game's log, once per pause.
/// </para>
/// </remarks>
internal sealed class PlayPauseController(IPlayPauseHost host)
{
    /// <summary>The most steps that can wait to run.</summary>
    public const int MaxPendingSteps = 600;

    // How often the parts are looked for again while paused (a scene loaded by a step can bring new processors)
    private const int RescanPeriodUpdates = 30;

    private readonly List<IPlayPausable> pausables = [];
    // The factor last written by the controller; null when it leaves the factor to the game (playing at the game's speed)
    private double? written;
    private StepPhase step;
    private int updatesSinceEndOfTick;
    private int updatesSinceScan;
    private bool warnedWhilePaused;
    private bool warnedWhilePlaying;
    private bool singleStep;
    private bool warnedNoSingleStep;
    // Whether the last frame ended in a draw; when not, a step pauses again in its own update (no draw would show it)
    private bool lastTickDrawn = true;

    private enum StepPhase
    {
        // No step: playing, or paused and waiting
        None,

        // Armed at the end of a frame: the next update is the step
        Armed,

        // The step's update ran: the frame's draw follows, then the pause again
        Ran,
    }

    /// <summary>Whether the game is paused (a step in progress included).</summary>
    public bool IsPaused { get; private set; }

    /// <summary>Why the game is paused; <see cref="PlayPauseReason.None"/> while it plays.</summary>
    public PlayPauseReason PauseReason { get; private set; }

    /// <summary>The time scale; 1 by default.</summary>
    public double TimeScale { get; private set; } = 1;

    /// <summary>The game's own time factor, as it last set it (1 unless it sets it).</summary>
    public double GameTimeFactor { get; private set; } = 1;

    /// <summary>The updates that ran while not paused, steps included.</summary>
    public long PlayedFrames { get; private set; }

    /// <summary>The steps asked for that have not run yet (the one in progress included).</summary>
    public int StepsPending { get; private set; }

    /// <summary>Whether the state changed since <see cref="TakeChanged"/> (the agent then sends it).</summary>
    public bool Changed { get; private set; }

    /// <summary>Whether the update that just ran was a step (the agent then sends a snapshot at once).</summary>
    public bool StepRanThisUpdate { get; private set; }

    /// <summary>
    /// Returns whether the state changed since the last call, and forgets it.
    /// </summary>
    public bool TakeChanged()
    {
        var changed = Changed;
        Changed = false;
        return changed;
    }

    /// <summary>
    /// Pauses the game from its next update, and frees a locked mouse. A game that is paused stays so; a script's exception
    /// then becomes the reason, and the steps not run yet are dropped.
    /// </summary>
    /// <param name="reason">Why (the editor's Pause by default).</param>
    public void Pause(PlayPauseReason reason = PlayPauseReason.Editor)
    {
        if (reason == PlayPauseReason.None)
            throw new ArgumentOutOfRangeException(nameof(reason), reason, "A pause has a reason.");
        if (IsPaused)
        {
            // An exception in a step stops the stepping: the user sees it before anything else runs
            if (reason == PlayPauseReason.ScriptException && (PauseReason != reason || StepsPending > 1))
            {
                PauseReason = reason;
                StepsPending = Math.Min(StepsPending, step == StepPhase.None ? 0 : 1);
                Changed = true;
            }
            return;
        }
        ObserveFactor();
        IsPaused = true;
        PauseReason = reason;
        warnedWhilePaused = false;
        Scan();
        foreach (var pausable in pausables)
            pausable.Pause();
        WriteFactor(0);
        host.FreeMouse();
        Changed = true;
    }

    /// <summary>
    /// Resumes a paused game as it was: the parts stopped are restarted, the time runs again at the time scale, the time paused
    /// is not counted, and the keys held down are released. The steps not run yet are dropped.
    /// </summary>
    public void Resume()
    {
        if (!IsPaused)
            return;
        ObserveFactor();
        IsPaused = false;
        PauseReason = PlayPauseReason.None;
        StepsPending = 0;
        if (step == StepPhase.Armed)
            EndSingleStep();
        step = StepPhase.None;
        var isActive = host.IsActive;
        foreach (var pausable in pausables)
            pausable.Resume(isActive);
        pausables.Clear();
        host.ResetElapsedTime();
        host.ReleaseInput();
        host.RestoreMouse();
        ApplyPlayingFactor();
        Changed = true;
    }

    /// <summary>
    /// Runs updates with the fixed step, then stays paused; pauses a game that plays first.
    /// </summary>
    /// <param name="frames">How many updates.</param>
    public void Step(int frames = 1)
    {
        if (frames <= 0)
            return;
        Pause();
        StepsPending = Math.Min(MaxPendingSteps, StepsPending + frames);
        Changed = true;
    }

    /// <summary>
    /// Sets the speed of the game time (a multiple of the game's own factor), from the next update; it is kept through pauses.
    /// </summary>
    /// <param name="scale">The speed; kept within [<see cref="PlayGameState.MinTimeScale"/>, <see cref="PlayGameState.MaxTimeScale"/>],
    /// 1 for anything that is not a number.</param>
    public void SetTimeScale(double scale)
    {
        scale = double.IsFinite(scale) ? Math.Clamp(scale, PlayGameState.MinTimeScale, PlayGameState.MaxTimeScale) : 1;
        if (scale == TimeScale)
            return;
        ObserveFactor();
        TimeScale = scale;
        if (!IsPaused || step != StepPhase.None)
            ApplyPlayingFactor();
        Changed = true;
    }

    /// <summary>
    /// Ends the pause and the time scale (the last subscriber left): the game goes on as without play mode.
    /// </summary>
    public void Release()
    {
        Resume();
        SetTimeScale(1);
    }

    /// <summary>
    /// At the end of each update (the agent's, which runs last): follows the game's own factor, counts the frames played, and
    /// keeps the paused parts stopped.
    /// </summary>
    public void OnUpdate()
    {
        updatesSinceEndOfTick++;
        StepRanThisUpdate = false;
        ObserveFactor();

        if (step == StepPhase.Armed)
        {
            // This update was the step: the frame's draw shows it, and the end of the frame pauses again
            step = StepPhase.Ran;
            StepRanThisUpdate = true;
            PlayedFrames++;
            EndSingleStep();
            Changed = true;
            if (!lastTickDrawn)
                EndOfTick(drawn: false);
        }
        else if (!IsPaused)
        {
            PlayedFrames++;
        }

        if (IsPaused && step == StepPhase.None)
        {
            if (++updatesSinceScan >= RescanPeriodUpdates)
                Scan(pauseNew: true);
            foreach (var pausable in pausables)
                pausable.Hold();
        }

        // A game that does not draw (minimized, or a tick of several updates) never ends its frames: end them here
        if (updatesSinceEndOfTick >= 2)
            EndOfTick(drawn: false);
    }

    /// <summary>
    /// At the end of each frame (the agent's draw, which runs last): pauses again after a step, and arms the next one.
    /// </summary>
    public void OnEndOfTick() => EndOfTick(drawn: true);

    private void EndOfTick(bool drawn)
    {
        updatesSinceEndOfTick = 0;
        lastTickDrawn = drawn;
        if (step == StepPhase.Ran)
        {
            step = StepPhase.None;
            StepsPending = Math.Max(0, StepsPending - 1);
            foreach (var pausable in pausables)
            {
                if (pausable.RunsOnStep)
                    pausable.Pause();
            }
            WriteFactor(0);
            // A script of the step may have locked the mouse again
            host.FreeMouse();
            Changed = true;
        }
        if (IsPaused && step == StepPhase.None && StepsPending > 0)
        {
            // The next update is the step: one fixed step of game time at the game's own factor (not slowed by the time scale,
            // so that each step ticks the physics once), and the parts that run on a step
            step = StepPhase.Armed;
            Scan(pauseNew: true);
            foreach (var pausable in pausables)
            {
                if (pausable.RunsOnStep)
                    pausable.Resume(host.IsActive);
            }
            singleStep = host.BeginSingleStep();
            if (!singleStep && !warnedNoSingleStep)
            {
                warnedNoSingleStep = true;
                host.Warn("Play mode cannot force one update of the fixed step in this game (its engine differs): a step runs one update with the frame's elapsed time.");
            }
            WriteFactor(GameTimeFactor);
        }
    }

    /// <summary>
    /// The state, for a <see cref="PlayGameState"/> message.
    /// </summary>
    public PlayGameState CreateState(long frame, double framesPerSecond, int entityCount)
        => new(frame, PlayedFrames, IsPaused, TimeScale, GameTimeFactor, framesPerSecond, entityCount, StepsPending, PauseReason);

    private void EndSingleStep()
    {
        if (!singleStep)
            return;
        singleStep = false;
        host.EndSingleStep();
    }

    /// <summary>
    /// Reads the factor: while the controller leaves it to the game, it is the game's; otherwise a value other than the one
    /// written is the game's own, written since.
    /// </summary>
    private void ObserveFactor()
    {
        var factor = host.TimeFactor;
        if (written is not { } expected)
        {
            GameTimeFactor = factor;
            return;
        }
        if (factor == expected)
            return;

        GameTimeFactor = factor;
        if (IsPaused && step == StepPhase.None)
        {
            if (!warnedWhilePaused)
            {
                warnedWhilePaused = true;
                host.Warn($"The game set its time factor to {factor} while play mode paused it; play mode wrote 0 again. A game that writes the time factor itself (a slow motion, its own pause) can defeat the pause.");
            }
            WriteFactor(0);
        }
        else
        {
            if (!warnedWhilePlaying && TimeScale != 1)
            {
                warnedWhilePlaying = true;
                host.Warn($"The game sets its own time factor (to {factor}): play mode's time scale multiplies it.");
            }
            WriteFactor(step == StepPhase.None ? factor * TimeScale : factor);
        }
        Changed = true;
    }

    private void ApplyPlayingFactor()
    {
        if (TimeScale == 1 && !IsPaused)
        {
            // The game's speed: the factor is the game's again
            host.TimeFactor = GameTimeFactor;
            written = null;
            return;
        }
        WriteFactor(GameTimeFactor * TimeScale);
    }

    private void WriteFactor(double factor)
    {
        host.TimeFactor = factor;
        // Read back: the game time clamps what it is given
        written = host.TimeFactor;
    }

    private void Scan(bool pauseNew = false)
    {
        updatesSinceScan = 0;
        foreach (var found in host.FindPausables())
        {
            if (pausables.Exists(x => x.Key == found.Key))
                continue;
            pausables.Add(found);
            if (pauseNew)
                found.Pause();
        }
    }
}
