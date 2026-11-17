// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Stride.PlayMode.Spike;

// The editor side of the spike: starts the built game with the agent, listens on the pipe, writes the tree dump and
// the snapshots, decodes the snapshots as the editor would, and collects the measurements. It always ends the game.
//
// Usage: PlayHost --game <game assembly> --out <folder> [--no-game-assemblies] [--seconds 10] [--baseline]
//        [--pick name1,name2,...] [--input-script <file>]

var options = Options.Parse(args);
Directory.CreateDirectory(options.Output);

if (options.Baseline)
    return await Baseline.RunAsync(options);

var decoder = new SnapshotDecoder(options);
using var session = new PlaySession(options, decoder);
return await session.RunAsync();

internal sealed record Options(string Game, string Output, string Agent, bool LoadGameAssemblies, int Seconds, bool Baseline, string[] Pick, int ClickMs, bool Quick)
{
    public static Options Parse(string[] args)
    {
        string? game = null, output = null, agent = null;
        var load = true;
        var seconds = 10;
        var baseline = false;
        var pick = Array.Empty<string>();
        var clickMs = 0;
        var quick = false;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--game": game = Path.GetFullPath(args[++i]); break;
                case "--out": output = Path.GetFullPath(args[++i]); break;
                case "--agent": agent = Path.GetFullPath(args[++i]); break;
                case "--no-game-assemblies": load = false; break;
                case "--seconds": seconds = int.Parse(args[++i]); break;
                case "--baseline": baseline = true; break;
                case "--pick": pick = args[++i].Split(','); break;
                case "--click-ms": clickMs = int.Parse(args[++i]); break;
                case "--quick": quick = true; break;
                default: throw new ArgumentException($"Unknown argument {args[i]}");
            }
        }
        agent ??= Path.Combine(AppContext.BaseDirectory, "agent", "Stride.PlayMode.Spike.Agent.dll");
        return new Options(game ?? throw new ArgumentException("--game is required"), output ?? throw new ArgumentException("--out is required"), agent, load, seconds, baseline, pick, clickMs, quick);
    }
}

/// <summary>
/// Starts the game as the editor's game launcher does, with the agent's variables, and makes sure it ends.
/// </summary>
internal static class GameStarter
{
    public static Process Start(Options options, IDictionary<string, string?> environment, string logName)
    {
        // As GameProcessLauncher.CreateStartInfo: the apphost next to the assembly, else the dotnet muxer.
        var appHost = Path.ChangeExtension(options.Game, OperatingSystem.IsWindows() ? ".exe" : null);
        var startInfo = File.Exists(appHost) ? new ProcessStartInfo(appHost) : new ProcessStartInfo("dotnet") { ArgumentList = { options.Game } };
        startInfo.UseShellExecute = false;
        startInfo.WorkingDirectory = Path.GetDirectoryName(options.Game)!;
        startInfo.CreateNoWindow = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        foreach (var (key, value) in environment)
            startInfo.Environment[key] = value;

        var log = new StreamWriter(Path.Combine(options.Output, logName)) { AutoFlush = true };
        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) lock (log) log.WriteLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) lock (log) log.WriteLine("ERR " + e.Data); };
        process.Exited += (_, _) => { lock (log) log.WriteLine($"EXIT {process.ExitCode}"); };
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }

    /// <summary>Waits for the game to end, and kills it after the timeout.</summary>
    public static async Task<int> EndAsync(Process process, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("The game did not exit: killing it.");
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
        }
        return process.ExitCode;
    }
}

/// <summary>
/// Clicks in the game window at random places, so that the game spawns and removes entities (shots, click effects,
/// walking into streamed scenes).
/// </summary>
internal static class Clicker
{
    public static async Task RunAsync(int processId, int intervalMs, CancellationToken cancellationToken)
    {
        string? window = null;
        while (window is null && !cancellationToken.IsCancellationRequested)
        {
            window = (await XdoAsync($"search --pid {processId}")).Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
            if (window is null)
                await Task.Delay(500, CancellationToken.None);
        }
        // "WIDTHxHEIGHT" from the geometry line; clicks stay in the middle of the window.
        var geometry = (await XdoAsync($"getwindowgeometry {window}")).Split("Geometry: ")[^1].Trim().Split('x');
        var (width, height) = (int.Parse(geometry[0]), int.Parse(geometry[1]));
        var random = new Random(1);
        var clicks = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            await XdoAsync($"mousemove --window {window} {random.Next(width / 6, width * 5 / 6)} {random.Next(height / 5, height * 4 / 5)} click 1");
            clicks++;
            try
            {
                await Task.Delay(intervalMs, cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
        }
        Console.WriteLine($"Clicked {clicks} times in window {window}");
    }

    private static async Task<string> XdoAsync(string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo("xdotool", arguments) { RedirectStandardOutput = true, UseShellExecute = false })!;
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        return output;
    }
}

