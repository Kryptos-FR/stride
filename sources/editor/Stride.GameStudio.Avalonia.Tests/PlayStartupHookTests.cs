// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections;
using System.Reflection;
using Stride.Editor.PlayMode;
using Stride.PlayMode.Protocol;

namespace Stride.GameStudio.Avalonia.Tests;

/// <summary>
/// The startup hook that the editor ships next to it, as a game's host calls it: it removes the play mode's variables from the
/// game's environment, the options of the launch among them, so that a process the game starts does not inherit them.
/// </summary>
/// <remarks>
/// Without a pipe the hook loads no agent. The test process's own variables that start with the prefix (those of the end-to-end
/// tests) are put back, and the tests of this assembly do not run in parallel.
/// </remarks>
public sealed class PlayStartupHookTests
{
    [Fact]
    public void TheHookClearsTheLaunchOptionsFromTheGamesEnvironment()
    {
        Assert.SkipUnless(PlayAgentFiles.NextToEditor.Exist, $"The play agent's files are not in {PlayAgentFiles.NextToEditor.Directory}.");
        var hookPath = PlayAgentFiles.NextToEditor.HookPath;
        var saved = Environment.GetEnvironmentVariables().Cast<DictionaryEntry>()
            .Where(x => x.Key is string name && (name.StartsWith(PlayProtocol.VariablePrefix, StringComparison.Ordinal) || name == PlayProtocol.StartupHooksVariable))
            .ToDictionary(x => (string)x.Key, x => x.Value as string);
        var options = new PlayLaunchOptions(StartPaused: true, PauseOnScriptException: true, Hotkey: new PlayHotkey("F7"), Scene: "/MyGame/Scenes/Level2");
        try
        {
            foreach (var name in saved.Keys)
                Environment.SetEnvironmentVariable(name, null);
            foreach (var (name, value) in options.ToVariables())
                Environment.SetEnvironmentVariable(name, value);
            Environment.SetEnvironmentVariable(PlayProtocol.EngineVersionVariable, "1.0.0-test");
            Environment.SetEnvironmentVariable(PlayProtocol.StartupHooksVariable, hookPath);
            Assert.Equal("/MyGame/Scenes/Level2", Environment.GetEnvironmentVariable(PlayProtocol.SceneVariable));

            var hook = Assembly.LoadFrom(hookPath).GetType("StartupHook", throwOnError: true)!;
            hook.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, null);

            Assert.All(PlayProtocol.GameVariables, name => Assert.Null(Environment.GetEnvironmentVariable(name)));
            Assert.DoesNotContain(Environment.GetEnvironmentVariables().Keys.Cast<string>(), x => x.StartsWith(PlayProtocol.VariablePrefix, StringComparison.Ordinal));
        }
        finally
        {
            foreach (var name in PlayProtocol.GameVariables)
                Environment.SetEnvironmentVariable(name, null);
            foreach (var (name, value) in saved)
                Environment.SetEnvironmentVariable(name, value);
        }
    }
}
