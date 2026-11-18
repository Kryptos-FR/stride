// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Diagnostics;
using Avalonia.Headless.XUnit;
using Stride.Assets.Editor.PlayMode;
using Stride.Core.Diagnostics;
using Stride.Editor.PlayMode;
using Stride.GameStudio.Avalonia.Tests.Helpers;
using Stride.PlayMode.Protocol;

namespace Stride.GameStudio.Avalonia.Tests;

/// <summary>
/// Plays a real game built from a Stride template with the play agent: its tree reaches the editor, and a snapshot of one of
/// its entities reaches the live property grid; then the game ends, with every process and pipe the test started.
/// </summary>
/// <remarks>
/// The game is not built by the test: <c>STRIDE_PLAY_TEST_GAME</c> names the assembly of its executable project (for example
/// the Linux head of <c>dotnet new stride-fps -p linux</c>, built against this checkout's packages). It needs a graphics device
/// and, on Linux, an X display (Xvfb with a Vulkan driver such as lavapipe). <c>STRIDE_PLAY_TEST_ENTITY</c> names the entity
/// to show; otherwise the one with the most components. Without the game or the display, the test is skipped.
/// </remarks>
public sealed class PlayModeEndToEndTests
{
    public const string GameVariable = "STRIDE_PLAY_TEST_GAME";
    public const string EntityVariable = "STRIDE_PLAY_TEST_ENTITY";
    private const int StartTimeout = 120_000;

    [AvaloniaFact]
    public async Task ATemplateGameSendsItsTreeAndASnapshotThatTheLiveGridShows()
    {
        var gamePath = Environment.GetEnvironmentVariable(GameVariable);
        Assert.SkipWhen(string.IsNullOrEmpty(gamePath) || !File.Exists(gamePath), $"No built game: set {GameVariable} to the assembly of a game built from a Stride template (for example the Linux head of 'dotnet new stride-fps').");
        Assert.SkipWhen(OperatingSystem.IsLinux() && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY")), "No X display: run the test under Xvfb, with a Vulkan driver (lavapipe), so that the game can open its window.");
        Assert.SkipUnless(PlayAgentFiles.NextToEditor.Exist, $"The play agent's files are not in {PlayAgentFiles.NextToEditor.Directory}.");

        using var fixture = await ShellSessionFixture.OpenAsync();
        var output = new LoggerResult();
        var tree = new TaskCompletionSource<PlayTree>(TaskCreationOptions.RunContinuationsAsynchronously);
        LivePropertiesViewModel? properties = null;
        int? processId = null;
        var session = new PlaySession(new PipePlayTransport(), PlayAgentFiles.NextToEditor, new PlaySessionOptions
        {
            ConnectTimeout = TimeSpan.FromMilliseconds(StartTimeout),
            StopGracePeriod = TimeSpan.FromSeconds(10),
            Log = output,
        });
        session.TreeReceived += (_, x) => tree.TrySetResult(x);
        try
        {
            var start = session.StartAsync(environment => PlayGameLauncher.Start(gamePath!, output, environment), PlayAgentFiles.GetGameStrideVersion(gamePath!));
            processId = session.Game?.Id;
            await WaitAsync(start, "the agent's connection", output);
            Assert.True(session.State is PlaySessionState.Starting or PlaySessionState.Connected, $"The session is {session.State}: {session.StateMessage}{Log(output)}");

            // The whole tree, once the game runs its first frame
            await WaitAsync(Task.WhenAny(tree.Task, session.Exited).Unwrap(), "the game's tree", output);
            Assert.True(tree.Task.IsCompleted, $"The game exited with {session.ExitCode} before it sent its tree.{Log(output)}");
            var entities = tree.Task.Result.Entities;
            Assert.NotEmpty(entities);
            Assert.Equal(PlaySessionState.Connected, session.State);
            var name = Environment.GetEnvironmentVariable(EntityVariable);
            var entity = string.IsNullOrEmpty(name) ? entities.MaxBy(x => x.Components.Count)! : entities.First(x => x.Name == name);

            // The entity's snapshot, in the live grid
            properties = new LivePropertiesViewModel(fixture.Session);
            var applied = new TaskCompletionSource<LiveApplyEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
            properties.Applied += (_, x) =>
            {
                if (x.Snapshot is not null)
                    applied.TrySetResult(x);
            };
            properties.Show(session, [entity.Handle]);
            await WindowTestHelper.WaitUntilAsync(() => applied.Task.IsCompleted || session.Exited.IsCompleted, "a snapshot in the live grid", StartTimeout);
            Assert.True(applied.Task.IsCompleted, $"The game exited with {session.ExitCode} before it sent a snapshot.{Log(output)}");
            Assert.Same(properties, fixture.Session.ActiveProperties);
            Assert.NotNull(properties.ViewModel);
            Assert.Equal(entity.Name, properties.Name);
            Assert.Equal([entity.Handle], properties.Handles);
            Assert.StartsWith("Frame ", properties.LiveStatus, StringComparison.Ordinal);
            Assert.Equal(entity.Components.Count, properties.Mirrors.Single().Mirror.Components.Count);
        }
        finally
        {
            properties?.Detach();
            properties?.ViewModel?.Destroy();
            // Quit, or the end of the process after the grace period; then the pipe
            await session.DisposeAsync();
        }

        // The game is gone, with the processes it started
        Assert.True(session.Exited.IsCompleted, "The game still runs.");
        if (processId is { } id)
            Assert.True(HasExited(id), $"The game's process {id} still runs.");
    }

    private static async Task WaitAsync(Task task, string what, LoggerResult output)
    {
        // The UI thread keeps running while the game starts: the session's events are dispatched to it
        await WindowTestHelper.WaitUntilAsync(() => task.IsCompleted, what, StartTimeout + 10_000);
        if (task.IsFaulted)
            throw new InvalidOperationException($"Waiting for {what} failed.{Log(output)}", task.Exception!.GetBaseException());
    }

    private static string Log(LoggerResult output) => output.Messages.Count == 0 ? string.Empty
        : Environment.NewLine + "The game's output:" + Environment.NewLine + string.Join(Environment.NewLine, output.Messages.TakeLast(40).Select(x => x.Text));

    private static bool HasExited(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return process.HasExited;
        }
        catch (ArgumentException)
        {
            return true;
        }
    }
}
