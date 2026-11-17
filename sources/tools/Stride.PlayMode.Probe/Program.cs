// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Diagnostics;
using System.Globalization;
using Stride.Core.Diagnostics;
using Stride.Editor.PlayMode;
using Stride.PlayMode.Agent;
using Stride.PlayMode.Protocol;

namespace Stride.PlayMode.Probe;

/// <summary>
/// Plays a built game with a play session and prints what its agent sends: the hello, the tree, the changes, the snapshots
/// and the heartbeats. Then it measures the agent idle and with snapshots, and stops the game.
/// </summary>
/// <remarks>
/// Usage: <c>Stride.PlayMode.Probe &lt;game assembly&gt; [--seconds 10] [--rate 5] [--pick Name1,Name2] [--every-frame]
/// [--game-assemblies &lt;folder&gt;]</c>.
/// </remarks>
internal static class Program
{
    private static readonly object ConsoleLock = new();
    private static readonly Stopwatch Clock = Stopwatch.StartNew();

    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args[0].StartsWith("--", StringComparison.Ordinal))
        {
            Console.Error.WriteLine("Usage: Stride.PlayMode.Probe <game assembly> [--seconds 10] [--rate 5] [--pick Name1,Name2] [--every-frame] [--game-assemblies <folder>]");
            return 2;
        }
        var gameAssembly = Path.GetFullPath(args[0]);
        var seconds = double.Parse(Option(args, "--seconds") ?? "10", CultureInfo.InvariantCulture);
        var rate = double.Parse(Option(args, "--rate") ?? "5", CultureInfo.InvariantCulture);
        var picks = (Option(args, "--pick") ?? "PlayerCharacter,Ambient light,Gun,Camera").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var everyFrame = args.Contains("--every-frame");
        if (Option(args, "--game-assemblies") is { } assemblies)
            LoadGameAssemblies(assemblies);

        var output = new LoggerResult();
        output.MessageLogged += (_, e) => Print($"[game output] {e.Message}");
        var gameVersion = PlayAgentFiles.GetGameStrideVersion(gameAssembly);
        Print($"Editor Stride {PlayAgentFiles.EditorStrideVersion}, game Stride {gameVersion ?? "(unknown)"}, agent files {PlayAgentFiles.NextToEditor.Directory} (exist: {PlayAgentFiles.NextToEditor.Exist})");

        var heartbeats = new List<PlayHeartbeat>();
        var snapshots = 0;
        var batches = 0;
        var operations = 0;
        var statuses = new Dictionary<LiveComponentStatus, int>();
        var treeReceived = new TaskCompletionSource<PlayTree>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var session = new PlaySession(new PipePlayTransport(), PlayAgentFiles.NextToEditor, new PlaySessionOptions { Log = output });
        session.StateChanged += (_, e) => Print($"State {e.OldState} -> {e.NewState}{(e.Message is null ? string.Empty : $": {e.Message}")}");
        session.TreeReceived += (_, tree) =>
        {
            Print($"Tree at frame {tree.Frame}: {tree.Scenes.Count} scenes, {tree.Entities.Count} entities");
            treeReceived.TrySetResult(tree);
        };
        session.TreeChanged += (_, batch) =>
        {
            lock (ConsoleLock)
            {
                batches++;
                operations += batch.Operations.Count;
            }
            var kinds = batch.Operations.GroupBy(x => x.GetType().Name).Select(x => $"{x.Key} x{x.Count()}");
            Print($"Changes at frame {batch.Frame}: {string.Join(", ", kinds)}");
        };
        session.SnapshotReceived += (_, snapshot) =>
        {
            lock (ConsoleLock)
            {
                if (snapshots == 0)
                {
                    foreach (var entity in snapshot.Entities)
                    {
                        Print($"First snapshot of {entity.Name}: {string.Join(", ", entity.Components.Select(x => $"{x.TypeName.Split(',')[0].Split('.')[^1]} {x.Status}"))}");
                        foreach (var component in entity.Components.Where(x => x.Status != LiveComponentStatus.Read))
                            Print($"  {component.TypeName}: {component.Error}");
                    }
                }
                snapshots++;
                foreach (var component in snapshot.Entities.SelectMany(x => x.Components))
                    statuses[component.Status] = statuses.GetValueOrDefault(component.Status) + 1;
            }
        };
        session.LogReceived += (_, log) => Print($"[game log {log.Level}] {log.Module}: {log.Text}");
        session.HeartbeatReceived += (_, heartbeat) =>
        {
            lock (ConsoleLock)
                heartbeats.Add(heartbeat);
            var s = heartbeat.Stats;
            Print($"Heartbeat frame {heartbeat.Frame}: {heartbeat.FramesPerSecond:0.0} fps, agent {s.AgentMeanMicroseconds:0.0} µs/frame (max {s.AgentMaxMicroseconds:0}), {s.Snapshots} snapshots {s.SnapshotMeanMicroseconds:0} µs (max {s.SnapshotMaxMicroseconds:0}), {s.BytesSent / 1024} KB sent, {s.DroppedMessages} dropped");
        };

        try
        {
            await session.StartAsync(environment => PlayGameLauncher.Start(gameAssembly, output, environment), gameVersion);
            if (session.State is PlaySessionState.Failed or PlaySessionState.VersionMismatch)
                return 1;
            var tree = await treeReceived.Task.WaitAsync(TimeSpan.FromSeconds(60));
            Print($"Hello: process {session.Hello!.ProcessId}, Stride {session.Hello.StrideVersion}, game {session.Hello.GameType}, {session.Hello.Assemblies.Count} assemblies, stale: [{string.Join(", ", session.Assemblies.StaleAssemblies)}]");
            foreach (var entity in tree.Entities.Take(25))
                Print($"  #{entity.Handle} {entity.Name} (parent #{entity.ParentHandle}, scene #{entity.SceneHandle}, {entity.Components.Count} components{(entity.Enabled ? string.Empty : ", disabled")})");

            await Task.Delay(TimeSpan.FromSeconds(2));
            var idle = await MeasureAsync("Idle (no snapshot)", seconds, heartbeats);

            var picked = tree.Entities.Where(x => picks.Contains(x.Name)).GroupBy(x => x.Name).Select(x => x.First()).ToList();
            if (picked.Count < 4)
                picked.AddRange(tree.Entities.Except(picked).OrderByDescending(x => x.Components.Count).Take(4 - picked.Count));
            Print($"Subscribing to {string.Join(", ", picked.Select(x => $"{x.Name} ({x.Components.Count})"))} at {rate} Hz");
            session.Subscribe(picked.Select(x => x.Handle).ToList(), rate);
            await Task.Delay(TimeSpan.FromSeconds(1));
            var subscribed = await MeasureAsync($"Snapshots at {rate} Hz", seconds, heartbeats);
            PlayHeartbeat[]? fast = null;
            if (everyFrame)
            {
                session.Subscribe(picked.Select(x => x.Handle).ToList(), PlayAgentSystem.MaxRateHz);
                await Task.Delay(TimeSpan.FromSeconds(1));
                fast = await MeasureAsync("Snapshots every frame", seconds, heartbeats);
            }

            Print("Stopping the game");
            var stop = Stopwatch.StartNew();
            await session.StopAsync();
            Print($"Stopped in {stop.ElapsedMilliseconds} ms, state {session.State}, exit code {session.ExitCode}");

            lock (ConsoleLock)
            {
                Print($"Summary: {batches} batches ({operations} operations), {snapshots} snapshots read, components {string.Join(", ", statuses.Select(x => $"{x.Key} {x.Value}"))}");
                Summarize("idle", idle);
                Summarize($"{rate} Hz", subscribed);
                if (fast is not null)
                    Summarize("every frame", fast);
            }
            return session.State == PlaySessionState.GameExited && session.ExitCode == 0 ? 0 : 1;
        }
        catch (Exception e)
        {
            Print($"Failed: {e}");
            return 1;
        }
    }

    private static async Task<PlayHeartbeat[]> MeasureAsync(string name, double seconds, List<PlayHeartbeat> heartbeats)
    {
        int first;
        lock (ConsoleLock)
            first = heartbeats.Count;
        Print($"Measuring: {name}, {seconds} s");
        await Task.Delay(TimeSpan.FromSeconds(seconds));
        lock (ConsoleLock)
            return heartbeats.Skip(first).ToArray();
    }

    private static void Summarize(string name, PlayHeartbeat[] window)
    {
        if (window.Length == 0)
        {
            Print($"  {name}: no heartbeat");
            return;
        }
        var frames = window.Sum(x => x.Stats.Frames);
        var agentMean = window.Sum(x => x.Stats.AgentMeanMicroseconds * x.Stats.Frames) / Math.Max(frames, 1);
        var agentMax = window.Max(x => x.Stats.AgentMaxMicroseconds);
        var count = window.Sum(x => x.Stats.Snapshots);
        var snapshotMean = count > 0 ? window.Sum(x => x.Stats.SnapshotMeanMicroseconds * x.Stats.Snapshots) / count : 0;
        var snapshotMax = window.Max(x => x.Stats.SnapshotMaxMicroseconds);
        var fps = window.Average(x => x.FramesPerSecond);
        var bytes = window[^1].Stats.BytesSent - (window.Length > 1 ? window[0].Stats.BytesSent : 0);
        Print($"  {name}: {frames} frames at {fps:0.0} fps, agent {agentMean:0.0} µs/frame (max {agentMax:0}), {count} snapshots of {snapshotMean:0} µs (max {snapshotMax:0}), {bytes / 1024.0:0.0} KB sent, {window[^1].Stats.DroppedMessages} dropped");
    }

    /// <summary>
    /// Loads the assemblies of a folder that the probe does not have, as the editor has the game's code: the mirrors then
    /// read the game's components (or show them stale, from another build).
    /// </summary>
    private static void LoadGameAssemblies(string directory)
    {
        var loaded = AppDomain.CurrentDomain.GetAssemblies().Select(x => x.GetName().Name).ToHashSet();
        // The default context binds the references of the loaded assemblies by name: it finds them in the folder
        System.Runtime.Loader.AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            var path = Path.Combine(directory, $"{name.Name}.dll");
            return File.Exists(path) ? context.LoadFromAssemblyPath(Path.GetFullPath(path)) : null;
        };
        foreach (var path in Directory.GetFiles(directory, "*.dll"))
        {
            var name = Path.GetFileNameWithoutExtension(path);
            if (loaded.Contains(name) || name.StartsWith("System.", StringComparison.Ordinal) || name.StartsWith("Microsoft.", StringComparison.Ordinal))
                continue;
            try
            {
                var assembly = System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(path));
                // Its serializers register in its module initializer
                System.Runtime.CompilerServices.RuntimeHelpers.RunModuleConstructor(assembly.ManifestModule.ModuleHandle);
                Print($"Loaded {name} ({assembly.ManifestModule.ModuleVersionId})");
            }
            catch (Exception e) when (e is BadImageFormatException or FileLoadException or TypeInitializationException or FileNotFoundException)
            {
                // A native library, or one whose dependencies the probe does not have
            }
        }
    }

    private static string? Option(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static void Print(string text)
    {
        lock (ConsoleLock)
            Console.WriteLine($"[{Clock.Elapsed.TotalSeconds,7:0.000}] {text}");
    }
}
