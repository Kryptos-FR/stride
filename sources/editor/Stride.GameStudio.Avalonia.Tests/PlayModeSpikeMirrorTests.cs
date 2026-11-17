// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Diagnostics;
using System.Globalization;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Stride.Assets.Models;
using Stride.Core;
using Stride.Core.Assets;
using Stride.Core.Mathematics;
using Stride.Core.Presentation.Avalonia.Controls;
using Stride.Core.Presentation.Quantum.ViewModels;
using Stride.Core.Serialization;
using Stride.Engine;
using Stride.GameStudio.Avalonia.PlayModeSpike;
using Stride.GameStudio.Avalonia.Tests.Helpers;
using Stride.GameStudio.Avalonia.Views;
using Stride.Particles;
using Stride.Particles.Components;
using Stride.Particles.Initializers;
using Stride.Particles.Modules;
using Stride.Rendering;
using Stride.Rendering.Colors;
using Stride.Rendering.Lights;

namespace Stride.GameStudio.Avalonia.Tests;

/// <summary>
/// Play mode spike (S2): the property grid on a mirror entity, fed from recorded snapshots and updated in place.
/// </summary>
public sealed class PlayModeSpikeMirrorTests
{
    private const string ModelLocation = "Models/Box";
    private const int FrameCount = 50;

    [AvaloniaFact]
    public async Task TheGridFollowsTheSnapshotsInPlaceAndRebuildsOnlyWhenTheStructureChanges()
    {
        using var fixture = await ShellSessionFixture.OpenAsync((ModelLocation, new ModelAsset()));
        var (player, _) = MakeSmallScene();
        var recording = MirrorRecorder.Record(player, FrameCount);
        var properties = new MirrorPropertiesViewModel(fixture.Session);
        var window = new Window { Width = 500, Height = 900, Content = new PropertyGridView { DataContext = properties } };
        await window.ShowAsync();
        try
        {
            await properties.ApplyAsync(recording.Snapshots[0]);
            await WindowTestHelper.PumpAsync();
            var viewModel = properties.ViewModel!;
            Assert.Equal(1, properties.BuildCount);
            var rebuilds = 0;
            for (var frame = 1; frame < FrameCount; frame++)
            {
                var timing = await properties.ApplyAsync(recording.Snapshots[frame]);
                await WindowTestHelper.PumpAsync();
                var listGrew = frame % MirrorRecorder.ListGrowthPeriod == 0;
                Assert.Equal(listGrew, timing.Rebuilt);
                if (timing.Rebuilt)
                {
                    ++rebuilds;
                    Assert.Contains(nameof(MirrorTestScript.Values), timing.Result!.StructuralChange);
                    viewModel = properties.ViewModel!;
                }
                else
                {
                    // The same view models, with the values of the snapshot
                    Assert.Same(viewModel, properties.ViewModel);
                    Assert.True(timing.Result!.WrittenValues > 0);
                }

                var expected = MirrorSnapshotSerializer.Restore(recording.Snapshots[frame]);
                Assert.Equal(expected.Transform.Position, Node(viewModel, "Components", typeof(TransformComponent), nameof(TransformComponent.Position)).NodeValue);
                Assert.Equal(expected.Get<LightComponent>()!.Intensity, Node(viewModel, "Components", typeof(LightComponent), nameof(LightComponent.Intensity)).NodeValue);
                Assert.Equal(expected.Get<MirrorTestScript>()!.Speed, Node(viewModel, "Components", typeof(MirrorTestScript), nameof(MirrorTestScript.Speed)).NodeValue);
            }
            Assert.Equal((FrameCount - 1) / MirrorRecorder.ListGrowthPeriod, rebuilds);
            Assert.Equal(1 + rebuilds, properties.BuildCount);

            // The control shows the value of the last snapshot
            var last = MirrorSnapshotSerializer.Restore(recording.Snapshots[^1]);
            (await ItemAsync(window, x => x.Name == "Components")).IsExpanded = true;
            (await ItemAsync(window, x => x.NodeValue is MirrorTestScript)).IsExpanded = true;
            var speedItem = await ItemAsync(window, x => x.NodeValue is float && x.Name == nameof(MirrorTestScript.Speed));
            // The editor of the value (the first text box that holds a number; the name is also in a text box)
            var speedText = speedItem.GetVisualDescendants().OfType<TextBox>().Select(x => x.Text).First(x => float.TryParse(x, NumberStyles.Float, CultureInfo.CurrentCulture, out _));
            Assert.Equal(last.Get<MirrorTestScript>()!.Speed, float.Parse(speedText!, CultureInfo.CurrentCulture));
        }
        finally
        {
            await window.CloseAsync();
            properties.ViewModel?.Destroy();
        }
    }

