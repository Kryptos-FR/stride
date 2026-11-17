// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace Stride.PlayMode.Spike;

/// <summary>
/// Measures the time between two updates of the game and the time the agent spends in each update.
/// </summary>
internal sealed class FrameMeter
{
    private readonly List<double> frameMs = new(4096);
    private readonly List<double> agentUs = new(4096);
    private readonly List<double> updateMs = new(4096);
    private readonly List<double> drawMs = new(4096);
    private long lastTimestamp;
    private int gen0;
    private int gen1;
    private int gen2;
    private long allocated;
    private long resetTimestamp;

    public FrameMeter() => Reset();

    public void Reset()
    {
        frameMs.Clear();
        agentUs.Clear();
        updateMs.Clear();
        drawMs.Clear();
        lastTimestamp = 0;
        gen0 = GC.CollectionCount(0);
        gen1 = GC.CollectionCount(1);
        gen2 = GC.CollectionCount(2);
        allocated = GC.GetTotalAllocatedBytes();
        resetTimestamp = Stopwatch.GetTimestamp();
    }

    public void OnFrame(long timestamp)
    {
        if (lastTimestamp != 0)
            frameMs.Add(Stopwatch.GetElapsedTime(lastTimestamp, timestamp).TotalMilliseconds);
        lastTimestamp = timestamp;
    }

    /// <summary>The time from the first system's update to the agent's (scripts, physics, scene; not the agent).</summary>
    public void OnUpdatePhase(long ticks) => updateMs.Add(ticks * 1000.0 / Stopwatch.Frequency);

    /// <summary>The time from the first system's draw to the last one's (rendering, before present).</summary>
    public void OnDrawPhase(long ticks) => drawMs.Add(ticks * 1000.0 / Stopwatch.Frequency);

    public void OnAgentWork(long ticks) => agentUs.Add(ticks * 1_000_000.0 / Stopwatch.Frequency);

    public byte[] ToJson(string label, double startToFirstFrameMs, Action<Utf8JsonWriter>? extra = null)
    {
        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            json.WriteStartObject();
            json.WriteString("label", label);
            json.WriteNumber("seconds", Stopwatch.GetElapsedTime(resetTimestamp).TotalSeconds);
            json.WriteNumber("startToFirstFrameMs", startToFirstFrameMs);
            WriteDistribution(json, "frameMs", frameMs);
            WriteDistribution(json, "agentUs", agentUs);
            WriteDistribution(json, "updatePhaseMs", updateMs);
            WriteDistribution(json, "drawPhaseMs", drawMs);
            json.WriteNumber("gen0", GC.CollectionCount(0) - gen0);
            json.WriteNumber("gen1", GC.CollectionCount(1) - gen1);
            json.WriteNumber("gen2", GC.CollectionCount(2) - gen2);
            json.WriteNumber("allocatedBytes", GC.GetTotalAllocatedBytes() - allocated);
            extra?.Invoke(json);
            json.WriteEndObject();
        }
        return stream.ToArray();
    }

    /// <summary>Writes count, mean, median, 95th and 99th percentiles and maximum of a series.</summary>
    public static void WriteDistribution(Utf8JsonWriter json, string name, List<double> values)
    {
        json.WriteStartObject(name);
        json.WriteNumber("count", values.Count);
        if (values.Count > 0)
        {
            var sorted = values.ToArray();
            Array.Sort(sorted);
            double sum = 0;
            foreach (var v in sorted)
                sum += v;
            json.WriteNumber("mean", Math.Round(sum / sorted.Length, 3));
            json.WriteNumber("p50", Math.Round(Percentile(sorted, 0.50), 3));
            json.WriteNumber("p95", Math.Round(Percentile(sorted, 0.95), 3));
            json.WriteNumber("p99", Math.Round(Percentile(sorted, 0.99), 3));
            json.WriteNumber("max", Math.Round(sorted[^1], 3));
        }
        json.WriteEndObject();
    }

    private static double Percentile(double[] sorted, double p)
    {
        var index = (int)Math.Ceiling(p * sorted.Length) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Length - 1)];
    }
}
