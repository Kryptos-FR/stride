// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using Stride.Core.Extensions;
using Stride.Core.Presentation.Commands;
using Stride.Core.Presentation.Services;
using Stride.Core.Presentation.ViewModels;
using Stride.Core.Translation;
using Stride.PlayMode.Protocol;

namespace Stride.Editor.PlayMode;

/// <summary>
/// Pause, Step and the time scale of the game started with Start project, and its frame rate: the Live tab's toolbar and the
/// commands of the main window (Pause game, Step one frame) share one instance.
/// </summary>
/// <remarks>
/// It follows the sessions of a <see cref="PlayModeService"/>; their events come on their reading tasks, and the state is
/// updated on the UI thread. A new game starts at the game's own speed (a time scale of 1).
/// </remarks>
[Experimental("STREXP012")]
public sealed class PlayControlsViewModel : DispatcherViewModel
{
    private readonly PlayModeService playMode;
    // The time scale is being set from the game's state: not sent back to it
    private bool updating;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlayControlsViewModel"/> class.
    /// </summary>
    /// <param name="serviceProvider">The services; a dispatcher among them.</param>
    /// <param name="playMode">The play sessions to follow.</param>
    public PlayControlsViewModel(IViewModelServiceProvider serviceProvider, PlayModeService playMode)
        : base(serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(playMode);
        this.playMode = playMode;
        PauseCommand = new AnonymousCommand(serviceProvider, TogglePause) { IsEnabled = false };
        StepCommand = new AnonymousCommand(serviceProvider, Step) { IsEnabled = false };
        playMode.SessionStarted += SessionStarted;
        playMode.WindowOnTopChanged += WindowOnTopChanged;
        if (playMode.Session is { } current)
            Follow(current);
    }

    /// <summary>
    /// The time scales offered, from slow motion to fast forward.
    /// </summary>
    public static IReadOnlyList<double> TimeScales { get; } = [0.1, 0.25, 0.5, 1, 2, 4];

    /// <summary>
    /// Whether a connected game can be paused, stepped and slowed down.
    /// </summary>
    public bool CanControl
    {
        get;
        private set => SetValue(ref field, value, () => PauseCommand.IsEnabled = StepCommand.IsEnabled = value);
    }

    /// <summary>
    /// Whether the game is paused.
    /// </summary>
    public bool IsPaused { get; private set => SetValue(ref field, value, nameof(IsPaused), nameof(PauseText)); }

    /// <summary>
    /// What <see cref="PauseCommand"/> does now: Pause, or Resume while paused.
    /// </summary>
    public string PauseText => IsPaused ? Tr._p("Play", "Resume") : Tr._p("Play", "Pause");

    /// <summary>
    /// The speed of the game time, a multiple of the game's own (1). Setting it sends it to the game.
    /// </summary>
    public double TimeScale
    {
        get;
        set
        {
            if (!double.IsFinite(value) || value <= 0)
                return;
            SetValue(ref field, value, () =>
            {
                if (!updating)
                    Session?.SetTimeScale(value);
            });
        }
    } = 1;

    /// <summary>
    /// Whether the game's window stays on top while the editor or the game is the active app (the toolbar's pin): it applies to
    /// the game that plays at once, and to the next ones (<see cref="PlayModeService.WindowOnTop"/>).
    /// </summary>
    public bool IsWindowOnTop
    {
        get => playMode.WindowOnTop;
        set => playMode.WindowOnTop = value;
    }

    /// <summary>
    /// The tooltip of the pin.
    /// </summary>
    public string WindowOnTopText => IsWindowOnTop
        ? Tr._p("Play", "The game window stays on top while the editor or the game is the active app. Click to let it go behind the editor.")
        : Tr._p("Play", "Keep the game window on top while the editor or the game is the active app.");

    /// <summary>
    /// The game's frame rate as of its last state (it goes on drawing while paused); 0 before the first.
    /// </summary>
    public double FramesPerSecond { get; private set => SetValue(ref field, value); }

    /// <summary>
    /// The entities of the running scenes, as of the game's last state.
    /// </summary>
    public int EntityCount { get; private set => SetValue(ref field, value); }

    /// <summary>
    /// The frames the game played (paused frames not counted, each step counts one), as of its last state.
    /// </summary>
    public long PlayedFrames { get; private set => SetValue(ref field, value); }

    /// <summary>
    /// Pauses the game, or resumes it while paused (Pause game, F7).
    /// </summary>
    public ICommandBase PauseCommand { get; }

    /// <summary>
    /// Runs one update of the game's fixed step, then stays paused; pauses a game that plays first (Step one frame, F10).
    /// </summary>
    public ICommandBase StepCommand { get; }

    /// <summary>
    /// The session followed.
    /// </summary>
    internal PlaySession? Session { get; private set; }

    /// <inheritdoc/>
    public override void Destroy()
    {
        playMode.SessionStarted -= SessionStarted;
        playMode.WindowOnTopChanged -= WindowOnTopChanged;
        Unfollow();
        base.Destroy();
    }

    private void WindowOnTopChanged(object? sender, EventArgs e) => Dispatcher.InvokeAsync(() => OnPropertyChanged(nameof(IsWindowOnTop), nameof(WindowOnTopText))).Forget();

    private void SessionStarted(object? sender, PlaySession started) => Dispatcher.InvokeAsync(() => Follow(started)).Forget();

    private void Follow(PlaySession followed)
    {
        if (IsDestroyed || followed == Session)
            return;
        Unfollow();
        Session = followed;
        followed.StateChanged += SessionChanged;
        followed.RespondingChanged += SessionChanged;
        followed.GameStateChanged += SessionChanged;
        Update();
    }

    private void Unfollow()
    {
        if (Session is null)
            return;
        Session.StateChanged -= SessionChanged;
        Session.RespondingChanged -= SessionChanged;
        Session.GameStateChanged -= SessionChanged;
        Session = null;
    }

    private void SessionChanged<T>(object? sender, T e)
    {
        if (sender == Session)
            Dispatcher.InvokeAsync(Update).Forget();
    }

    private void Update()
    {
        if (Session is not { } current)
            return;
        var connected = current.State == PlaySessionState.Connected;
        var state = connected ? current.GameState : null;
        CanControl = connected && current.IsResponding;
        IsPaused = state is { Paused: true };
        FramesPerSecond = state?.FramesPerSecond ?? 0;
        EntityCount = state?.EntityCount ?? 0;
        PlayedFrames = state?.PlayedFrames ?? 0;
        updating = true;
        try
        {
            TimeScale = state?.TimeScale ?? 1;
        }
        finally
        {
            updating = false;
        }
    }

    private void TogglePause()
    {
        if (Session is not { } current)
            return;
        if (IsPaused)
            current.Resume();
        else
            current.Pause();
    }

    private void Step() => Session?.Step();
}