    [AvaloniaFact]
    public async Task TheColorEditorFollowsTheSnapshots()
    {
        // The color of a light is an inlined value ([InlineProperty] on ColorRgbProvider.Value): each write refreshes the parent
        // presenter, which needs FinalizeTree to inline the value again. With BatchFinalizeTree, the editor stays on the first
        // written value (see the report).
        using var fixture = await ShellSessionFixture.OpenAsync((ModelLocation, new ModelAsset()));
        var (player, _) = MakeSmallScene();
        var recording = MirrorRecorder.Record(player, 6, growLists: false);
        var properties = new MirrorPropertiesViewModel(fixture.Session);
        var window = new Window { Width = 500, Height = 1400, Content = new PropertyGridView { DataContext = properties } };
        await window.ShowAsync();
        try
        {
            await properties.ApplyAsync(recording.Snapshots[0]);
            await WindowTestHelper.PumpAsync();
            (await ItemAsync(window, x => x.Name == "Components")).IsExpanded = true;
            (await ItemAsync(window, x => x.NodeValue is LightComponent)).IsExpanded = true;
            (await ItemAsync(window, x => x.NodeValue is LightPoint)).IsExpanded = true;
            await WindowTestHelper.PumpAsync();
            for (var frame = 1; frame < 6; frame++)
            {
                await properties.ApplyAsync(recording.Snapshots[frame]);
                await WindowTestHelper.PumpAsync();
                var expected = ((ColorRgbProvider)((LightPoint)MirrorSnapshotSerializer.Restore(recording.Snapshots[frame]).Get<LightComponent>()!.Type).Color).Value;
                // The editor shows the color as text (#RRGGBB): a byte per channel
                var shown = window.GetVisualDescendants().OfType<Color3Editor>().Single().Value;
                Assert.True(Math.Abs(shown.R - expected.R) <= 1 / 255f && Math.Abs(shown.G - expected.G) <= 1 / 255f && Math.Abs(shown.B - expected.B) <= 1 / 255f,
                    $"frame {frame}: the editor shows {shown}, the snapshot holds {expected}");
            }
        }
        finally
        {
            await window.CloseAsync();
            properties.ViewModel?.Destroy();
        }
    }

    [AvaloniaFact]
    public async Task AListThatGrowsIsChangedInPlaceWhenAsked()
    {
        using var fixture = await ShellSessionFixture.OpenAsync((ModelLocation, new ModelAsset()));
        var (player, _) = MakeSmallScene();
        var recording = MirrorRecorder.Record(player, 2 * MirrorRecorder.ListGrowthPeriod + 1);
        var properties = new MirrorPropertiesViewModel(fixture.Session) { InPlaceCollections = true };
        await properties.ApplyAsync(recording.Snapshots[0]);
        try
        {
            var viewModel = properties.ViewModel!;
            var values = Node(viewModel, "Components", typeof(MirrorTestScript), nameof(MirrorTestScript.Values));
            var count = values.Children.Count;
            for (var frame = 1; frame < recording.Snapshots.Count; frame++)
            {
                var timing = await properties.ApplyAsync(recording.Snapshots[frame]);
                Assert.False(timing.Rebuilt);
                Assert.Same(viewModel, properties.ViewModel);
                if (frame % MirrorRecorder.ListGrowthPeriod == 0)
                    Assert.Equal(2, timing.Result!.AddedItems); // Values and Points
            }
            Assert.Equal(1, properties.BuildCount);
            values = Node(viewModel, "Components", typeof(MirrorTestScript), nameof(MirrorTestScript.Values));
            Assert.Equal(count + 2, values.Children.Count);
            Assert.Equal(MirrorSnapshotSerializer.Restore(recording.Snapshots[^1]).Get<MirrorTestScript>()!.Values, (List<float>)values.NodeValue!);
        }
        finally
        {
            properties.ViewModel?.Destroy();
        }
    }

