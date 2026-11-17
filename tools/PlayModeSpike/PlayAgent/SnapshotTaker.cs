// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using Stride.Engine;

namespace Stride.PlayMode.Spike;

/// <summary>
/// Takes snapshots of the selected entities at the end of a frame, at the rate the editor asks for, and measures them.
/// </summary>
/// <remarks>
/// Snapshot message: frame (int64), game time (double), format (byte), entity count (int32), then for each entity its
/// id, name, component count and components (id, type, byte length or -1 and an error, bytes), then its time in
/// stopwatch ticks and its allocated bytes.
/// </remarks>
internal sealed class SnapshotTaker(TreeTracker tracker, Action<PlayMessageType, byte[]> send)
{
    private readonly List<Guid> selection = [];
    private readonly Dictionary<string, Series> series = [];
    private readonly MemoryStream message = new(16384);
    private BinaryComponentWriter? binary;
    private YamlComponentWriter? yaml;
    private SnapshotFormat format = SnapshotFormat.Binary;
    private double rateHz;
    private long lastSnapshotTimestamp;
    private string label = "";

    public int DroppedMessages { get; set; }

    public void Select(JsonElement command)
    {
        selection.Clear();
        foreach (var id in command.GetProperty("ids").EnumerateArray())
            selection.Add(id.GetGuid());
        rateHz = command.GetProperty("rateHz").GetDouble();
        format = command.GetProperty("format").GetString() == "yaml" ? SnapshotFormat.Yaml : SnapshotFormat.Binary;
        label = command.TryGetProperty("label", out var l) ? l.GetString() ?? "" : "";
        lastSnapshotTimestamp = 0;
    }

    public void ResetStats() => series.Clear();

    public void Update(long frame, double gameSeconds, long now)
    {
        if (selection.Count == 0)
            return;
        // rateHz 0: every frame.
        if (rateHz > 0 && lastSnapshotTimestamp != 0 && Stopwatch.GetElapsedTime(lastSnapshotTimestamp, now).TotalSeconds < 1 / rateHz)
            return;
        lastSnapshotTimestamp = now;
        Take(frame, gameSeconds);
    }

    private void Take(long frame, double gameSeconds)
    {
        var totalStart = Stopwatch.GetTimestamp();
        var totalAllocated = GC.GetAllocatedBytesForCurrentThread();
        message.SetLength(0);
        var writer = new BinaryWriter(message, Encoding.UTF8, leaveOpen: true);
        writer.Write(frame);
        writer.Write(gameSeconds);
        writer.Write((byte)format);
        var countPosition = message.Position;
        writer.Write(0);
        var count = 0;
        foreach (var id in selection)
        {
            if (!tracker.Identifiables.TryGetValue(id, out var identifiable) || identifiable is not Entity entity)
                continue;
            count++;
            var start = Stopwatch.GetTimestamp();
            var allocated = GC.GetAllocatedBytesForCurrentThread();
            var entityStart = message.Position;
            writer.Write(entity.Id.ToByteArray());
            writer.Write(entity.Name ?? "");
            writer.Write(entity.Components.Count);
            foreach (var component in entity.Components)
            {
                writer.Write(component.Id.ToByteArray());
                writer.Write(TypeName(component.GetType()));
                var componentStart = Stopwatch.GetTimestamp();
                try
                {
                    var bytes = format == SnapshotFormat.Yaml
                        ? (yaml ??= CreateYamlWriter()).Write(component)
                        : (binary ??= new BinaryComponentWriter()).Write(component, tracker.Identifiables);
                    writer.Write(bytes.Length);
                    writer.Write(bytes);
                }
                catch (Exception e)
                {
                    writer.Write(-1);
                    writer.Write($"{e.GetType().Name}: {e.Message}");
                }
                Record($"component {format} {component.GetType().Name}", Stopwatch.GetElapsedTime(componentStart).TotalMicroseconds, 0, 0);
            }
            var ticks = Stopwatch.GetTimestamp() - start;
            var entityAllocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
            Record($"entity {format} {entity.Name}", ticks * 1_000_000.0 / Stopwatch.Frequency, entityAllocated, message.Position - entityStart);
            writer.Write(ticks);
            writer.Write(entityAllocated);
        }
        var end = message.Position;
        message.Position = countPosition;
        writer.Write(count);
        message.Position = end;
        writer.Flush();
        Record($"snapshot {format} {label}", Stopwatch.GetElapsedTime(totalStart).TotalMicroseconds, GC.GetAllocatedBytesForCurrentThread() - totalAllocated, message.Length);
        send(PlayMessageType.Snapshot, message.ToArray());
    }

    private static YamlComponentWriter CreateYamlWriter()
    {
        var start = Stopwatch.GetTimestamp();
        var writer = new YamlComponentWriter();
        Console.WriteLine($"Play agent: YAML writer created in {Stopwatch.GetElapsedTime(start).TotalMilliseconds:F1} ms");
        return writer;
    }

    private static string TypeName(Type type) => $"{type.FullName}, {type.Assembly.GetName().Name}";

    private void Record(string key, double microseconds, long allocated, long size)
    {
        if (!series.TryGetValue(key, out var s))
            series[key] = s = new Series();
        s.Microseconds.Add(microseconds);
        s.Allocated.Add(allocated);
        s.Size.Add(size);
    }

    public Action<Utf8JsonWriter> StatsJson() => json =>
    {
        json.WriteNumber("droppedMessages", DroppedMessages);
        json.WriteNumber("treeBatches", tracker.BatchesSent);
        json.WriteNumber("treeOps", tracker.OpsSent);
        json.WriteNumber("treeOpsCoalesced", tracker.OpsCoalesced);
        json.WriteNumber("idCollisions", tracker.IdCollisions);
        json.WriteNumber("sharedIdEntities", tracker.SharedIdEntities);
        json.WriteStartArray("resolvedFromAgent");
        lock (AgentBootstrap.ResolvedFromAgent)
        {
            foreach (var name in AgentBootstrap.ResolvedFromAgent)
                json.WriteStringValue(name);
        }
        json.WriteEndArray();
        json.WriteStartObject("series");
        foreach (var (key, s) in series)
        {
            json.WriteStartObject(key);
            FrameMeter.WriteDistribution(json, "us", s.Microseconds);
            FrameMeter.WriteDistribution(json, "allocatedBytes", s.Allocated);
            FrameMeter.WriteDistribution(json, "bytes", s.Size);
            json.WriteEndObject();
        }
        json.WriteEndObject();
    };

    private sealed class Series
    {
        public List<double> Microseconds { get; } = [];

        public List<double> Allocated { get; } = [];

        public List<double> Size { get; } = [];
    }
}
