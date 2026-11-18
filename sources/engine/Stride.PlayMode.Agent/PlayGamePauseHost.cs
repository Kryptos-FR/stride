// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Reflection;
using System.Runtime.CompilerServices;
using Stride.Core.Diagnostics;
using Stride.Engine;
using Stride.Games;
using Stride.Input;

namespace Stride.PlayMode.Agent;

/// <summary>
/// The pause controller's view of a running game (<see cref="IPlayPauseHost"/>): its update time factor, its fixed step, and
/// the parts that keep moving under a time factor of 0, stopped by name.
/// </summary>
/// <remarks>
/// <para>
/// The parts stopped (the audit of play mode's pause, in the job's report): the script system (sync and async scripts), the
/// sprite animation system (it reads the raw elapsed time), the UI system and its input (a click on the game's UI would run the
/// game's code; the UI is frozen), the audio (music and sounds, streamed ones included), the video processor and the recast
/// navigation processor (raw elapsed time). The plugins are found by their type's name, so that the agent references none of
/// them; a game without one has nothing to stop there.
/// </para>
/// <para>
/// A step needs one update with the game's fixed step: the game's <c>ForceOneUpdatePerDraw</c> and its accumulated time are
/// not public, and are read and written by reflection (the agent always runs the editor's engine version, which the startup
/// hook checks).
/// </para>
/// </remarks>
internal sealed class PlayGamePauseHost(Game game, ILogger log) : IPlayPauseHost
{
    /// <summary>The game systems stopped while paused, by type name, and whether they run on a step.</summary>
    internal static readonly IReadOnlyList<(string TypeName, bool RunsOnStep)> PausedSystems =
    [
        ("Stride.UI.UISystem", false),
        ("Stride.Audio.AudioSystem", false),
    ];

    /// <summary>The entity processors stopped while paused, by type name (they all run on a step).</summary>
    internal static readonly IReadOnlyList<string> PausedProcessors =
    [
        "Stride.Video.VideoProcessor",
        "Stride.BepuPhysics.Navigation.Processors.RecastNavigationProcessor",
    ];

