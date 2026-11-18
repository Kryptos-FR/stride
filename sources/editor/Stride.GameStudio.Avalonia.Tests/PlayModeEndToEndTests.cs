// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.Concurrent;
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
/// its entities reaches the live property grid; it pauses, steps, slows down and resumes; it starts paused; then the game ends, with every
/// process and pipe the test started.
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

    [AvaloniaFact]
    public async Task ATemplateGamePausesStepsSlowsDownAndResumes()
    {
        var gamePath = Environment.GetEnvironmentVariable(GameVariable);
        Assert.SkipWhen(string.IsNullOrEmpty(gamePath) || !File.Exists(gamePath), $"No built game: set {GameVariable} to the assembly of a game built from a Stride template (for example the Linux head of 'dotnet new stride-fps').");
        Assert.SkipWhen(OperatingSystem.IsLinux() && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY")), "No X display: run the test under Xvfb, with a Vulkan driver (lavapipe), so that the game can open its window.");
        Assert.SkipUnless(PlayAgentFiles.NextToEditor.Exist, $"The play agent's files are not in {PlayAgentFiles.NextToEditor.Directory}.");

        var output = new LoggerResult();
        var tree = new TaskCompletionSource<PlayTree>(TaskCreationOptions.RunContinuationsAsynchronously);
        var heartbeats = new ConcurrentQueue<(bool Paused, PlayHeartbeat Heartbeat)>();
        var snapshots = 0;
        int? processId = null;
        var session = new PlaySession(new PipePlayTransport(), PlayAgentFiles.NextToEditor, new PlaySessionOptions
        {
            ConnectTimeout = TimeSpan.FromMilliseconds(StartTimeout),
            StopGracePeriod = TimeSpan.FromSeconds(10),
            Log = output,
        });
        session.TreeReceived += (_, x) => tree.TrySetResult(x);
        session.HeartbeatReceived += (_, x) => heartbeats.Enqueue((session.IsPaused, x));
        session.SnapshotReceived += (_, _) => Interlocked.Increment(ref snapshots);
        try
        {
            var start = session.StartAsync(environment => PlayGameLauncher.Start(gamePath!, output, environment), PlayAgentFiles.GetGameStrideVersion(gamePath!));
            processId = session.Game?.Id;
            await WaitAsync(start, "the agent's connection", output);
            await WaitAsync(Task.WhenAny(tree.Task, session.Exited).Unwrap(), "the game's tree", output);
            Assert.True(tree.Task.IsCompleted, $"The game exited with {session.ExitCode} before it sent its tree.{Log(output)}");
            var entity = tree.Task.Result.Entities.MaxBy(x => x.Components.Count)!;
            Assert.True(session.Subscribe([entity.Handle]));

            // Playing: the state comes after each heartbeat
            await WaitForAsync(session, x => !x.Paused && x.PlayedFrames > 0, "the game playing", output);
            await WaitForHeartbeatsAsync(heartbeats, paused: false, 3, output);
            Assert.Equal(tree.Task.Result.Entities.Count, session.GameState!.EntityCount);
            Assert.True(session.GameState.FramesPerSecond > 0);

            // Paused: no frame plays, the game goes on drawing and sending its snapshots
            session.Pause();
            var paused = await WaitForAsync(session, x => x.Paused, "the pause", output);
            Assert.True(session.IsPaused);
            var snapshotsAtPause = Volatile.Read(ref snapshots);
            var atPause = heartbeats.Count;
            await WaitForHeartbeatsAsync(heartbeats, paused: true, 5, output);
            var beforeStep = heartbeats.Count;
            Assert.Equal(paused.PlayedFrames, session.GameState!.PlayedFrames);
            Assert.True(session.GameState.Frame > paused.Frame, "The game stopped drawing while paused.");
            Assert.True(Volatile.Read(ref snapshots) > snapshotsAtPause, "No snapshot while paused.");

            // A step plays one frame and stays paused
            session.Step();
            await WaitForAsync(session, x => x.Paused && x.StepsPending == 0 && x.PlayedFrames == paused.PlayedFrames + 1, "the step", output);
            session.Step(3);
            await WaitForAsync(session, x => x.Paused && x.StepsPending == 0 && x.PlayedFrames == paused.PlayedFrames + 4, "three steps", output);

            // The time scale, then resume
            session.SetTimeScale(0.25);
            await WaitForAsync(session, x => x.TimeScale == 0.25, "the time scale", output);
            session.Resume();
            var resumed = await WaitForAsync(session, x => !x.Paused, "the resume", output);
            await WaitForAsync(session, x => x.PlayedFrames > resumed.PlayedFrames, "frames after the resume", output);
            Assert.Equal(0.25, session.GameState!.TimeScale);
            Assert.DoesNotContain(output.Messages, x => x.Text.Contains("time factor", StringComparison.Ordinal));
            var beforeResume = heartbeats.Count;
            await WaitForHeartbeatsAsync(heartbeats, paused: false, 3, output);

            // The agent's cost per frame, playing (after the resume: the first frames pay for the JIT) and paused (before the
            // steps, and not the heartbeat of the pause)
            var playing = heartbeats.Skip(beforeResume).Where(x => !x.Paused).Select(x => x.Heartbeat.Stats).Where(x => x.Frames > 0).ToList();
            var stopped = heartbeats.Take(beforeStep).Skip(atPause + 1).Where(x => x.Paused).Select(x => x.Heartbeat.Stats).Where(x => x.Frames > 0).ToList();
            foreach (var (isPaused, heartbeat) in heartbeats)
                TestContext.Current.TestOutputHelper?.WriteLine($"{(isPaused ? "paused " : "playing")} {heartbeat.FramesPerSecond:0.0} fps, agent {heartbeat.Stats.AgentMeanMicroseconds:0} µs a frame (max {heartbeat.Stats.AgentMaxMicroseconds:0}), {heartbeat.Stats.Snapshots} snapshots of {heartbeat.Stats.SnapshotMeanMicroseconds:0} µs");
            TestContext.Current.TestOutputHelper?.WriteLine($"Agent per frame (median of the heartbeats): playing {Median(playing)} µs over {playing.Sum(x => x.Frames)} frames, paused {Median(stopped)} µs over {stopped.Sum(x => x.Frames)} frames.");
        }
        finally
        {
            await session.DisposeAsync();
        }

        Assert.True(session.Exited.IsCompleted, "The game still runs.");
        if (processId is { } id)
            Assert.True(HasExited(id), $"The game's process {id} still runs.");
    }

    [AvaloniaFact]
    public async Task ATemplateGameStartedPausedPlaysNoFrameUntilAStep()
    {
        var gamePath = Environment.GetEnvironmentVariable(GameVariable);
        Assert.SkipWhen(string.IsNullOrEmpty(gamePath) || !File.Exists(gamePath), $"No built game: set {GameVariable} to the assembly of a game built from a Stride template (for example the Linux head of 'dotnet new stride-fps').");
        Assert.SkipWhen(OperatingSystem.IsLinux() && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY")), "No X display: run the test under Xvfb, with a Vulkan driver (lavapipe), so that the game can open its window.");
        Assert.SkipUnless(PlayAgentFiles.NextToEditor.Exist, $"The play agent's files are not in {PlayAgentFiles.NextToEditor.Directory}.");

        var output = new LoggerResult();
        var tree = new TaskCompletionSource<PlayTree>(TaskCreationOptions.RunContinuationsAsynchronously);
        var heartbeats = new ConcurrentQueue<(bool Paused, PlayHeartbeat Heartbeat)>();
        int? processId = null;
        // Every option on, as the settings would send them: the key and the watcher of the scripts' exceptions cost their share
        var session = new PlaySession(new PipePlayTransport(), PlayAgentFiles.NextToEditor, new PlaySessionOptions
        {
            ConnectTimeout = TimeSpan.FromMilliseconds(StartTimeout),
            StopGracePeriod = TimeSpan.FromSeconds(10),
            Log = output,
        })
        {
            LaunchOptions = new PlayLaunchOptions(StartPaused: true, PauseOnScriptException: true, Hotkey: new PlayHotkey("F7")),
        };
        session.TreeReceived += (_, x) => tree.TrySetResult(x);
        session.HeartbeatReceived += (_, x) => heartbeats.Enqueue((session.IsPaused, x));
        try
        {
            var start = session.StartAsync(environment => PlayGameLauncher.Start(gamePath!, output, environment), PlayAgentFiles.GetGameStrideVersion(gamePath!));
            processId = session.Game?.Id;
            await WaitAsync(start, "the agent's connection", output);
            await WaitAsync(Task.WhenAny(tree.Task, session.Exited).Unwrap(), "the game's tree", output);
            Assert.True(tree.Task.IsCompleted, $"The game exited with {session.ExitCode} before it sent its tree.{Log(output)}");

            // Paused before its first update: it draws, and plays no frame
            var paused = await WaitForAsync(session, x => x.Paused, "the start paused", output);
            Assert.Equal(PlayPauseReason.StartPaused, paused.PauseReason);
            await WaitForHeartbeatsAsync(heartbeats, paused: true, 3, output);
            Assert.Equal(0, session.GameState!.PlayedFrames);
            Assert.True(session.GameState.Frame > paused.Frame, "The game stopped drawing while paused.");

            // A step plays the first frame, and the reason stays: the game has not been resumed
            session.Step();
            await WaitForAsync(session, x => x.Paused && x.StepsPending == 0 && x.PlayedFrames == 1, "the step", output);
            Assert.Equal(PlayPauseReason.StartPaused, session.GameState!.PauseReason);

            session.Resume();
            var resumed = await WaitForAsync(session, x => !x.Paused, "the resume", output);
            Assert.Equal(PlayPauseReason.None, resumed.PauseReason);
            await WaitForAsync(session, x => x.PlayedFrames > resumed.PlayedFrames, "frames after the resume", output);
            var beforePlaying = heartbeats.Count;
            await WaitForHeartbeatsAsync(heartbeats, paused: false, 4, output);
            Assert.DoesNotContain(output.Messages, x => x.Type >= LogMessageType.Warning && x.Text.Contains("Play mode", StringComparison.Ordinal));
            Assert.Null(session.LastScriptException);

            var playing = heartbeats.Skip(beforePlaying + 1).Where(x => !x.Paused).Select(x => x.Heartbeat.Stats).Where(x => x.Frames > 0).ToList();
            TestContext.Current.TestOutputHelper?.WriteLine($"Agent per frame with every option on (median of the heartbeats): playing {Median(playing)} µs over {playing.Sum(x => x.Frames)} frames.");
        }
        finally
        {
            await session.DisposeAsync();
        }

        Assert.True(session.Exited.IsCompleted, "The game still runs.");
        if (processId is { } id)
            Assert.True(HasExited(id), $"The game's process {id} still runs.");
    }

    // A heartbeat with a pause, a step or the JIT of a first call costs more: the median leaves it out
    private static string Median(List<PlayAgentStats> stats)
        => stats.Count == 0 ? "-" : stats.Select(x => x.AgentMeanMicroseconds).Order().ElementAt(stats.Count / 2).ToString("0");

    private static async Task<PlayGameState> WaitForAsync(PlaySession session, Func<PlayGameState, bool> condition, string what, LoggerResult output)
    {
        await WindowTestHelper.WaitUntilAsync(() => session.GameState is { } state && condition(state) || session.Exited.IsCompleted, what, StartTimeout);
        Assert.False(session.Exited.IsCompleted, $"The game exited with {session.ExitCode} while waiting for {what}.{Log(output)}");
        return session.GameState!;
    }

    private static async Task WaitForHeartbeatsAsync(ConcurrentQueue<(bool Paused, PlayHeartbeat Heartbeat)> heartbeats, bool paused, int count, LoggerResult output)
    {
        var target = heartbeats.Count(x => x.Paused == paused) + count;
        await WindowTestHelper.WaitUntilAsync(() => heartbeats.Count(x => x.Paused == paused) >= target, $"{count} heartbeats", StartTimeout);
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
