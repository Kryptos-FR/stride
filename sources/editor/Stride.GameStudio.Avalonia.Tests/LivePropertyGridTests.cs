// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Stride.Assets.Editor.PlayMode;
using Stride.Assets.Models;
using Stride.Core;
using Stride.Core.Assets;
using Stride.Core.Assets.Editor.Avalonia.Converters;
using Stride.Core.Assets.Editor.Quantum.NodePresenters.Commands;
using Stride.Core.Assets.Presentation.Services;
using Stride.Core.Diagnostics;
using Stride.Core.Mathematics;
using Stride.Core.Quantum;
using Stride.Core.Presentation.Avalonia.Controls;
using Stride.Core.Presentation.Quantum.ViewModels;
using Stride.Core.Serialization;
using Stride.Core.Yaml;
using Stride.Editor.PlayMode;
using Stride.Engine;
using Stride.GameStudio.Avalonia.Tests.Helpers;
using Stride.GameStudio.Avalonia.Views;
using Stride.PlayMode.Agent;
using Stride.PlayMode.Protocol;
using Stride.Rendering;
using Stride.Rendering.Colors;
using Stride.Rendering.Lights;

namespace Stride.GameStudio.Avalonia.Tests;

/// <summary>
/// The property grid of a live entity: the snapshots a game's agent writes, read into mirrors and applied to the grid.
/// </summary>
public sealed class LivePropertyGridTests
{
    private const string ModelLocation = "Models/Box";
    private readonly Entity target = new("Target") { new CameraComponent() };
    private readonly Entity player;
    private readonly LiveTestScript script;
    private readonly LightComponent light;
    private readonly ModelComponent model = new();
    private readonly Dictionary<Entity, int> handles;
    private long frame;

    public LivePropertyGridTests()
    {
        script = new LiveTestScript { Target = target, TargetCamera = target.Get<CameraComponent>() };
        light = new LightComponent { Type = new LightPoint { Color = new ColorRgbProvider(new Color3(1, 0.5f, 0.25f)) }, Intensity = 2 };
        player = new Entity("Player") { light, model, script };
        handles = new(ReferenceEqualityComparer.Instance) { [player] = 1, [target] = 2 };
    }

