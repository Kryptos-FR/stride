// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core.Assets.Editor.Settings;
using Stride.Core.IO;
using Stride.Core.Mathematics;
using Stride.Core.Settings;
using Stride.Core.Translation;
using Stride.GameStudio.Avalonia.Helpers;

namespace Stride.GameStudio.Avalonia.Settings;

/// <summary>
/// The user settings of the Game Studio shell (the editor layers declare theirs in <see cref="EditorSettings"/>).
/// </summary>
internal static class GameStudioEditorSettings
{
    static GameStudioEditorSettings()
    {
        // Note: the name of this setting is based on the editor version, because we want to force displaying the release notes on a new version.
        StartupSession = new SettingsKey<UFile>("Interface/StartupSession" + StrideGameStudio.EditorVersion, EditorSettings.SettingsContainer, "")
        {
            DisplayName = $"{EditorSettings.Interface}/{Tr._p("Settings", "Default session to load")}"
        };
        // The theme of this editor, under keys of its own (the other editors keep theirs elsewhere), so not in EditorSettings
        ThemeAccent = new SettingsKey<Color>("Interface/Theme/Accent", EditorSettings.SettingsContainer, default(Color))
        {
            DisplayName = $"{EditorSettings.Interface}/{Tr._p("Settings", "Theme Accent")}",
        };
        ThemeVariant = new SettingsKey<string>("Interface/Theme/Variant", EditorSettings.SettingsContainer, "Default")
        {
            DisplayName = $"{EditorSettings.Interface}/{Tr._p("Settings", "Theme Variant")}",
            GetAcceptableValues = () => ["Default", "Dark", "Light"],
        };
        // A way out on a system where the windows of the OS misbehave (the panels cannot leave the main window then)
        FloatingWindowsInsideMainWindow = new SettingsKey<bool>("Interface/FloatingWindowsInsideMainWindow", EditorSettings.SettingsContainer, false)
        {
            DisplayName = $"{EditorSettings.Interface}/{Tr._p("Settings", "Floating panels inside the main window")}",
        };
        // Start project runs the game of the editor's platform in play mode, with the Live tab, unless this is off
        PlayModeConnect = new SettingsKey<bool>("PlayMode/ConnectWhenStarting", EditorSettings.SettingsContainer, true)
        {
            DisplayName = $"{PlayMode}/{Tr._p("Settings", "Connect to the game when starting the project")}",
            Description = Tr._p("Settings", "Start project (F5) starts the game with the live connection: the Live tab shows its scenes and entities, and the property grid the selected ones. Off, the game starts as it would without play mode."),
        };
        // On by default: the last tree of a game that exited stays readable
        PlayModeKeepLiveTabAfterExit = new SettingsKey<bool>("PlayMode/KeepLiveTabAfterExit", EditorSettings.SettingsContainer, true)
        {
            DisplayName = $"{PlayMode}/{Tr._p("Settings", "Keep the Live tab open when the game exits")}",
            Description = Tr._p("Settings", "The Live tab stays open with the last tree of the game once it exits, greyed. Off, it closes when the game exits normally, and the tab it covered comes back; it stays open when the game crashes (a non-zero exit code) or its connection is lost, so that the reason stays visible."),
        };
        // The user's shortcuts, by command identifier. Edited in the settings file for now: a display name without a category
        // keeps it out of the dialog
        ShortcutOverrides = new SettingsKey<Dictionary<string, string>>("Shortcuts/Overrides", EditorSettings.SettingsContainer, () => new Dictionary<string, string>())
        {
            DisplayName = "ShortcutOverrides",
        };
    }

    /// <summary>
    /// The category of the play mode settings.
    /// </summary>
    public static string PlayMode => Tr._p("Settings", "Play mode");

    /// <summary>
    /// Whether Start project starts the game of the editor's platform with the live connection (play mode).
    /// </summary>
    public static SettingsKey<bool> PlayModeConnect { get; }

    /// <summary>
    /// Whether the Live tab stays open when its game exits normally. It stays open after a crash or a lost connection anyway.
    /// </summary>
    public static SettingsKey<bool> PlayModeKeepLiveTabAfterExit { get; }

    /// <summary>
    /// The session to open at startup when no path is given on the command line.
    /// </summary>
    public static SettingsKey<UFile> StartupSession { get; }

    public static SettingsKey<Color> ThemeAccent { get; }

    public static SettingsKey<string> ThemeVariant { get; }

    /// <summary>
    /// Whether a floating panel or editor is drawn inside the main window rather than in a window of its own.
    /// </summary>
    public static SettingsKey<bool> FloatingWindowsInsideMainWindow { get; }

    /// <summary>
    /// The shortcuts the user gave to commands, or took from them: a command identifier ("Stride.File.Save") to a shortcut
    /// ("Ctrl+Alt+S"), or to "None" (or nothing) for none. They come before the default shortcuts of the editor and of the
    /// plugins (<see cref="Core.Assets.Editor.Commands.CommandGestureBindings"/>); a command that is not loaded is skipped.
    /// </summary>
    public static SettingsKey<Dictionary<string, string>> ShortcutOverrides { get; }
}
