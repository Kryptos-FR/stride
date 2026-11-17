// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.IO.Pipes;
using Stride.Engine;

namespace Stride.PlayMode.Agent;

/// <summary>
/// Starts the play agent in a game: connects to the editor's pipe and adds a <see cref="PlayAgentSystem"/> to the first game
/// that starts. The startup hook (Stride.PlayMode.Hook) calls <see cref="Start"/> before the game's <c>Main</c>.
/// </summary>
public static class PlayAgentBootstrap
{
    /// <summary>
    /// How long the agent waits for the editor's pipe.
    /// </summary>
    public static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);

    private static readonly object Lock = new();
    private static PlayAgentSystem? agent;
    private static Stream? connectedStream;

    /// <summary>
    /// Connects to the editor and waits for the game to start. Called once, by the startup hook.
    /// </summary>
    /// <param name="pipeName">The pipe the editor listens on.</param>
    public static void Start(string pipeName)
    {
        ArgumentException.ThrowIfNullOrEmpty(pipeName);
        Game.GameStarted += OnGameStarted;
        // Connect while the game initializes: the pipe is ready when the first frame runs
        _ = Task.Run(() => ConnectAsync(pipeName));
    }

    private static void OnGameStarted(object? sender, EventArgs e)
    {
        lock (Lock)
        {
            // Only the first game: a game could create others (an editor-like tool), which are not played
            if (agent is not null || sender is not Game game)
                return;
            agent = new PlayAgentSystem(game);
            game.GameSystems.Add(agent);
            if (connectedStream is not null)
                agent.AddSubscriber(connectedStream);
        }
        Game.GameStarted -= OnGameStarted;
    }

    private static async Task ConnectAsync(string pipeName)
    {
        var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        try
        {
            using var timeout = new CancellationTokenSource(ConnectTimeout);
            await pipe.ConnectAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            await pipe.DisposeAsync().ConfigureAwait(false);
            Console.Error.WriteLine($"Play mode: the agent could not connect to the editor; the live connection is off. {e.Message}");
            return;
        }
        lock (Lock)
        {
            if (agent is not null)
                agent.AddSubscriber(pipe);
            else
                connectedStream = pipe;
        }
    }
}