    [AvaloniaFact]
    public async Task TheGridFollowsTheSnapshotsInPlace()
    {
        using var fixture = await OpenAsync();
        // Every snapshot is applied, however long the test machine takes (see AnEntitySlowToShowIsRefreshedLessOften)
        var properties = new LivePropertiesViewModel(fixture.Session) { Throttle = new LiveRefreshThrottle { Budget = TimeSpan.FromSeconds(10) } };
        var window = new Window { Width = 500, Height = 1400, Content = new PropertyGridView { DataContext = properties } };
        await window.ShowAsync();
        try
        {
            var first = await properties.ApplyAsync(Snapshot());
            await WindowTestHelper.PumpAsync();
            Assert.True(first.Rebuilt);
            var viewModel = properties.ViewModel!;
            (await ItemAsync(window, x => x.Name == "Components")).IsExpanded = true;
            (await ItemAsync(window, x => x.NodeValue is LightComponent)).IsExpanded = true;
            (await ItemAsync(window, x => x.NodeValue is LightPoint)).IsExpanded = true;
            (await ItemAsync(window, x => x.NodeValue is LiveTestScript)).IsExpanded = true;
            await WindowTestHelper.PumpAsync();

            for (var i = 1; i <= 5; i++)
            {
                player.Transform.Position = new Vector3(i, 2 * i, 0);
                light.Intensity = 2 + i;
                ((ColorRgbProvider)((LightPoint)light.Type).Color).Value = new Color3(0.1f * i, 0.5f, 0.2f);
                script.Speed = 10 * i;
                script.Values[0] = i;

                var applied = await properties.ApplyAsync(Snapshot());
                await WindowTestHelper.PumpAsync();

                // In place: the same view models, with the game's values
                Assert.False(applied.Rebuilt);
                Assert.Same(viewModel, properties.ViewModel);
                Assert.True(applied.Entities.Single().Result.WrittenValues > 0);
                Assert.Equal(player.Transform.Position, Node(viewModel, typeof(TransformComponent), nameof(TransformComponent.Position)).NodeValue);
                Assert.Equal(light.Intensity, Node(viewModel, typeof(LightComponent), nameof(LightComponent.Intensity)).NodeValue);
                Assert.Equal(script.Speed, Node(viewModel, typeof(LiveTestScript), nameof(LiveTestScript.Speed)).NodeValue);
                Assert.Equal(script.Values, (List<float>)Node(viewModel, typeof(LiveTestScript), nameof(LiveTestScript.Values)).NodeValue!);
                // The color is an inlined value: its editor follows too (a byte per channel)
                var expected = ((ColorRgbProvider)((LightPoint)light.Type).Color).Value;
                var shown = window.GetVisualDescendants().OfType<Color3Editor>().Single().Value;
                Assert.True(Math.Abs(shown.R - expected.R) <= 1 / 255f && Math.Abs(shown.G - expected.G) <= 1 / 255f && Math.Abs(shown.B - expected.B) <= 1 / 255f,
                    $"snapshot {i}: the editor shows {shown}, the game has {expected}");
            }
            Assert.Equal(1, properties.BuildCount);
            Assert.Equal($"Frame {frame}", properties.LiveStatus);

            // The text box of the speed shows the last value
            var speedItem = await ItemAsync(window, x => x.NodeValue is float && x.Name == nameof(LiveTestScript.Speed));
            var speedText = speedItem.GetVisualDescendants().OfType<TextBox>().Select(x => x.Text).First(x => float.TryParse(x, NumberStyles.Float, CultureInfo.CurrentCulture, out _));
            Assert.Equal(script.Speed, float.Parse(speedText!, CultureInfo.CurrentCulture));
        }
        finally
        {
            await window.CloseAsync();
            properties.ViewModel?.Destroy();
        }
    }

