// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Reflection;
using Silk.NET.SDL;
using Stride.Engine;
using SdlWindow = Stride.Graphics.SDL.Window;

namespace Stride.PlayMode.Agent;

/// <summary>
/// The window of a running game, kept on top by the OS: SDL's "always on top" for an SDL window (Windows, Linux X11 and
/// XWayland, macOS), and the form's <c>TopMost</c> for a Windows Forms window.
/// </summary>
/// <remarks>
/// <para>
/// The engine's own <c>TopMost</c> of an SDL window only sets a hint; the agent calls <c>SDL_SetWindowAlwaysOnTop</c> itself
/// (SDL 2.0.16 and later), which sets the window manager's "above" state on X11 and the floating level on macOS.
/// </para>
/// <para>
/// A desktop game never raises <see cref="Game.Activated"/> and <see cref="Game.Deactivated"/> (only the mobile platforms do),
/// and its <see cref="Game.IsActive"/> stays <c>true</c>: the activation of the window comes from its focus instead. An SDL
/// window says when its focus changes; the focus of a Windows Forms window (<c>ContainsFocus</c>) is read each frame by
/// <see cref="PlayWindowController.Update"/>.
/// </para>
/// </remarks>
internal sealed class PlayGameWindow : IPlayWindow, IDisposable
{
    private readonly Game game;
    private readonly SdlWindow? sdl;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlayGameWindow"/> class.
    /// </summary>
    /// <param name="game">The started game, whose window exists.</param>
    public PlayGameWindow(Game game)
    {
        this.game = game;
        sdl = game.Window?.NativeWindow?.NativeWindow as SdlWindow;
        if (sdl is not null)
        {
            sdl.FocusGainedActions += OnFocusGained;
            sdl.FocusLostActions += OnFocusLost;
        }
        else
        {
            game.Activated += OnGameActivated;
            game.Deactivated += OnGameDeactivated;
        }
    }

    /// <inheritdoc/>
    public event EventHandler<EventArgs>? Activated;

    /// <inheritdoc/>
    public event EventHandler<EventArgs>? Deactivated;

    /// <inheritdoc/>
    public bool IsActive => sdl?.Focused ?? game.Window?.Focused ?? game.IsActive;

    /// <inheritdoc/>
    public void Dispose()
    {
        if (sdl is not null)
        {
            sdl.FocusGainedActions -= OnFocusGained;
            sdl.FocusLostActions -= OnFocusLost;
        }
        else
        {
            game.Activated -= OnGameActivated;
            game.Deactivated -= OnGameDeactivated;
        }
    }

    /// <inheritdoc/>
    public bool TrySetOnTop(bool onTop)
    {
        var native = sdl ?? game.Window?.NativeWindow?.NativeWindow;
        try
        {
            switch (native)
            {
                case SdlWindow sdl:
                    return SetSdlOnTop(sdl, onTop);
                case not null when native.GetType().GetProperty("TopMost", BindingFlags.Public | BindingFlags.Instance) is { CanWrite: true } property && property.PropertyType == typeof(bool):
                    // A Windows Forms window (the agent does not reference Windows Forms)
                    property.SetValue(native, onTop);
                    return true;
                default:
                    return false;
            }
        }
        catch (Exception)
        {
            // An SDL older than 2.0.16 has no such call; the play mode must not end the game for it
            return false;
        }
    }

    private void OnFocusGained(Silk.NET.SDL.WindowEvent e) => Activated?.Invoke(this, EventArgs.Empty);

    private void OnFocusLost(Silk.NET.SDL.WindowEvent e) => Deactivated?.Invoke(this, EventArgs.Empty);

    private void OnGameActivated(object? sender, EventArgs e) => Activated?.Invoke(this, e);

    private void OnGameDeactivated(object? sender, EventArgs e) => Deactivated?.Invoke(this, e);

    private static unsafe bool SetSdlOnTop(SdlWindow window, bool onTop)
    {
        var handle = (Silk.NET.SDL.Window*)window.SdlHandle;
        if (handle is null || SdlWindow.SDL is not { } sdl)
            return false;
        sdl.SetWindowAlwaysOnTop(handle, onTop ? SdlBool.True : SdlBool.False);
        return true;
    }
}
