// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections;
using System.Diagnostics;
using Stride.Core.Assets;
using Stride.Core.Assets.Quantum;
using Stride.Core.Mathematics;
using Stride.Core.Quantum;
using Stride.Core.Reflection;
using Stride.Core.Serialization;
using Stride.Engine;
using Stride.Engine.Design;
using Stride.Rendering.Colors;
using Stride.Rendering.Lights;

namespace Stride.GameStudio.Avalonia.PlayModeSpike;

/// <summary>
/// A recorded sequence of snapshots of one entity, and what recording it cost.
/// </summary>
public sealed record MirrorRecording(Entity Runtime, IReadOnlyList<MirrorSnapshot> Snapshots, TimeSpan CaptureTotal)
{
    public TimeSpan CaptureAverage => Snapshots.Count == 0 ? TimeSpan.Zero : CaptureTotal / Snapshots.Count;

    public double AverageSize => Snapshots.Count == 0 ? 0 : Snapshots.Average(x => x.ApproximateSize);
}

/// <summary>
/// Play mode spike: plays the part of the game. It makes a runtime entity from an entity of a scene asset and records a
/// sequence of snapshots in which values change as in a running game: the transform moves, a light changes color and
/// intensity, the public fields of the scripts change, their lists grow.
/// </summary>
public static class MirrorRecorder
{
    /// <summary>
    /// Every how many frames a list of a script gets one more item (a change of structure).
    /// </summary>
    public const int ListGrowthPeriod = 10;

    /// <summary>
    /// Makes a runtime copy of an entity of the editor (an asset-side entity): its children are copied with it, the entities
    /// outside are shared, and the content references keep only their URL, as content loaded by a running game does.
    /// </summary>
    public static Entity MakeRuntimeEntity(Entity assetSideEntity)
    {
        ArgumentNullException.ThrowIfNull(assetSideEntity);
        var runtime = EntityCloner.Clone(assetSideEntity);
        ForgetAssetIds(runtime);
        return runtime;
    }

    /// <summary>
    /// Records <paramref name="frameCount"/> snapshots of <paramref name="runtime"/>, changing its values between two.
    /// </summary>
    /// <param name="growLists">Whether the lists of the scripts grow (a change of structure every <see cref="ListGrowthPeriod"/> frames).</param>
    public static MirrorRecording Record(Entity runtime, int frameCount, bool growLists = true)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        var snapshots = new List<MirrorSnapshot>(frameCount);
        var start = runtime.Transform.Position;
        var capture = TimeSpan.Zero;
        for (var frame = 0; frame < frameCount; frame++)
        {
            var t = frame / 10f;
            runtime.Transform.Position = start + new Vector3(MathF.Cos(t), 0.1f * frame, MathF.Sin(t));
            runtime.Transform.Rotation = Quaternion.RotationY(t);
            foreach (var component in runtime.Components)
            {
                switch (component)
                {
                    case LightComponent light:
                        light.Intensity = 1 + 0.5f * MathF.Sin(t);
                        if (light.Type is IColorLight { Color: ColorRgbProvider color })
                            color.Value = new Color3(0.5f + 0.5f * MathF.Sin(t), 0.5f + 0.5f * MathF.Cos(t), 1 - 0.1f * (frame % 10));
                        break;
                    case ScriptComponent script:
                        ChangeFields(script, frame, growLists);
                        break;
                }
            }
            var stopwatch = Stopwatch.StartNew();
            snapshots.Add(MirrorSnapshotSerializer.Capture(runtime));
            capture += stopwatch.Elapsed;
        }
        return new MirrorRecording(runtime, snapshots, capture);
    }

    private static void ChangeFields(ScriptComponent script, int frame, bool growLists)
    {
        var descriptor = (ObjectDescriptor)TypeDescriptorFactory.Default.Find(script.GetType());
        foreach (var member in descriptor.Members)
        {
            if (member.DeclaringType is { } declaring && (declaring == typeof(ScriptComponent) || declaring == typeof(EntityComponent)))
                continue;
            var value = member.Get(script);
            if (member.HasSet)
            {
                object? newValue = value switch
                {
                    float x => x + 0.25f,
                    double x => x + 0.25,
                    int x => x + 1,
                    bool x => frame % 5 == 0 ? !x : x,
                    string x when x.Length < 64 => $"{x.Split('#')[0]}#{frame}",
                    Vector3 x => x + new Vector3(0.1f, 0, 0),
                    Color3 x => new Color3((x.R + 0.05f) % 1, x.G, x.B),
                    _ => value,
                };
                if (!Equals(newValue, value))
                    member.Set(script, newValue);
            }
            if (growLists && frame > 0 && frame % ListGrowthPeriod == 0 && value is IList list && !list.IsReadOnly)
                GrowList(list, frame);
        }
    }

    private static void GrowList(IList list, int frame)
    {
        var itemType = list.GetType().GetInterfaces().FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IList<>))?.GetGenericArguments()[0];
        object? item = itemType switch
        {
            _ when itemType == typeof(float) => (float)frame,
            _ when itemType == typeof(int) => frame,
            _ when itemType == typeof(string) => $"item {frame}",
            _ when itemType == typeof(Vector3) => new Vector3(frame, 0, 0),
            _ => null,
        };
        if (item is not null)
            list.Add(item);
    }

    /// <summary>
    /// Replaces the content references of the entity's components (proxies with an asset id and a URL, as the asset side has
    /// them) by proxies with the URL only.
    /// </summary>
    private static void ForgetAssetIds(Entity runtime)
    {
        var nodes = new AssetNodeContainer();
        var visited = new HashSet<IGraphNode>();
        foreach (var component in runtime.Components)
            Visit(nodes.GetOrCreateNode(component)!);

        void Visit(IObjectNode node)
        {
            if (!visited.Add(node))
                return;
            foreach (var member in node.Members)
            {
                var value = member.Retrieve();
                if (WithoutId(value) is { } replacement)
                    member.Update(replacement);
                else if (member.Target is { } target && value is not Entity && value is not EntityComponent)
                    Visit(target);
            }
            if (node.IsEnumerable)
            {
                foreach (var index in node.Indices.ToList())
                {
                    var value = node.Retrieve(index);
                    if (WithoutId(value) is { } replacement)
                        node.Update(replacement, index);
                    else if (node.IndexedTarget(index) is { } target && value is not Entity && value is not EntityComponent)
                        Visit(target);
                }
            }
        }

        static object? WithoutId(object? value)
        {
            var reference = AttachedReferenceManager.GetAttachedReference(value);
            if (reference is not { IsProxy: true, Url: { } url } || reference.Id == AssetId.Empty)
                return null;
            return AttachedReferenceManager.CreateProxyObject(value!.GetType(), AssetId.Empty, url);
        }
    }
}
