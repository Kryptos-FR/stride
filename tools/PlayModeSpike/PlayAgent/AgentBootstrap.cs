// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using Stride.Engine;

namespace Stride.PlayMode.Spike;

/// <summary>
/// Waits for the game to start and adds the play agent to its systems.
/// </summary>
public static class AgentBootstrap
{
    private static PlayAgentSystem? agent;

    /// <summary>The assemblies the game did not have, loaded from the agent's folder by the startup hook.</summary>
    internal static List<string> ResolvedFromAgent => AppContext.GetData("Stride.PlayMode.ResolvedFromAgent") as List<string> ?? [];

    /// <summary>The time the startup hook ran, from <see cref="System.Diagnostics.Stopwatch.GetTimestamp"/>.</summary>
    internal static long HookTimestamp => AppContext.GetData("Stride.PlayMode.HookTimestamp") as long? ?? 0;

    /// <summary>Subscribes to <see cref="Game.GameStarted"/>.</summary>
    public static void Start(string? pipeName, string? mode)
    {
        // Processes the game starts must not load the agent again.
        Environment.SetEnvironmentVariable("DOTNET_STARTUP_HOOKS", null);
        Environment.SetEnvironmentVariable(PlayFraming.PipeVariable, null);

        Game.GameStarted += (sender, _) =>
        {
            // Only the first game: a game could create others (an editor-like tool), which are not played.
            if (agent is not null || sender is not Game game)
                return;
            // Spike-only knob, applied the same way with and without the agent: a smaller back buffer, so that a
            // software renderer (lavapipe under Xvfb) leaves frame times where the agent's cost can be seen.
            if (Environment.GetEnvironmentVariable("STRIDE_PLAY_SPIKE_BACKBUFFER") is { } size && size.Split('x') is [var w, var h])
            {
                var deviceManager = (Stride.Games.GraphicsDeviceManager)game.GraphicsDeviceManager;
                deviceManager.PreferredBackBufferWidth = int.Parse(w);
                deviceManager.PreferredBackBufferHeight = int.Parse(h);
                deviceManager.SynchronizeWithVerticalRetrace = false;
            }
            agent = new PlayAgentSystem(game, pipeName, mode == "meter", Environment.GetEnvironmentVariable(PlayFraming.MeterOutputVariable));
            game.GameSystems.Add(agent);
        };
    }
}
