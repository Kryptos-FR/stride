// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using Stride.Core.Translation;
using Stride.PlayMode.Protocol;

namespace Stride.Editor.PlayMode;

/// <summary>
/// How the editor says why a played game is paused, and which script exception paused it: the status bar, the Live tab and
/// the Output pane use the same words.
/// </summary>
[Experimental("STREXP012")]
public static class PlayPauseText
{
    /// <summary>
    /// The state of a paused game with its reason: "Paused", "Paused: F7 in the game window", "Paused: started paused",
    /// "Paused: script exception in PlayerController".
    /// </summary>
    /// <param name="reason">Why the game is paused.</param>
    /// <param name="exception">The script exception that paused it, when that is the reason.</param>
    /// <param name="hotkey">The key of the game's window, when that is the reason.</param>
    public static string Describe(PlayPauseReason reason, PlayScriptException? exception = null, PlayHotkey? hotkey = null) => reason switch
    {
        PlayPauseReason.Hotkey => hotkey is { } key
            ? string.Format(Tr._p("Play", "Paused: {0} in the game window"), key)
            : Tr._p("Play", "Paused: the pause key in the game window"),
        PlayPauseReason.StartPaused => Tr._p("Play", "Paused: started paused"),
        PlayPauseReason.ScriptException => exception?.ScriptName is { } script
            ? string.Format(Tr._p("Play", "Paused: script exception in {0}"), script)
            : Tr._p("Play", "Paused: script exception"),
        _ => Tr._p("Play", "Paused"),
    };

    /// <summary>
    /// What a script exception was, in a sentence: "PlayerController (entity Player) threw InvalidOperationException in Update:
    /// the message."
    /// </summary>
    public static string DescribeException(PlayScriptException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var script = exception.ScriptName ?? Tr._p("Play", "A script");
        var where = exception.EntityName is { } entity ? string.Format(Tr._p("Play", "{0} (entity {1})"), script, entity) : script;
        var type = exception.ExceptionType[(exception.ExceptionType.LastIndexOf('.') + 1)..];
        var method = exception.Kind switch
        {
            PlayScriptExceptionKind.Update => Tr._p("Play", " in Update"),
            PlayScriptExceptionKind.Start => Tr._p("Play", " in Start"),
            PlayScriptExceptionKind.Async => Tr._p("Play", " in its async code"),
            _ => string.Empty,
        };
        return string.Format(Tr._p("Play", "{0} threw {1}{2}: {3}"), where, type, method, exception.Message);
    }

    /// <summary>
    /// What the Output pane shows for a script exception: the sentence, what happened to the game and the script, and the
    /// exception with its stack.
    /// </summary>
    public static string DescribeForOutput(PlayScriptException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var outcome = exception.Paused
            ? Tr._p("Play", "Play mode paused the game; the engine stopped this script. Resume (F7), step (F10) or stop the game (Shift+F5).")
            : Tr._p("Play", "The engine stopped this script.");
        var sentence = DescribeException(exception);
        // The exception's message often has no full stop of its own
        if (!sentence.EndsWith('.') && !sentence.EndsWith('!') && !sentence.EndsWith('?'))
            sentence += ".";
        return $"{sentence} {outcome}{(exception.Details is { } details ? Environment.NewLine + details : string.Empty)}";
    }
}
