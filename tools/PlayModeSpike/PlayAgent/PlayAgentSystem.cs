// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Stride.Core;
using Stride.Engine;
using Stride.Games;

namespace Stride.PlayMode.Spike;

/// <summary>
/// The play agent: a game system that runs last in each update, tracks the scene tree, takes snapshots and measures frames.
/// </summary>
internal sealed class PlayAgentSystem : GameSystemBase
{
    private readonly Game game;
    private readonly string? pipeName;
    private readonly bool meterOnly;
    private readonly string? meterOutput;
    private readonly BlockingCollection<(PlayMessageType Type, byte[] Payload)> outgoing = new(boundedCapacity: 256);
    private readonly ConcurrentQueue<(PlayMessageType Type, byte[] Payload)> incoming = new();
    private readonly FrameMeter meter = new();
    private readonly TreeTracker tracker;
    private readonly SnapshotTaker snapshots;
    private readonly FrameStartSystem frameStart;
    private NamedPipeClientStream? pipe;
    private volatile bool connected;
    private long frameIndex;
    private long firstFrameTimestamp;
    private double meterStartSeconds = -1;

    public PlayAgentSystem(Game game, string? pipeName, bool meterOnly, string? meterOutput)
        : base(game.Services)
    {
        this.game = game;
        this.pipeName = pipeName;
        this.meterOnly = meterOnly;
        this.meterOutput = meterOutput;
        tracker = new TreeTracker(Send);
        snapshots = new SnapshotTaker(tracker, Send);
        Enabled = true;
        Visible = true;
        DrawOrder = int.MaxValue;
        frameStart = new FrameStartSystem(game.Services);
        game.GameSystems.Add(frameStart);
        // After the scripts and the scene system: the snapshot sees the state at the end of the frame's update.
        UpdateOrder = int.MaxValue;
        if (!meterOnly && pipeName is not null)
            _ = Task.Run(ConnectAsync);
    }

    public override void Update(GameTime gameTime)
    {
        var start = Stopwatch.GetTimestamp();
        frameIndex++;
        if (firstFrameTimestamp == 0)
            firstFrameTimestamp = start;
        meter.OnFrame(start);
        if (frameStart.UpdateStart != 0)
            meter.OnUpdatePhase(start - frameStart.UpdateStart);

        if (meterOnly)
        {
            RunMeterOnly(gameTime);
            return;
        }

        while (incoming.TryDequeue(out var command))
            Handle(command.Type, command.Payload);

        var sceneInstance = game.SceneSystem.SceneInstance;
        if (connected && sceneInstance is not null)
        {
            tracker.Update(sceneInstance, frameIndex);
            snapshots.Update(frameIndex, gameTime.Total.TotalSeconds, start);
        }

        meter.OnAgentWork(Stopwatch.GetTimestamp() - start);
    }

    public override void Draw(GameTime gameTime)
    {
        if (frameStart.DrawStart != 0)
            meter.OnDrawPhase(Stopwatch.GetTimestamp() - frameStart.DrawStart);
    }

    private void RunMeterOnly(GameTime gameTime)
    {
        // Warm up 15 s, then measure the number of seconds asked for, write the result and end the game.
        var seconds = double.TryParse(Environment.GetEnvironmentVariable("STRIDE_PLAY_METER_SECONDS"), out var s) ? s : 10;
        var now = gameTime.Total.TotalSeconds;
        if (meterStartSeconds < 0 && now >= 15)
        {
            meterStartSeconds = now;
            meter.Reset();
        }
        else if (meterStartSeconds >= 0 && now - meterStartSeconds >= seconds)
        {
            var stats = meter.ToJson("no agent (meter only)", StartupFirstFrameMs());
            if (meterOutput is not null)
                File.WriteAllBytes(meterOutput, stats);
            meterStartSeconds = double.MaxValue;
            game.Exit();
        }
    }

    private double StartupFirstFrameMs()
    {
        var processStart = Process.GetCurrentProcess().StartTime.ToUniversalTime();
        var firstFrame = DateTime.UtcNow - Stopwatch.GetElapsedTime(firstFrameTimestamp);
        return (firstFrame - processStart).TotalMilliseconds;
    }

    private void Handle(PlayMessageType type, byte[] payload)
    {
        switch (type)
        {
            case PlayMessageType.Select:
                snapshots.Select(JsonDocument.Parse(payload).RootElement);
                break;
            case PlayMessageType.ResetStats:
                meter.Reset();
                snapshots.ResetStats();
                break;
            case PlayMessageType.RequestStats:
                var label = payload.Length > 0 ? System.Text.Encoding.UTF8.GetString(payload) : "stats";
                Send(PlayMessageType.Stats, meter.ToJson(label, StartupFirstFrameMs(), snapshots.StatsJson()));
                break;
            case PlayMessageType.Quit:
                game.Exit();
                break;
        }
    }

    private void Send(PlayMessageType type, byte[] payload)
    {
        if (!connected)
            return;
        // Never block the game thread: a full queue drops the message and says so in the next log.
        if (!outgoing.TryAdd((type, payload)))
            snapshots.DroppedMessages++;
    }

    private async Task ConnectAsync()
    {
        try
        {
            pipe = new NamedPipeClientStream(".", pipeName!, PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(10_000);
            connected = true;
            var writer = new Thread(WriteLoop) { IsBackground = true, Name = "Play agent writer" };
            writer.Start();
            Send(PlayMessageType.Hello, HelloJson());
            while (await PlayFraming.ReadAsync(pipe, CancellationToken.None) is { } message)
                incoming.Enqueue(message);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"Play agent: {e.Message}");
        }
        finally
        {
            connected = false;
            outgoing.CompleteAdding();
        }
    }

    private void WriteLoop()
    {
        try
        {
            foreach (var (type, payload) in outgoing.GetConsumingEnumerable())
            {
                PlayFraming.Write(pipe!, type, payload);
                pipe!.Flush();
            }
        }
        catch (IOException)
        {
            // The editor closed the pipe.
        }
    }

    private byte[] HelloJson()
    {
        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream))
        {
            json.WriteStartObject();
            json.WriteNumber("pid", Environment.ProcessId);
            json.WriteString("stride", typeof(Game).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);
            json.WriteString("game", game.GetType().AssemblyQualifiedName);
            json.WriteNumber("hookToGameStartedMs", Stopwatch.GetElapsedTime(AgentBootstrap.HookTimestamp).TotalMilliseconds);
            json.WriteStartArray("assemblies");
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().Where(x => !x.IsDynamic).OrderBy(x => x.GetName().Name))
            {
                var name = assembly.GetName();
                json.WriteStringValue($"{name.Name} {name.Version}");
            }
            json.WriteEndArray();
            json.WriteEndObject();
        }
        return stream.ToArray();
    }
}
