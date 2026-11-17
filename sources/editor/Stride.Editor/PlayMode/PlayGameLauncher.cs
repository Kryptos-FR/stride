// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Stride.Core.Assets.Editor.Components.Build;
using Stride.Core.Diagnostics;

namespace Stride.Editor.PlayMode;

/// <summary>
/// A played game's process, which the play session can end.
/// </summary>
[Experimental("STREXP012")]
public interface IPlayGameProcess : IGameProcess
{
    /// <summary>
    /// Ends the game's process (and the processes it started) if it still runs.
    /// </summary>
    void Kill();
}

/// <summary>
/// Starts a built game as the Start project command does (<see cref="GameProcessLauncher"/>), with the variables of a play
/// session added to its environment.
/// </summary>
[Experimental("STREXP012")]
public static class PlayGameLauncher
{
    /// <summary>
    /// Starts the game.
    /// </summary>
    /// <param name="assemblyPath">The assembly of the game's executable project.</param>
    /// <param name="output">Receives the game's console output.</param>
    /// <param name="environment">The variables to add (<see cref="PlayAgentFiles.CreateGameEnvironment"/>); empty to start the
    /// game with no agent.</param>
    public static IPlayGameProcess Start(string assemblyPath, ILogger output, IReadOnlyDictionary<string, string> environment)
    {
        ArgumentNullException.ThrowIfNull(assemblyPath);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(environment);

        var startInfo = GameProcessLauncher.CreateStartInfo(assemblyPath);
        foreach (var (name, value) in environment)
            startInfo.Environment[name] = value;
        return new PlayGameProcess(GameProcessLauncher.StartProcess(startInfo, output));
    }

    private sealed class PlayGameProcess : IPlayGameProcess
    {
        private readonly Process process;

        public PlayGameProcess(Process process)
        {
            this.process = process;
            Id = process.Id;
            Exited = WaitForExitAsync();
        }

        public int Id { get; }

        public Task<int> Exited { get; }

        public void Kill()
        {
            try
            {
                if (!Exited.IsCompleted)
                    process.Kill(entireProcessTree: true);
            }
            catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // It exited in the meantime
            }
        }

        private async Task<int> WaitForExitAsync()
        {
            // Also waits for the end of the redirected output; the process is disposed after its exit only, so Kill can use it
            await process.WaitForExitAsync().ConfigureAwait(false);
            var exitCode = process.ExitCode;
            process.Dispose();
            return exitCode;
        }
    }
}