    [AvaloniaFact]
    public async Task ReferencesShowTheObjectsOutsideOfTheMirrorAsStandIns()
    {
        using var fixture = await ShellSessionFixture.OpenAsync((ModelLocation, new ModelAsset()));
        var (player, target) = MakeSmallScene();
        var snapshot = MirrorSnapshotSerializer.Capture(player);
        var properties = new MirrorPropertiesViewModel(fixture.Session);
        await properties.ApplyAsync(snapshot);
        try
        {
            var viewModel = properties.ViewModel!;
            // Another entity: a stand-in with its id and name, shown as a reference (no members)
            var targetNode = Node(viewModel, "Components", typeof(MirrorTestScript), nameof(MirrorTestScript.Target));
            var standIn = Assert.IsType<Entity>(targetNode.NodeValue);
            Assert.NotSame(target, standIn);
            Assert.Equal((target.Id, target.Name), (standIn.Id, standIn.Name));
            Assert.Empty(targetNode.Children);
            // A component of another entity: a stand-in component on the entity's stand-in
            var cameraNode = Node(viewModel, "Components", typeof(MirrorTestScript), nameof(MirrorTestScript.TargetCamera));
            var camera = Assert.IsType<CameraComponent>(cameraNode.NodeValue);
            Assert.True(target.Name == camera.Entity?.Name, MirrorSnapshotSerializer.SerializerOf(typeof(CameraComponent)) + " " + string.Join(", ", snapshot.Shared));
            Assert.Empty(cameraNode.Children);
            // Content: by URL, without the asset id a running game does not know
            var modelNode = Node(viewModel, "Components", typeof(ModelComponent), nameof(ModelComponent.Model));
            var reference = AttachedReferenceManager.GetAttachedReference(modelNode.NodeValue);
            Assert.Equal((ModelLocation, AssetId.Empty), (reference?.Url, reference?.Id ?? default));
            // Read-only: the node presenters refuse the writes of the view
            var speed = Node(viewModel, "Components", typeof(MirrorTestScript), nameof(MirrorTestScript.Speed));
            var before = speed.NodeValue;
            speed.NodeValue = 1234f;
            Assert.Equal(before, speed.NodeValue);
            Assert.True(speed.IsReadOnly);
        }
        finally
        {
            properties.ViewModel?.Destroy();
        }
    }

    [AvaloniaFact]
    public async Task AProtectedValueIsNotWrittenUntilItIsReleased()
    {
        using var fixture = await ShellSessionFixture.OpenAsync((ModelLocation, new ModelAsset()));
        var (player, _) = MakeSmallScene();
        var recording = MirrorRecorder.Record(player, 3, growLists: false);
        var properties = new MirrorPropertiesViewModel(fixture.Session);
        await properties.ApplyAsync(recording.Snapshots[0]);
        try
        {
            var intensity = Node(properties.ViewModel!, "Components", typeof(LightComponent), nameof(LightComponent.Intensity));
            var accessor = intensity.NodePresenters.Single().GetNodeAccessor();
            var before = intensity.NodeValue;
            properties.IsProtected = (node, index) => node == accessor.Node && index == accessor.Index;

            var timing = await properties.ApplyAsync(recording.Snapshots[1]);
            Assert.Equal(before, intensity.NodeValue);
            Assert.True(timing.Result!.SkippedValues > 0);

            properties.IsProtected = null;
            await properties.ApplyAsync(recording.Snapshots[2]);
            Assert.Equal(MirrorSnapshotSerializer.Restore(recording.Snapshots[2]).Get<LightComponent>()!.Intensity, intensity.NodeValue);
        }
        finally
        {
            properties.ViewModel?.Destroy();
        }
    }