    [AvaloniaFact]
    public async Task WithEditsOffEveryEditorOfTheLiveGridIsReadOnly()
    {
        using var fixture = await OpenAsync();
        // A camera: a ranged slider (field of view), a combo box (projection), check boxes and numbers
        player.Add(new CameraComponent());
        var properties = new LivePropertiesViewModel(fixture.Session) { EditsEnabled = false };
        var window = new Window { Width = 500, Height = 3000, Content = new PropertyGridView { DataContext = properties } };
        var refused = new List<string>();
        void OnLogged(ILogMessage message)
        {
            if (message.Module == "PlayMode")
                refused.Add(message.Text);
        }
        GlobalLogger.GlobalMessageLogged += OnLogged;
        await window.ShowAsync();
        try
        {
            await properties.ApplyAsync(Snapshot());
            await WindowTestHelper.PumpAsync();
            (await ItemAsync(window, x => x.Name == "Components")).IsExpanded = true;
            foreach (var type in new[] { typeof(CameraComponent), typeof(LightComponent), typeof(ModelComponent), typeof(LiveTestScript) })
                (await ItemAsync(window, x => x.NodeValue?.GetType() == type)).IsExpanded = true;
            await WindowTestHelper.PumpAsync();

            var editors = window.GetVisualDescendants().OfType<Border>().Where(x => x.Name == "PART_Editor").ToList();
            Assert.NotEmpty(editors);
            // The visible ones (the buttons that add an item are there, hidden without the command)
            var controls = editors.SelectMany(x => x.GetVisualDescendants()).Where(x => x.IsEffectivelyVisible).ToList();
            Assert.Contains(controls, x => x is Slider);
            Assert.All(controls.OfType<TextBox>(), x => Assert.True(x.IsReadOnly || !x.IsEffectivelyEnabled, $"{x.Text} can be typed in"));
            Assert.All(controls.OfType<ToggleButton>(), x => Assert.False(x.IsEffectivelyEnabled, "a check box or a toggle is enabled"));
            Assert.All(controls.OfType<ComboBox>(), x => Assert.False(x.IsEffectivelyEnabled, $"the combo box of {(x.DataContext as NodeViewModel)?.DisplayPath ?? x.DataContext?.GetType().Name} ({x.Name}) is enabled"));
            Assert.All(controls.OfType<Slider>(), x => Assert.False(x.IsEffectivelyEnabled, "a slider is enabled"));
            // The component headers' check boxes too, and there is no Add component button
            Assert.All(window.GetVisualDescendants().OfType<CheckBox>().Where(x => x.IsEffectivelyVisible), x => Assert.False(x.IsEffectivelyEnabled, "a header check box is enabled"));
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<ToggleButton>(), x => x.IsVisible && x.IsEffectivelyEnabled && (x.Content as string)?.Contains("Add component") == true);
            // Known: the color editor writes its value back on display, rounded to a byte per channel (the hex text); the
            // live grid refuses it. Any other write from the view is a template that ignores IsReadOnly.
            Assert.All(refused, x => Assert.Contains("of Color.InlinedProperty was refused", x));
        }
        finally
        {
            GlobalLogger.GlobalMessageLogged -= OnLogged;
            await window.CloseAsync();
            properties.ViewModel?.Destroy();
        }
    }

    [AvaloniaFact]
    public async Task AChangeOfStructureRebuildsTheGrid()
    {
        using var fixture = await OpenAsync();
        var properties = new LivePropertiesViewModel(fixture.Session);
        try
        {
            await properties.ApplyAsync(Snapshot());
            player.Add(new LiveTestSecondScript());

            var applied = await properties.ApplyAsync(Snapshot());

            Assert.True(applied.Rebuilt);
            Assert.NotNull(applied.StructuralChange);
            Assert.Equal(2, properties.BuildCount);
            Assert.Contains(Components(properties.ViewModel!).Children, x => x.NodeValue is LiveTestSecondScript);

            // An entity that left the game: the grid says so
            var gone = await properties.ApplyAsync(new LiveSnapshot(++frame, frame / 60.0, []));
            Assert.True(gone.Rebuilt);
            Assert.Equal("The entity is no longer in the game.", properties.FallbackMessage);
        }
        finally
        {
            properties.ViewModel?.Destroy();
        }
    }

    [AvaloniaFact]
    public async Task AProtectedValueIsNotWrittenUntilItIsReleased()
    {
        using var fixture = await OpenAsync();
        var properties = new LivePropertiesViewModel(fixture.Session);
        try
        {
            await properties.ApplyAsync(Snapshot());
            var intensity = Node(properties.ViewModel!, typeof(LightComponent), nameof(LightComponent.Intensity));
            var accessor = intensity.NodePresenters.Single().GetNodeAccessor();
            IReadOnlyCollection<(IGraphNode, NodeIndex)> held = [(accessor.Node, accessor.Index)];
            properties.ProtectedValues = () => held;

            light.Intensity = 7;
            script.Speed = 8;
            var applied = await properties.ApplyAsync(Snapshot());

            // The pointer or the focus is on the intensity: the other values are written
            Assert.Equal(2f, intensity.NodeValue);
            Assert.Equal(8f, Node(properties.ViewModel!, typeof(LiveTestScript), nameof(LiveTestScript.Speed)).NodeValue);
            Assert.True(applied.Entities.Single().Result.SkippedValues > 0);

            held = [];
            await properties.ApplyAsync(Snapshot());
            Assert.Equal(7f, intensity.NodeValue);
        }
        finally
        {
            properties.ViewModel?.Destroy();
        }
    }

    [AvaloniaFact]
    public async Task AnEntitySlowToShowIsRefreshedLessOften()
    {
        using var fixture = await OpenAsync();
        // Any apply exceeds a budget of a tick
        var properties = new LivePropertiesViewModel(fixture.Session) { Throttle = new LiveRefreshThrottle { Budget = TimeSpan.FromTicks(1) } };
        try
        {
            await properties.ApplyAsync(Snapshot());
            // The first apply in place counts at most as the budget (it pays for the first layout); the second slows down
            script.Speed = 1.5f;
            var first = await properties.ApplyAsync(Snapshot());
            Assert.Single(first.Entities);
            Assert.False(properties.IsThrottled);
            script.Speed = 2;
            var second = await properties.ApplyAsync(Snapshot());
            Assert.Single(second.Entities);
            Assert.True(properties.IsThrottled);
            Assert.Equal(properties.Throttle.MaximumInterval, properties.Throttle.GetInterval(1));
            Assert.Contains("refreshed every 1 s", properties.LiveStatus);

            // Within the interval, the entity keeps its values
            script.Speed = 3;
            var held = await properties.ApplyAsync(Snapshot());
            Assert.Empty(held.Entities);
            Assert.Equal(2f, Node(properties.ViewModel!, typeof(LiveTestScript), nameof(LiveTestScript.Speed)).NodeValue);
        }
        finally
        {
            properties.ViewModel?.Destroy();
        }
    }

    [AvaloniaFact]
    public async Task TheGridShowsTheEntityAsTheSceneEditorDoes()
    {
        using var fixture = await OpenAsync();
        var properties = new LivePropertiesViewModel(fixture.Session);
        try
        {
            await properties.ApplyAsync(Snapshot());
            var components = Components(properties.ViewModel!);

            Assert.Equal(("Live entity", "Player"), (properties.TypeDescription, properties.Name));
            Assert.True(properties.IsLive);
            // The transform first, without its children; the components named by their display name
            Assert.IsType<TransformComponent>(components.Children.First().NodeValue);
            Assert.DoesNotContain(components.Children.First().Children, x => x.Name == nameof(TransformComponent.Children) && x.IsVisible);
            Assert.Equal("Light", components.Children.Single(x => x.NodeValue is LightComponent).DisplayName);
            // Another entity and its component: by name, without their members
            var targetNode = Node(properties.ViewModel!, typeof(LiveTestScript), nameof(LiveTestScript.Target));
            Assert.Equal((target.Id, "Target"), (((Entity)targetNode.NodeValue!).Id, ((Entity)targetNode.NodeValue!).Name));
            Assert.Empty(targetNode.Children);
            Assert.Empty(Node(properties.ViewModel!, typeof(LiveTestScript), nameof(LiveTestScript.TargetCamera)).Children);
        }
        finally
        {
            properties.ViewModel?.Destroy();
        }
    }

    [AvaloniaFact]
    public async Task ContentIsTheSessionsAssetOfItsUrl()
    {
        using var fixture = await OpenAsync();
        var asset = fixture.Asset(ModelLocation);
        model.Model = AttachedReferenceManager.CreateProxyObject<Model>(AssetId.Empty, asset.Url);
        var properties = new LivePropertiesViewModel(fixture.Session);
        try
        {
            await properties.ApplyAsync(Snapshot());
            var modelNode = Node(properties.ViewModel!, typeof(ModelComponent), nameof(ModelComponent.Model));
            var reference = AttachedReferenceManager.GetAttachedReference(modelNode.NodeValue);
            Assert.Equal((asset.Url, asset.Id), (reference?.Url, reference?.Id ?? default));
            Assert.Equal(asset.Url, new ContentReferenceToUrl().Convert(modelNode.NodeValue, typeof(string), null, CultureInfo.InvariantCulture));

            // Content the game created has no URL
            model.Model = new Model();
            await properties.ApplyAsync(Snapshot());
            modelNode = Node(properties.ViewModel!, typeof(ModelComponent), nameof(ModelComponent.Model));
            Assert.IsType<Model>(modelNode.NodeValue);
            Assert.Equal((null, AssetId.Empty), (AttachedReferenceManager.GetAttachedReference(modelNode.NodeValue)?.Url, AttachedReferenceManager.GetAttachedReference(modelNode.NodeValue)?.Id ?? default));
            Assert.Equal(ContentReferenceHelper.CreatedAtRunTime, new ContentReferenceToUrl().Convert(modelNode.NodeValue, typeof(string), null, CultureInfo.InvariantCulture));
        }
        finally
        {
            properties.ViewModel?.Destroy();
        }
    }

    [AvaloniaFact]
    public async Task AComponentOfAStaleTypeIsAPlaceholderNamedAfterItsType()
    {
        using var fixture = await OpenAsync();
        // The game's code is newer than the editor's
        var assemblies = new PlayAssemblyCheck();
        assemblies.Add([new PlayAssemblyInfo(typeof(LiveTestScript).Assembly.GetName().Name!, null, Guid.NewGuid())]);
        var properties = new LivePropertiesViewModel(fixture.Session);
        try
        {
            await properties.ApplyAsync(Snapshot(assemblies));
            var placeholder = Components(properties.ViewModel!).Children.Single(x => x.NodeValue is IUnloadable);
            Assert.Equal("Live Test Script", placeholder.DisplayName);
            Assert.True(placeholder.IsReadOnly);
        }
        finally
        {
            properties.ViewModel?.Destroy();
        }
    }

    [AvaloniaFact]
    public async Task WithoutAGameNothingIsWrittenThroughTheGrid()
    {
        using var fixture = await OpenAsync();
        var properties = new LivePropertiesViewModel(fixture.Session);
        try
        {
            // No play session (a tool drives the grid): the values can change, but nothing takes the edits
            await properties.ApplyAsync(Snapshot());
            var speed = Node(properties.ViewModel!, typeof(LiveTestScript), nameof(LiveTestScript.Speed));
            Assert.False(speed.IsReadOnly);

            speed.NodeValue = 1234f;
            speed.NodePresenters.Single().UpdateValue(1234f);
            Components(properties.ViewModel!).NodePresenters.Single().AddItem(new LiveTestSecondScript());

            Assert.Equal(script.Speed, speed.NodeValue);
            Assert.True(properties.RefusedWrites >= 2, $"{properties.RefusedWrites} writes refused");
            Assert.Equal(0, properties.SentEdits);
            Assert.DoesNotContain(Components(properties.ViewModel!).Children, x => x.NodeValue is LiveTestSecondScript);
            // The components cannot be added: only the members' commands
            Assert.True(Components(properties.ViewModel!).IsReadOnly);
            Assert.DoesNotContain(Components(properties.ViewModel!).NodePresenters.Single().Commands, x => x.Name == AddNewItemCommand.CommandName);
        }
        finally
        {
            properties.ViewModel?.Destroy();
        }
    }

    private static Task<ShellSessionFixture> OpenAsync() => ShellSessionFixture.OpenAsync((ModelLocation, new ModelAsset()));

    private LiveSnapshot Snapshot(PlayAssemblyCheck? assemblies = null)
    {
        ++frame;
        var snapshot = new PlaySnapshot(frame, frame / 60.0, [PlaySnapshotWriter.Write(player, x => handles.GetValueOrDefault(x))]);
        return new LiveMirrorReader(assemblies ?? new PlayAssemblyCheck()).Read(snapshot);
    }

    private static NodeViewModel Components(GraphViewModel viewModel) => viewModel.RootNode.Children.Single(x => x.Name == nameof(Entity.Components));

    private static NodeViewModel Node(GraphViewModel viewModel, Type componentType, params string[] path)
    {
        var node = Components(viewModel).Children.First(x => x.NodeValue?.GetType() == componentType);
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
/// A game's script: values, a list, references to another entity and to its component.
/// </summary>
[DataContract]
public sealed class LiveTestScript : SyncScript
{
    public float Speed { get; set; } = 1;

    public List<float> Values { get; } = [1, 2];

    public Entity? Target { get; set; }

    public CameraComponent? TargetCamera { get; set; }

    public override void Update()
    {
    }
}

/// <summary>
/// A script the game adds while it plays.
/// </summary>
[DataContract]
public sealed class LiveTestSecondScript : SyncScript
{
    public int Count { get; set; }

    public override void Update()
    {
    }
}
