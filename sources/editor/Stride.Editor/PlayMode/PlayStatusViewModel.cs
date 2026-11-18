// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using Stride.Core.Extensions;
using Stride.Core.Presentation.Services;
using Stride.Core.Presentation.ViewModels;
using Stride.Core.Translation;
using Stride.PlayMode.Protocol;

namespace Stride.Editor.PlayMode;

/// <summary>
/// The state of the game started with Start project, for the status bar: Connecting, Playing, Paused, Not connected, Not
/// responding or Stopped, while a play session exists.
/// </summary>
/// <remarks>
/// It follows the sessions of a <see cref="PlayModeService"/>; their events come on their reading tasks, and the state is
/// updated on the UI thread.
/// </remarks>
[Experimental("STREXP012")]
public sealed class PlayStatusViewModel : DispatcherViewModel
{
    private readonly PlayModeService playMode;
    private PlaySession? session;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlayStatusViewModel"/> class.
    /// </summary>
    /// <param name="serviceProvider">The services; a dispatcher among them.</param>
    /// <param name="playMode">The play sessions to follow.</param>
    public PlayStatusViewModel(IViewModelServiceProvider serviceProvider, PlayModeService playMode)
        : base(serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(playMode);
        this.playMode = playMode;
        playMode.SessionStarted += SessionStarted;
        if (playMode.Session is { } current)
            Follow(current);
    }

    /// <summary>
    /// Whether a play session exists: the status bar shows its state.
    /// </summary>
    public bool IsVisible => State != LiveTabState.NotPlaying;

    /// <summary>
    /// The state of the game.
    /// </summary>
    public LiveTabState State
    {
        get;
        private set => SetValue(ref field, value, nameof(State), nameof(IsVisible), nameof(IsPlaying), nameof(IsPaused), nameof(IsNotResponding), nameof(Text));
    } = LiveTabState.NotPlaying;

    /// <summary>
    /// Whether the game plays with the live connection.
    /// </summary>
    public bool IsPlaying => State == LiveTabState.Playing;

    /// <summary>
    /// Whether the game is paused.
    /// </summary>
    public bool IsPaused => State == LiveTabState.Paused;

    /// <summary>
    /// Whether the connected game has sent nothing for a few seconds.
    /// </summary>
    public bool IsNotResponding => State == LiveTabState.NotResponding;

    /// <summary>
    /// The state as the status bar shows it.
    /// </summary>
    public string Text => State switch
    {
        LiveTabState.Connecting => Tr._p("Play", "Game: Connecting"),
        LiveTabState.Playing => Tr._p("Play", "Game: Playing"),
        LiveTabState.Paused => string.Format(Tr._p("Play", "Game: {0}"), PauseDescription),
        LiveTabState.NotConnected => Tr._p("Play", "Game: Not connected"),
        LiveTabState.NotResponding => Tr._p("Play", "Game: Not responding"),
        LiveTabState.Stopped => ExitCode is { } code ? string.Format(Tr._p("Play", "Game: Stopped (exit code {0})"), code) : Tr._p("Play", "Game: Stopped"),
        _ => string.Empty,
    };

    /// <summary>
    /// Why the game is paused (<see cref="PlayPauseText.Describe"/>): "Paused", or "Paused: script exception in PlayerController".
    /// </summary>
    public string PauseDescription { get; private set => SetValue(ref field, value, nameof(PauseDescription), nameof(Text)); } = PlayPauseText.Describe(PlayPauseReason.Editor);

    /// <summary>
    /// Why the game runs without the live connection, for the tooltip; <c>null</c> otherwise.
    /// </summary>
    public string? Message { get; private set => SetValue(ref field, value); }

    /// <summary>
    /// The exit code of the game, once it exited.
    /// </summary>
    public int? ExitCode { get; private set => SetValue(ref field, value, nameof(ExitCode), nameof(Text)); }

    /// <inheritdoc/>
    public override void Destroy()
    {
        playMode.SessionStarted -= SessionStarted;
        Unfollow();
        base.Destroy();
    }

    private void SessionStarted(object? sender, PlaySession started) => Dispatcher.InvokeAsync(() => Follow(started)).Forget();

    private void Follow(PlaySession followed)
    {
        if (IsDestroyed || followed == session)
            return;
        Unfollow();
        session = followed;
        followed.StateChanged += SessionChanged;
        followed.RespondingChanged += SessionChanged;
        followed.GameStateChanged += SessionChanged;
        followed.ScriptExceptionReceived += SessionChanged;
        Update();
    }

    private void Unfollow()
    {
        if (session is null)
            return;
        session.StateChanged -= SessionChanged;
        session.RespondingChanged -= SessionChanged;
        session.GameStateChanged -= SessionChanged;
        session.ScriptExceptionReceived -= SessionChanged;
        session = null;
    }

    private void SessionChanged<T>(object? sender, T e)
    {
        if (sender == session)
            Dispatcher.InvokeAsync(Update).Forget();
    }

    private void Update()
    {
        if (session is not { } current)
            return;
        ExitCode = current.ExitCode;
        PauseDescription = PlayPauseText.Describe(current.PauseReason, current.LastScriptException, current.LaunchOptions.Hotkey);
        Message = current.State is PlaySessionState.Failed or PlaySessionState.VersionMismatch or PlaySessionState.WithoutAgent ? current.StateMessage : null;
        State = current.State switch
        {
            PlaySessionState.Starting => LiveTabState.Connecting,
            PlaySessionState.Connected => !current.IsResponding ? LiveTabState.NotResponding : current.IsPaused ? LiveTabState.Paused : LiveTabState.Playing,
            PlaySessionState.GameExited => LiveTabState.Stopped,
            _ => LiveTabState.NotConnected,
        };
    }
}