/// <summary>
/// The baseline: the game with the agent in meter mode (no pipe, no tree, no snapshot), which only writes frame times.
/// </summary>
internal static class Baseline
{
    public static async Task<int> RunAsync(Options options)
    {
        var output = Path.Combine(options.Output, "baseline-stats.json");
        File.Delete(output);
        using var process = GameStarter.Start(options, new Dictionary<string, string?>
        {
            ["DOTNET_STARTUP_HOOKS"] = options.Agent,
            [PlayFraming.ModeVariable] = "meter",
            [PlayFraming.MeterOutputVariable] = output,
            ["STRIDE_PLAY_METER_SECONDS"] = options.Seconds.ToString(),
        }, "baseline-game.log");
        try
        {
            var exitCode = await GameStarter.EndAsync(process, TimeSpan.FromSeconds(15 + options.Seconds + 90));
            Console.WriteLine($"Baseline game exited with {exitCode}");
            Console.WriteLine(File.Exists(output) ? File.ReadAllText(output) : "No baseline stats written.");
            return File.Exists(output) ? 0 : 1;
        }
        finally
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
    }
}

/// <summary>
/// One play session: the pipe, the game, the scenario of measurements.
/// </summary>
internal sealed class PlaySession(Options options, SnapshotDecoder decoder) : IDisposable
{
    private readonly string pipeName = "stride-play-" + Guid.NewGuid().ToString("N");
    private readonly TaskCompletionSource<JsonDocument> tree = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<JsonDocument> stats = [];
    private readonly Dictionary<string, int> snapshotCounts = [];
    private readonly HashSet<string> decodedFirst = [];
    private TaskCompletionSource<JsonDocument>? pendingStats;
    private NamedPipeServerStream? pipe;
    private Process? game;
    private StreamWriter? batches;
    private long batchCount;
    private string currentLabel = "";