    /// <summary>
    /// The numbers of the spike's report: apply in place and rebuild, for a small and a big entity. The results go to the test
    /// output, and to the file named by the variable PLAY_MODE_SPIKE_TIMINGS when it is set.
    /// </summary>
    [AvaloniaFact]
    public async Task MeasureApplyAndRebuild()
    {
        using var fixture = await ShellSessionFixture.OpenAsync((ModelLocation, new ModelAsset()));
        var report = new StringBuilder();
        report.AppendLine("entity | variant | snapshot bytes | capture ms | restore ms | apply in place ms (median/p95) | layout after apply ms (median) | values compared | written | rebuild ms (median/p95) | layout after rebuild ms (median) | grid nodes");
        foreach (var (name, make) in new (string, Func<Entity>)[] { ("small", () => MakeSmallScene().Player), ("big", MakeBigEntity) })
        {
            // A first pass warms up (JIT, templates); the second is reported
            for (var pass = 0; pass < 2; pass++)
            {
                foreach (var (variant, withView, batch) in new[] { ("grid shown", true, false), ("grid shown, FinalizeTree batched", true, true), ("no view", false, false), ("no view, FinalizeTree batched", false, true) })
                {
                    var row = await MeasureAsync(fixture, name, make(), variant, withView, batch);
                    if (pass == 1)
                        report.AppendLine(row);
                }
            }
        }
        report.AppendLine();
        report.AppendLine("entity | a list grows: | rebuild, ms (median) | layout, ms (median) | in place, ms (median) | layout, ms (median)");
        foreach (var (name, make) in new (string, Func<Entity>)[] { ("small", () => MakeSmallScene().Player), ("big", MakeBigEntity) })
            report.AppendLine(await MeasureGrowthAsync(fixture, name, make()));
        report.AppendLine();
        report.AppendLine("updaters that failed on a mirror:");
        foreach (var (updater, error) in MirrorGuardedUpdater.Failures)
            report.AppendLine($"- {updater}: {error}");
        TestContext.Current.TestOutputHelper?.WriteLine(report.ToString());
        if (Environment.GetEnvironmentVariable("PLAY_MODE_SPIKE_TIMINGS") is { Length: > 0 } path)
            File.AppendAllText(path, report.ToString());
    }

    private static async Task<string> MeasureAsync(ShellSessionFixture fixture, string name, Entity entity, string variant, bool withView, bool batch)
    {
        var recording = MirrorRecorder.Record(entity, FrameCount, growLists: false);
        var properties = new MirrorPropertiesViewModel(fixture.Session) { BatchFinalizeTree = batch };
        var window = withView ? new Window { Width = 500, Height = 900, Content = new PropertyGridView { DataContext = properties } } : null;
        if (window is not null)
            await window.ShowAsync();
        try
        {
            await properties.ApplyAsync(recording.Snapshots[0]);
            await WindowTestHelper.PumpAsync();
            if (window is not null)
            {
                // The components and the first level of their members are shown, as in the scene editor
                (await ItemAsync(window, x => x.Name == "Components")).IsExpanded = true;
                await WindowTestHelper.PumpAsync();
            }
            // Warm up (JIT) on a few frames, then measure the others
            var applies = new List<double>();
            var applyLayouts = new List<double>();
            var restores = new List<double>();
            MirrorApplyResult? result = null;
            // Twice through the recording (it loops back to the first frame), 100 applies
            for (var step = 1; step < 2 * FrameCount; step++)
            {
                var frame = step % FrameCount;
                var timing = await properties.ApplyAsync(recording.Snapshots[frame]);
                Assert.False(timing.Rebuilt);
                var layout = Stopwatch.StartNew();
                await WindowTestHelper.PumpAsync();
                if (step < 5)
                    continue;
                applies.Add(timing.Total.TotalMilliseconds);
                restores.Add(timing.Restore!.Value.TotalMilliseconds);
                applyLayouts.Add(layout.Elapsed.TotalMilliseconds);
                result = timing.Result;
            }

            var rebuilds = new List<double>();
            var rebuildLayouts = new List<double>();
            for (var i = 0; i < 22; i++)
            {
                var timing = await properties.ShowAsync(MirrorSnapshotSerializer.Restore(recording.Snapshots[i]));
                var layout = Stopwatch.StartNew();
                await WindowTestHelper.PumpAsync();
                if (i < 2)
                    continue;
                rebuilds.Add(timing.Total.TotalMilliseconds);
                rebuildLayouts.Add(layout.Elapsed.TotalMilliseconds);
            }
            var nodeCount = CountNodes(properties.ViewModel!.RootNode);
            return string.Create(CultureInfo.InvariantCulture,
                $"{name} | {variant} | {recording.AverageSize:F0} | {recording.CaptureAverage.TotalMilliseconds:F3} | {Percentile(restores, 0.5):F3} | {Percentile(applies, 0.5):F2}/{Percentile(applies, 0.95):F2} | {Percentile(applyLayouts, 0.5):F2} | {result!.ComparedValues} | {result.WrittenValues} | {Percentile(rebuilds, 0.5):F1}/{Percentile(rebuilds, 0.95):F1} | {Percentile(rebuildLayouts, 0.5):F1} | {nodeCount}");
        }
        finally
        {
            if (window is not null)
                await window.CloseAsync();
            properties.ViewModel?.Destroy();
        }
    }

