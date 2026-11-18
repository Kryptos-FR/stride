// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

namespace Stride.PlayMode.Agent;

/// <summary>
/// The window of a played game, as the play mode keeps it on top: its activation, and the OS's "always on top". The game's in
/// a running game; a fake in tests.
/// </summary>
internal interface IPlayWindow
{
    /// <summary>
    /// Raised on the game thread when the game's window is activated. A window may not raise it (a Windows Forms game): the
    /// controller also reads <see cref="IsActive"/> each frame.
    /// </summary>
    event EventHandler<EventArgs>? Activated;

    /// <summary>Raised on the game thread when the game's window is deactivated.</summary>
    event EventHandler<EventArgs>? Deactivated;

    /// <summary>Whether the game's window is active (has the focus). Read on the game thread, each frame.</summary>
    bool IsActive { get; }

    /// <summary>
    /// Puts the window above the windows of the other apps, or back among them. Called on the game thread.
    /// </summary>
    /// <returns><c>false</c> when this window cannot be kept on top.</returns>
    bool TrySetOnTop(bool onTop);
}

/// <summary>
/// Keeps the window of a played game on top while the editor or the game is the active app, so that a click in the editor does
/// not send the game behind it; when the user switches to another app, the window goes back among the others.
/// </summary>
/// <remarks>
/// <para>
/// The editor says when its main window is activated and deactivated (<see cref="SetEditorActive"/>), and the play toolbar's
/// pin turns the whole thing on and off (<see cref="SetEnabled"/>). Going from the game to the editor deactivates the game
/// before the editor's message arrives: the window stays on top for <see cref="ReleaseDelay"/> before it goes back, so that it
/// does not drop behind the editor and come back.
/// </para>
/// <para>
/// Used on the game thread only.
/// </para>
/// </remarks>
internal sealed class PlayWindowController : IDisposable
{
    private readonly IPlayWindow window;
    private readonly Func<TimeSpan> clock;
    private readonly Action<string>? warn;
    // When the window should have stopped being on top; null while it should be, or is not
    private TimeSpan? releaseSince;
    // The window's activation the last evaluation saw: a window that does not say when it changes is read each frame
    private bool wasActive;
    private bool failed;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlayWindowController"/> class.
    /// </summary>
    /// <param name="window">The game's window.</param>
    /// <param name="enabled">Whether the window is kept on top (the launch option).</param>
    /// <param name="clock">The time, for <see cref="ReleaseDelay"/>.</param>
    /// <param name="warn">Says that the window cannot be kept on top; called once.</param>
    public PlayWindowController(IPlayWindow window, bool enabled, Func<TimeSpan> clock, Action<string>? warn = null)
    {
        this.window = window;
        this.clock = clock;
        this.warn = warn;
        IsEnabled = enabled;
        window.Activated += OnActivationChanged;
        window.Deactivated += OnActivationChanged;
        Evaluate();
    }

    /// <summary>
    /// How long the window stays on top once neither the editor nor the game is active: the time it takes the editor's
    /// activation to arrive.
    /// </summary>
    public static TimeSpan ReleaseDelay { get; } = TimeSpan.FromMilliseconds(500);

    /// <summary>Whether the window is kept on top while the editor or the game is active (the pin of the play toolbar).</summary>
    public bool IsEnabled { get; private set; }

    /// <summary>Whether the editor's main window is active, as the editor last said.</summary>
    public bool IsEditorActive { get; private set; }

    /// <summary>Whether the window is on top now.</summary>
    public bool IsOnTop { get; private set; }

    /// <summary>Whether nobody can change the state any more (<see cref="Release"/>): the window stays among the others.</summary>
    public bool IsReleased { get; private set; }

    /// <summary>
    /// Turns keeping the window on top on or off.
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        IsEnabled = enabled;
        Evaluate();
    }

    /// <summary>
    /// Says whether the editor's main window is active.
    /// </summary>
    public void SetEditorActive(bool active)
    {
        IsEditorActive = active;
        Evaluate();
    }

    /// <summary>
    /// Follows the window's activation when the window did not say it changed, and lets the window go back among the others
    /// once <see cref="ReleaseDelay"/> passed with neither the editor nor the game active. Called each frame.
    /// </summary>
    public void Update()
    {
        if (window.IsActive != wasActive)
            Evaluate();
        if (releaseSince is { } since && clock() - since >= ReleaseDelay)
            Apply(false);
    }

    /// <summary>
    /// Nobody can resume or follow the game any more (the editor left): the window goes back among the others at once, and
    /// stays there.
    /// </summary>
    public void Release()
    {
        IsReleased = true;
        Evaluate();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        window.Activated -= OnActivationChanged;
        window.Deactivated -= OnActivationChanged;
    }

    private void OnActivationChanged(object? sender, EventArgs e) => Evaluate();

    private void Evaluate()
    {
        wasActive = window.IsActive;
        if (!IsEnabled || IsReleased)
        {
            // Turned off: at once
            Apply(false);
            return;
        }
        if (IsEditorActive || window.IsActive)
        {
            Apply(true);
            return;
        }
        // Another app came forward, or the editor's activation is on its way
        if (IsOnTop && releaseSince is null)
            releaseSince = clock();
    }

    private void Apply(bool onTop)
    {
        releaseSince = null;
        if (onTop == IsOnTop || failed)
            return;
        if (!window.TrySetOnTop(onTop))
        {
            failed = true;
            warn?.Invoke("Play mode cannot keep this game's window on top: its window does not support it.");
            return;
        }
        IsOnTop = onTop;
    }
}