    public async Task<int> RunAsync()
    {
        pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        var startTimestamp = Stopwatch.GetTimestamp();
        game = GameStarter.Start(options, new Dictionary<string, string?>
        {
            ["DOTNET_STARTUP_HOOKS"] = options.Agent,
            [PlayFraming.PipeVariable] = pipeName,
        }, "game.log");
        try
        {
            using (var connectTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(90)))
                await pipe.WaitForConnectionAsync(connectTimeout.Token);
            Console.WriteLine($"Connected after {Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds:F0} ms");
            batches = new StreamWriter(Path.Combine(options.Output, "tree-batches.jsonl"));
            var reader = Task.Run(ReadLoopAsync);

            var treeDocument = await tree.Task.WaitAsync(TimeSpan.FromSeconds(120));
            Console.WriteLine($"Tree after {Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds:F0} ms");
            decoder.SetTree(treeDocument);
            using var clicking = new CancellationTokenSource();
            var clicker = options.ClickMs > 0 ? Task.Run(() => Clicker.RunAsync(game.Id, options.ClickMs, clicking.Token)) : Task.CompletedTask;
            await RunScenarioAsync(treeDocument);
            clicking.Cancel();
            await clicker;

            Send(PlayMessageType.Quit, []);
            var exitCode = await GameStarter.EndAsync(game, TimeSpan.FromSeconds(20));
            Console.WriteLine($"Game exited with {exitCode}");
            WriteSummary();
            return 0;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Session failed: {e}");
            return 1;
        }
        finally
        {
            if (!game.HasExited)
            {
                game.Kill(entireProcessTree: true);
                await game.WaitForExitAsync();
            }
            batches?.Dispose();
        }
    }

    private async Task RunScenarioAsync(JsonDocument treeDocument)
    {
        if (options.Quick)
        {
            // Activity only: tree batches while the clicker plays, one binary snapshot to decode, and one of YAML.
            var quickPicks = EntityPicker.Pick(treeDocument.RootElement, options.Pick).Select(p => p.Id).ToArray();
            await MeasureAsync("binary at 5 Hz (activity)", quickPicks, 5, "binary");
            await MeasureAsync("yaml at 5 Hz (activity)", quickPicks, 5, "yaml");
            return;
        }

        // Idle: connected, tree sent, no selection.
        await Task.Delay(TimeSpan.FromSeconds(5));
        await MeasureAsync("connected, idle", null, 0, "binary");

        var picks = EntityPicker.Pick(treeDocument.RootElement, options.Pick);
        File.WriteAllLines(Path.Combine(options.Output, "picked-entities.txt"), picks.Select(p => $"{p.Role}: {p.Name} {p.Id} ({p.ComponentCount} components)"));
        foreach (var pick in picks)
            Console.WriteLine($"Picked {pick.Role}: {pick.Name} ({pick.ComponentCount} components)");
        var ids = picks.Select(p => p.Id).ToArray();

        foreach (var format in new[] { "binary", "yaml" })
        {
            await MeasureAsync($"{format} at 5 Hz", ids, 5, format);
            await MeasureAsync($"{format} every frame", ids, 0, format);
        }
        Send(PlayMessageType.Select, JsonSerializer.SerializeToUtf8Bytes(new { ids = Array.Empty<Guid>(), rateHz = 0, format = "binary" }));
    }

    private async Task MeasureAsync(string label, Guid[]? ids, double rateHz, string format)
    {
        Console.WriteLine($"Measuring: {label}");
        currentLabel = label;
        Send(PlayMessageType.Select, JsonSerializer.SerializeToUtf8Bytes(new { ids = ids ?? [], rateHz, format, label }));
        // Let the first snapshot (and its one-time costs) land before the window.
        await Task.Delay(TimeSpan.FromSeconds(2));
        Send(PlayMessageType.ResetStats, []);
        await Task.Delay(TimeSpan.FromSeconds(options.Seconds));
        pendingStats = new TaskCompletionSource<JsonDocument>(TaskCreationOptions.RunContinuationsAsynchronously);
        Send(PlayMessageType.RequestStats, Encoding.UTF8.GetBytes(label));
        stats.Add(await pendingStats.Task.WaitAsync(TimeSpan.FromSeconds(30)));
    }

    private void Send(PlayMessageType type, byte[] payload)
    {
        lock (pipe!)
        {
            PlayFraming.Write(pipe, type, payload);
            pipe.Flush();
        }
    }

    private async Task ReadLoopAsync()
    {
        try
        {
            while (await PlayFraming.ReadAsync(pipe!, CancellationToken.None) is { } message)
                Handle(message.Type, message.Payload);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Read loop ended: {e.Message}");
        }
    }

    private void Handle(PlayMessageType type, byte[] payload)
    {
        switch (type)
        {
            case PlayMessageType.Hello:
                File.WriteAllBytes(Path.Combine(options.Output, "hello.json"), Indent(payload));
                break;
            case PlayMessageType.Tree:
                File.WriteAllBytes(Path.Combine(options.Output, "tree.json"), Indent(payload));
                var document = JsonDocument.Parse(payload);
                File.WriteAllText(Path.Combine(options.Output, "tree-dump.txt"), TreeDump.Write(document.RootElement));
                tree.TrySetResult(document);
                break;
            case PlayMessageType.TreeBatch:
                batchCount++;
                batches!.WriteLine(Encoding.UTF8.GetString(payload));
                batches.Flush();
                break;
            case PlayMessageType.Snapshot:
                var key = currentLabel;
                snapshotCounts[key] = snapshotCounts.GetValueOrDefault(key) + 1;
                var format = (SnapshotFormat)payload[16];
                if (decodedFirst.Add(format.ToString()))
                {
                    File.WriteAllBytes(Path.Combine(options.Output, $"snapshot-{format}.bin"), payload);
                    decoder.Decode(payload, Path.Combine(options.Output, $"decoded-{format}.txt"));
                }
                else
                {
                    decoder.MeasureDecode(payload);
                }
                break;
            case PlayMessageType.Stats:
                pendingStats?.TrySetResult(JsonDocument.Parse(payload));
                break;
        }
    }

    private void WriteSummary()
    {
        using var stream = File.Create(Path.Combine(options.Output, "stats.json"));
        using var json = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        json.WriteStartObject();
        json.WriteNumber("treeBatchesReceived", batchCount);
        json.WriteStartObject("snapshotsReceived");
        foreach (var (key, count) in snapshotCounts)
            json.WriteNumber(key, count);
        json.WriteEndObject();
        json.WriteStartObject("decode");
        decoder.WriteStats(json);
        json.WriteEndObject();
        json.WriteStartArray("windows");
        foreach (var document in stats)
            document.RootElement.WriteTo(json);
        json.WriteEndArray();
        json.WriteEndObject();
    }

    private static byte[] Indent(byte[] payload)
        => JsonSerializer.SerializeToUtf8Bytes(JsonDocument.Parse(payload).RootElement, new JsonSerializerOptions { WriteIndented = true });

    public void Dispose()
    {
        pipe?.Dispose();
        game?.Dispose();
    }
}