    /// <summary>
    /// A player with a transform, a light, a model and a script; the script references another entity and its camera.
    /// </summary>
    private static (Entity Player, Entity Target) MakeSmallScene()
    {
        var target = new Entity("Target") { new CameraComponent() };
        var player = new Entity("Player")
        {
            new LightComponent { Type = new LightPoint { Color = new ColorRgbProvider(new Color3(1, 0.5f, 0.25f)) }, Intensity = 2 },
            new ModelComponent { Model = AttachedReferenceManager.CreateProxyObject<Model>(AssetId.Empty, ModelLocation) },
            new MirrorTestScript { Target = target, TargetCamera = target.Get<CameraComponent>() },
        };
        return (player, target);
    }

    /// <summary>
    /// An entity with many components and big collections: a particle system, a model with materials, lights, a camera,
    /// a script with a list of 200 vectors and references to 20 entities.
    /// </summary>
    private static Entity MakeBigEntity()
    {
        var others = Enumerable.Range(0, 20).Select(i => new Entity($"Other {i}") { new CameraComponent() }).ToList();
        var particles = new ParticleSystemComponent();
        for (var i = 0; i < 4; i++)
        {
            var emitter = new ParticleEmitter { EmitterName = $"Emitter {i}" };
            emitter.Initializers.Add(new InitialPositionSeed());
            emitter.Initializers.Add(new InitialVelocitySeed());
            emitter.Initializers.Add(new InitialSizeSeed());
            emitter.Updaters.Add(new UpdaterGravity());
            particles.ParticleSystem.Emitters.Add(emitter);
        }
        var model = new ModelComponent { Model = AttachedReferenceManager.CreateProxyObject<Model>(AssetId.Empty, ModelLocation) };
        for (var i = 0; i < 4; i++)
            model.Materials[i] = AttachedReferenceManager.CreateProxyObject<Material>(AssetId.Empty, $"Materials/M{i}");
        var script = new MirrorTestScript { Target = others[0], TargetCamera = others[1].Get<CameraComponent>() };
        for (var i = 0; i < 200; i++)
            script.Points.Add(new Vector3(i, 0, 0));
        script.Others.AddRange(others);
        return new Entity("Big")
        {
            model,
            particles,
            new LightComponent { Type = new LightPoint(), Intensity = 2 },
            new LightComponent { Type = new LightSpot(), Intensity = 3 },
            new CameraComponent(),
            new BackgroundComponent(),
            new SpriteComponent(),
            script,
            new MirrorTestScript(),
        };
    }