    private static readonly PropertyInfo? ForceOneUpdatePerDraw = typeof(GameBase).GetProperty("ForceOneUpdatePerDraw", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
    private static readonly FieldInfo? AccumulatedElapsedTime = typeof(GameBase).GetField("accumulatedElapsedGameTime", BindingFlags.Instance | BindingFlags.NonPublic);

    private (bool IsFixedTimeStep, bool ForceOneUpdatePerDraw, TimeSpan Accumulated)? savedStep;
    private readonly PlayMouseKeeper mouse = new(new GameMouse(game));

    /// <inheritdoc/>
    public double TimeFactor
    {
        get => game.UpdateTime.Factor;
        set => game.UpdateTime.Factor = value;
    }

    /// <inheritdoc/>
    public bool IsActive => game.IsActive;

    /// <inheritdoc/>
    public bool BeginSingleStep()
    {
        if (ForceOneUpdatePerDraw is null || AccumulatedElapsedTime is null)
            return false;
        savedStep ??= (game.IsFixedTimeStep, (bool)ForceOneUpdatePerDraw.GetValue(game)!, (TimeSpan)AccumulatedElapsedTime.GetValue(game)!);
        // One update of exactly the fixed step: the tick's elapsed time is not counted, and no catch up runs
        game.IsFixedTimeStep = true;
        ForceOneUpdatePerDraw.SetValue(game, true);
        game.ResetElapsedTime();
        return true;
    }

    /// <inheritdoc/>
    public void EndSingleStep()
    {
        if (savedStep is not { } saved)
            return;
        savedStep = null;
        game.IsFixedTimeStep = saved.IsFixedTimeStep;
        ForceOneUpdatePerDraw!.SetValue(game, saved.ForceOneUpdatePerDraw);
        // A game with its own fixed step goes on from where it was, without a catch up for the step
        AccumulatedElapsedTime!.SetValue(game, saved.Accumulated);
    }

    /// <inheritdoc/>
    public void ResetElapsedTime() => game.ResetElapsedTime();

    /// <inheritdoc/>
    public void ReleaseInput()
    {
        if (game.Input is not { } input)
            return;
        foreach (var keyboard in input.Keyboards)
        {
            if (keyboard is not KeyboardDeviceBase device || device.DownKeys.Count == 0)
                continue;
            foreach (var key in device.DownKeys.ToList())
                device.HandleKeyUp(key);
        }
    }

    /// <inheritdoc/>
    public void FreeMouse() => mouse.Free();

    /// <inheritdoc/>
    public void RestoreMouse() => mouse.Restore();

    /// <inheritdoc/>
    public IEnumerable<IPlayPausable> FindPausables()
    {
        yield return new UpdatePausable(game.Script, runsOnStep: true);
        yield return new DrawPausable(game.SpriteAnimation, runsOnStep: true);
        foreach (var system in game.GameSystems)
        {
            var name = system.GetType().FullName;
            switch (name)
            {
                case "Stride.UI.UISystem" when system is GameSystemBase ui:
                    yield return new UiPausable(ui, game.Input);
                    break;
                case "Stride.Audio.AudioSystem":
                    if (AudioPausable.TryCreate(system) is { } audio)
                        yield return audio;
                    break;
            }
        }
        if (game.SceneSystem.SceneInstance is { } scene)
        {
            foreach (var processor in scene.Processors)
            {
                if (PausedProcessors.Contains(processor.GetType().FullName))
                    yield return new ProcessorPausable(processor);
            }
        }
    }

    /// <inheritdoc/>
    public void Warn(string message) => log.Warning(message);

    private static string KeyOf(object target) => $"{target.GetType().FullName}#{RuntimeHelpers.GetHashCode(target)}";

    /// <summary>The game's mouse: its lock (the input manager's) and its cursor (the game's).</summary>
    private sealed class GameMouse(Game game) : IPlayMouse
    {
        public event EventHandler<EventArgs>? Activated
        {
            add => game.Activated += value;
            remove => game.Activated -= value;
        }

        public bool IsLocked => game.Input?.IsMousePositionLocked == true;

        public bool IsVisible
        {
            get => game.IsMouseVisible;
            set => game.IsMouseVisible = value;
        }

        public bool IsWindowActive => game.IsActive;

        public void Lock() => game.Input?.LockMousePosition(forceCenter: true);

        public void Unlock() => game.Input?.UnlockMousePosition();
    }

    /// <summary>A game system whose update stops.</summary>
    private sealed class UpdatePausable(GameSystemBase system, bool runsOnStep) : IPlayPausable
    {
        private bool paused;

        public string Key { get; } = KeyOf(system);

        public bool RunsOnStep => runsOnStep;

        public void Pause()
        {
            if (paused || !system.Enabled)
                return;
            system.Enabled = false;
            paused = true;
        }

        public void Hold()
        {
        }

        public void Resume(bool isActive)
        {
            if (!paused)
                return;
            system.Enabled = true;
            paused = false;
        }
    }

    /// <summary>A game system whose draw stops (the sprite animation advances in its draw, with the raw elapsed time).</summary>
    private sealed class DrawPausable(GameSystemBase system, bool runsOnStep) : IPlayPausable
    {
        private bool paused;

        public string Key { get; } = KeyOf(system);

        public bool RunsOnStep => runsOnStep;

        public void Pause()
        {
            if (paused || !system.Visible)
                return;
            system.Visible = false;
            paused = true;
        }

        public void Hold()
        {
        }

        public void Resume(bool isActive)
        {
            if (!paused)
                return;
            system.Visible = true;
            paused = false;
        }
    }

    /// <summary>An entity processor (its update and its draw stop).</summary>
    private sealed class ProcessorPausable(EntityProcessor processor) : IPlayPausable
    {
        private bool paused;

        public string Key { get; } = KeyOf(processor);

        public bool RunsOnStep => true;

        public void Pause()
        {
            if (paused || !processor.Enabled)
                return;
            processor.Enabled = false;
            paused = true;
        }

        public void Hold()
        {
        }

        public void Resume(bool isActive)
        {
            if (!paused)
                return;
            processor.Enabled = true;
            paused = false;
        }
    }

    /// <summary>
    /// The UI system: its update (key and text events) and its input stop, so that a click or a key does not reach the game's
    /// UI while paused, nor all at once at resume.
    /// </summary>
    private sealed class UiPausable(GameSystemBase system, InputManager? input) : IPlayPausable
    {
        private bool paused;

        public string Key { get; } = KeyOf(system);

        public bool RunsOnStep => false;

        public void Pause()
        {
            if (paused || !system.Enabled)
                return;
            system.Enabled = false;
            if (system is IInputEventListener listener)
                input?.RemoveListener(listener);
            paused = true;
        }

        public void Hold()
        {
        }

        public void Resume(bool isActive)
        {
            if (!paused)
                return;
            if (system is IInputEventListener listener)
                input?.AddListener(listener);
            system.Enabled = true;
            paused = false;
        }
    }

    /// <summary>
    /// The audio engine of the audio system: paused, and kept paused (the audio system resumes it when the window is activated).
    /// At resume it plays again only in an active window, as the audio system does.
    /// </summary>
    private sealed class AudioPausable : IPlayPausable
    {
        private readonly Func<object?> engine;
        private bool paused;

        private AudioPausable(object system, Func<object?> engine)
        {
            Key = KeyOf(system);
            this.engine = engine;
        }

        public string Key { get; }

        public bool RunsOnStep => false;

        public static AudioPausable? TryCreate(object system)
        {
            var property = system.GetType().GetProperty("AudioEngine", BindingFlags.Instance | BindingFlags.Public);
            return property is null ? null : new AudioPausable(system, () => property.GetValue(system));
        }

        public void Pause()
        {
            if (paused || engine() is not { } audio)
                return;
            switch (StateOf(audio))
            {
                case "Running":
                    Invoke(audio, "PauseAudio");
                    paused = true;
                    break;
                case "Paused":
                    // Paused by the audio system for an inactive window: play mode keeps it paused until its own resume
                    paused = true;
                    break;
            }
        }

        public void Hold()
        {
            if (paused && engine() is { } audio && StateOf(audio) == "Running")
                Invoke(audio, "PauseAudio");
        }

        public void Resume(bool isActive)
        {
            if (!paused)
                return;
            paused = false;
            if (isActive && engine() is { } audio && StateOf(audio) == "Paused")
                Invoke(audio, "ResumeAudio");
        }

        private static string? StateOf(object audio) => audio.GetType().GetProperty("State")?.GetValue(audio)?.ToString();

        private static void Invoke(object audio, string method) => audio.GetType().GetMethod(method, Type.EmptyTypes)?.Invoke(audio, null);
    }
}
