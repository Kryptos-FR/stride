// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

namespace Stride.PlayMode.Agent;

/// <summary>
/// The mouse of a played game, as the pause sees it: its lock and the cursor's visibility. The game's in a running game; a
/// fake in tests.
/// </summary>
internal interface IPlayMouse
{
    /// <summary>Raised on the game thread when the game's window is activated.</summary>
    event EventHandler<EventArgs>? Activated;

    /// <summary>Whether the mouse is locked to the game's window (a first-person game locks it to look around).</summary>
    bool IsLocked { get; }

    /// <summary>Whether the cursor shows over the game's window.</summary>
    bool IsVisible { get; set; }

    /// <summary>Whether the game's window is active (has the focus).</summary>
    bool IsWindowActive { get; }

    /// <summary>Locks the mouse, with the cursor in the middle of the window.</summary>
    void Lock();

    /// <summary>Unlocks the mouse.</summary>
    void Unlock();
}

/// <summary>
/// Frees the mouse of a paused game and gives it back at resume, as the game had it: a first-person game locks the mouse to
/// its window and hides the cursor, so that a paused one would keep the user out of the editor.
/// </summary>
/// <remarks>
/// A game resumed from the editor has no active window: a lock taken then would hold the pointer in a window the user is not
/// in (on Windows, the pointer is confined to it). Its lock comes back when its window is activated, unless the game took the
/// mouse back itself, or was paused again first.
/// </remarks>
internal sealed class PlayMouseKeeper(IPlayMouse mouse)
{
    // How the mouse was before the pause freed it; null while the game has it
    private (bool Locked, bool Visible)? saved;
    private bool relockWhenActive;

    /// <summary>Whether the mouse is freed (between <see cref="Free"/> and <see cref="Restore"/>).</summary>
    public bool IsFreed => saved is not null;

    /// <summary>Whether the lock comes back when the game's window is activated.</summary>
    public bool RelocksWhenActive => relockWhenActive;

    /// <summary>
    /// Unlocks the mouse and shows the cursor, and remembers how they were. Called again while freed (after a step whose
    /// scripts locked it again), it keeps that lock for <see cref="Restore"/>.
    /// </summary>
    public void Free()
    {
        // Paused again before the window was activated: the game still wants its lock
        var wantsLock = relockWhenActive;
        if (relockWhenActive)
        {
            relockWhenActive = false;
            mouse.Activated -= OnActivated;
        }
        var locked = mouse.IsLocked;
        wantsLock |= locked;
        var visible = mouse.IsVisible;
        if (saved is not { } previous)
            saved = (wantsLock, visible);
        else if (wantsLock || !visible)
            saved = (previous.Locked || wantsLock, previous.Visible && visible);
        if (locked)
            mouse.Unlock();
        if (!visible)
            mouse.IsVisible = true;
    }

    /// <summary>
    /// Gives the mouse back as it was before <see cref="Free"/>: the cursor at once, and the lock at once in an active window,
    /// or when the window is activated.
    /// </summary>
    public void Restore()
    {
        if (saved is not { } previous)
            return;
        saved = null;
        mouse.IsVisible = previous.Visible;
        if (!previous.Locked)
            return;
        if (mouse.IsWindowActive)
        {
            mouse.Lock();
            return;
        }
        if (!relockWhenActive)
        {
            relockWhenActive = true;
            mouse.Activated += OnActivated;
        }
    }

    private void OnActivated(object? sender, EventArgs e)
    {
        mouse.Activated -= OnActivated;
        if (!relockWhenActive)
            return;
        relockWhenActive = false;
        if (!mouse.IsLocked)
            mouse.Lock();
    }
}