    /// <summary>
    /// The frames where a list of the scripts grows: the grid rebuilt, or the list changed in place.
    /// </summary>
    private static async Task<string> MeasureGrowthAsync(ShellSessionFixture fixture, string name, Entity entity)
    {
        var recording = MirrorRecorder.Record(entity, FrameCount);
        var results = new List<double>[4];
        for (var i = 0; i < results.Length; i++)
            results[i] = [];
        foreach (var inPlace in new[] { false, true, false, true })
        {
            var properties = new MirrorPropertiesViewModel(fixture.Session) { BatchFinalizeTree = true, InPlaceCollections = inPlace };
            var window = new Window { Width = 500, Height = 900, Content = new PropertyGridView { DataContext = properties } };
            await window.ShowAsync();
            try
            {
                await properties.ApplyAsync(recording.Snapshots[0]);
                await WindowTestHelper.PumpAsync();
                (await ItemAsync(window, x => x.Name == "Components")).IsExpanded = true;
                await WindowTestHelper.PumpAsync();
                for (var frame = 1; frame < FrameCount; frame++)
                {
                    var timing = await properties.ApplyAsync(recording.Snapshots[frame]);
                    var layout = Stopwatch.StartNew();
                    await WindowTestHelper.PumpAsync();
                    if (frame % MirrorRecorder.ListGrowthPeriod != 0)
                        continue;
                    Assert.Equal(!inPlace, timing.Rebuilt);
                    var column = inPlace ? 2 : 0;
                    results[column].Add(timing.Total.TotalMilliseconds);
                    results[column + 1].Add(layout.Elapsed.TotalMilliseconds);
                }
            }
            finally
            {
                await window.CloseAsync();
                properties.ViewModel?.Destroy();
            }
        }
        return string.Create(CultureInfo.InvariantCulture,
            $"{name} | | {Percentile(results[0], 0.5):F1} | {Percentile(results[1], 0.5):F1} | {Percentile(results[2], 0.5):F1} | {Percentile(results[3], 0.5):F1}");
    }

    private static double Percentile(List<double> values, double percentile)
    {
        var sorted = values.Order().ToList();
        return sorted[(int)Math.Min(sorted.Count - 1, Math.Round(percentile * (sorted.Count - 1)))];
    }

    private static int CountNodes(NodeViewModel node) => 1 + node.Children.Sum(CountNodes);

    private static NodeViewModel Node(GraphViewModel viewModel, string components, Type componentType, params string[] path)
    {
        var node = viewModel.RootNode.Children.Single(x => x.Name == components).Children.First(x => x.NodeValue?.GetType() == componentType);
        foreach (var member in path)
            node = node.Children.Single(x => x.Name == member);
        return node;
    }

    private static IEnumerable<NodeViewModel> Descendants(NodeViewModel node) => node.Children.SelectMany(x => Descendants(x).Prepend(x));

    private static async Task<PropertyViewItem> ItemAsync(Window window, Func<NodeViewModel, bool> match)
    {
        IEnumerable<PropertyViewItem> Items() => window.GetVisualDescendants().OfType<PropertyViewItem>();
        await WindowTestHelper.WaitUntilAsync(() => Items().Any(x => x.DataContext is NodeViewModel node && match(node)),
            "the property among " + string.Join(", ", Items().Select(x => (x.DataContext as NodeViewModel)?.Name)));
        return Items().First(x => x.DataContext is NodeViewModel node && match(node));
    }
}

/// <summary>
/// A script with the kinds of public members a game's script has: values, a list, references to another entity and component.
/// </summary>
[DataContract]
public sealed class MirrorTestScript : SyncScript
{
    public float Speed { get; set; } = 1;

    public int Count { get; set; }

    public bool Enabled2 { get; set; } = true;

    public string Label { get; set; } = "label";

    public Vector3 Offset { get; set; }

    public List<float> Values { get; } = [1, 2];

    public List<Vector3> Points { get; } = [];

    public Entity? Target { get; set; }

    public CameraComponent? TargetCamera { get; set; }

    public List<Entity> Others { get; } = [];

    public override void Update()
    {
    }
}
