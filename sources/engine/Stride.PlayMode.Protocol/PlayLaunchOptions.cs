// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

namespace Stride.PlayMode.Protocol;

/// <summary>
/// What the editor asks of the agent when it starts a game: given as variables of the game's environment
/// (<see cref="ToVariables"/>), so that the agent has them before its first frame and without waiting for the connection.
/// </summary>
/// <param name="StartPaused">Whether the game pauses before its first update: no script starts until Resume or Step.</param>
/// <param name="PauseOnScriptException">Whether a script's exception pauses the game instead of ending it.</param>
/// <param name="Hotkey">The key that pauses the game from its window, frees the mouse, and resumes it; <c>null</c> for none.</param>
/// <param name="Scene">The URL of the scene the game starts on instead of its default scene (Play this scene); <c>null</c> for the
/// default scene. The scene must be in the game's built data.</param>
public sealed record PlayLaunchOptions(bool StartPaused = false, bool PauseOnScriptException = false, PlayHotkey? Hotkey = null, string? Scene = null)
{
    /// <summary>
    /// The options of a game started without any: it plays at once, a script's exception ends it as without play mode, and no
    /// key of its window is taken.
    /// </summary>
    public static PlayLaunchOptions None { get; } = new();

    /// <summary>
    /// Reads the options from the play mode's variables of a game.
    /// </summary>
    /// <param name="variables">The variables, by name; a missing one is off.</param>
    public static PlayLaunchOptions FromVariables(IReadOnlyDictionary<string, string> variables)
    {
        ArgumentNullException.ThrowIfNull(variables);
        return new PlayLaunchOptions(
            IsOn(variables, PlayProtocol.StartPausedVariable),
            IsOn(variables, PlayProtocol.PauseOnScriptExceptionVariable),
            variables.TryGetValue(PlayProtocol.HotkeyVariable, out var hotkey) && PlayHotkey.TryParse(hotkey, out var parsed) ? parsed : null,
            variables.TryGetValue(PlayProtocol.SceneVariable, out var scene) && !string.IsNullOrWhiteSpace(scene) ? scene : null);
    }

    /// <summary>
    /// The variables that give these options to a game's agent; only those that are on.
    /// </summary>
    public IReadOnlyDictionary<string, string> ToVariables()
    {
        var variables = new Dictionary<string, string>();
        if (StartPaused)
            variables[PlayProtocol.StartPausedVariable] = "1";
        if (PauseOnScriptException)
            variables[PlayProtocol.PauseOnScriptExceptionVariable] = "1";
        if (Hotkey is { } hotkey)
            variables[PlayProtocol.HotkeyVariable] = hotkey.ToString();
        if (!string.IsNullOrWhiteSpace(Scene))
            variables[PlayProtocol.SceneVariable] = Scene;
        return variables;
    }

    private static bool IsOn(IReadOnlyDictionary<string, string> variables, string name)
        => variables.TryGetValue(name, out var value) && (value == "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Modifier keys of a <see cref="PlayHotkey"/>.
/// </summary>
[Flags]
public enum PlayHotkeyModifiers : byte
{
    /// <summary>No modifier.</summary>
    None = 0,

    /// <summary>Either Ctrl key.</summary>
    Ctrl = 1,

    /// <summary>Either Shift key.</summary>
    Shift = 2,

    /// <summary>Either Alt key.</summary>
    Alt = 4,
}

/// <summary>
/// A key of the game's window that the agent takes for itself, with its modifiers: written as "F7", "Shift+F7" or
/// "Ctrl+Alt+Pause". The key is the name of a Stride key (<c>Stride.Input.Keys</c>); the agent ignores a name it does not know.
/// </summary>
/// <param name="Key">The name of the key.</param>
/// <param name="Modifiers">The modifiers that must be held, and only those.</param>
public readonly record struct PlayHotkey(string Key, PlayHotkeyModifiers Modifiers = PlayHotkeyModifiers.None)
{
    /// <summary>
    /// The text that means no key.
    /// </summary>
    public const string NoneText = "None";

    /// <summary>
    /// Reads a key written as "F7" or "Shift+F7".
    /// </summary>
    /// <param name="text">The text; empty, or <see cref="NoneText"/>, for no key.</param>
    /// <param name="hotkey">The key read.</param>
    /// <returns><c>false</c> when the text names no key, or a modifier this type does not know.</returns>
    public static bool TryParse(string? text, out PlayHotkey hotkey)
    {
        hotkey = default;
        if (string.IsNullOrWhiteSpace(text) || string.Equals(text.Trim(), NoneText, StringComparison.OrdinalIgnoreCase))
            return false;
        var parts = text.Split('+', StringSplitOptions.TrimEntries);
        var modifiers = PlayHotkeyModifiers.None;
        for (var i = 0; i < parts.Length - 1; i++)
        {
            switch (parts[i].ToLowerInvariant())
            {
                case "ctrl" or "control":
                    modifiers |= PlayHotkeyModifiers.Ctrl;
                    break;
                case "shift":
                    modifiers |= PlayHotkeyModifiers.Shift;
                    break;
                case "alt":
                    modifiers |= PlayHotkeyModifiers.Alt;
                    break;
                default:
                    return false;
            }
        }
        var key = parts[^1];
        if (key.Length == 0)
            return false;
        hotkey = new PlayHotkey(key, modifiers);
        return true;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        var text = Key;
        if ((Modifiers & PlayHotkeyModifiers.Alt) != 0)
            text = "Alt+" + text;
        if ((Modifiers & PlayHotkeyModifiers.Shift) != 0)
            text = "Shift+" + text;
        if ((Modifiers & PlayHotkeyModifiers.Ctrl) != 0)
            text = "Ctrl+" + text;
        return text;
    }
}
