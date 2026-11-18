// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core.Diagnostics;
using Stride.Engine;

namespace Stride.PlayMode.Agent;

/// <summary>
/// Starts a played game on another scene than its default one (Play this scene): the agent, created when the game starts,
/// replaces the scene system's initial scene before the scene system loads it.
/// </summary>
/// <remarks>
/// The game's settings keep their default scene: a script that reads <c>Game.Settings.DefaultSceneUrl</c> (to restart a level,
/// for instance) still gets it. A game that sets its scene itself, in its own code, after the scene system loaded the initial
/// one, replaces this one too.
/// </remarks>
internal static class PlayStartScene
{
    /// <summary>
    /// Makes the scene the game's initial scene, if the game's built data has it.
    /// </summary>
    /// <param name="sceneSystem">The scene system of the game, before it loads its content.</param>
    /// <param name="url">The URL of the scene.</param>
    /// <param name="exists">Whether the game's content has a URL.</param>
    /// <param name="log">Where a scene that is missing is reported; the agent sends it to the editor's Output pane.</param>
    /// <returns><c>true</c> when the game starts on the scene; <c>false</c> when it is not in the built data, and the game starts
    /// on its default scene.</returns>
    public static bool Apply(SceneSystem sceneSystem, string url, Func<string, bool> exists, ILogger log)
    {
        ArgumentNullException.ThrowIfNull(sceneSystem);
        ArgumentException.ThrowIfNullOrEmpty(url);
        ArgumentNullException.ThrowIfNull(exists);
        ArgumentNullException.ThrowIfNull(log);

        var defaultScene = sceneSystem.InitialSceneUrl;
        bool found;
        try
        {
            found = exists(url);
        }
        catch (Exception e)
        {
            log.Warning($"Play mode could not look for the scene [{url}] in the game's data ({e.Message}).");
            found = false;
        }
        if (!found)
        {
            log.Warning(defaultScene is null
                ? $"The scene [{url}] is not in the game's built data: the game starts without a scene."
                : $"The scene [{url}] is not in the game's built data: the game starts on its default scene [{defaultScene}].");
            return false;
        }
        sceneSystem.InitialSceneUrl = url;
        return true;
    }
}
