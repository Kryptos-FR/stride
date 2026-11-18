// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Input;
using Stride.PlayMode.Protocol;

namespace Stride.PlayMode.Agent;

/// <summary>
/// Listens for the play mode's key in the game's window (<see cref="PlayLaunchOptions.Hotkey"/>) and calls back on its first
/// press, with exactly its modifiers held.
/// </summary>
/// <remarks>
/// The input manager routes its events in its own update, which runs before the scripts (the input system's update order is
/// -50): a pause taken in the callback stops the scripts before they run in that frame, so that they do not see the press.
/// </remarks>
internal sealed class PlayHotkeyListener(Keys key, PlayHotkeyModifiers modifiers, Action pressed) : IInputEventListener<KeyEvent>
{
    /// <summary>The key.</summary>
    public Keys Key => key;

    /// <summary>The modifiers that must be held, and only those.</summary>
    public PlayHotkeyModifiers Modifiers => modifiers;

    /// <summary>
    /// Makes a listener for a key named as in the options.
    /// </summary>
    /// <param name="hotkey">The key; <c>null</c> for none.</param>
    /// <param name="pressed">Called on the game thread when the key is pressed.</param>
    /// <param name="warn">Told when the key's name is not a Stride key.</param>
    /// <returns>The listener; <c>null</c> for no key, or a name that is not a key.</returns>
    public static PlayHotkeyListener? TryCreate(PlayHotkey? hotkey, Action pressed, Action<string> warn)
    {
        if (hotkey is not { } value)
            return null;
        if (!Enum.TryParse<Keys>(value.Key, ignoreCase: true, out var parsed) || parsed == Keys.None || int.TryParse(value.Key, out _))
        {
            warn($"Play mode does not know the key \"{value.Key}\" of the setting \"Pause key in the game window\": the game window has no pause key.");
            return null;
        }
        return new PlayHotkeyListener(parsed, value.Modifiers, pressed);
    }

    /// <inheritdoc/>
    public void ProcessEvent(KeyEvent inputEvent) => OnKey(inputEvent.Key, inputEvent.IsDown, inputEvent.RepeatCount, inputEvent.Keyboard.DownKeys);

    /// <summary>
    /// Handles a key event: calls back for the first press of the key with exactly its modifiers held.
    /// </summary>
    /// <param name="eventKey">The key of the event.</param>
    /// <param name="isDown">Whether the key went down.</param>
    /// <param name="repeatCount">0 for the first press, more for the repeats of a key held down.</param>
    /// <param name="down">The keys held down on the keyboard.</param>
    internal void OnKey(Keys eventKey, bool isDown, int repeatCount, Core.Collections.IReadOnlySet<Keys> down)
    {
        if (eventKey != key || !isDown || repeatCount != 0)
            return;
        var held = PlayHotkeyModifiers.None;
        if (down.Contains(Keys.LeftCtrl) || down.Contains(Keys.RightCtrl))
            held |= PlayHotkeyModifiers.Ctrl;
        if (down.Contains(Keys.LeftShift) || down.Contains(Keys.RightShift))
            held |= PlayHotkeyModifiers.Shift;
        if (down.Contains(Keys.LeftAlt) || down.Contains(Keys.RightAlt))
            held |= PlayHotkeyModifiers.Alt;
        if (held == modifiers)
            pressed();
    }
}
