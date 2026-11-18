// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core.Assets;
using Stride.Core.Serialization;
using Stride.Core.Yaml;
using Stride.Editor.PlayMode;
using Stride.Engine;
using Stride.PlayMode.Agent;
using Stride.PlayMode.Protocol;
using Stride.PlayMode.Snapshots;
using Stride.Rendering;

namespace Stride.Editor.Tests.PlayMode;

/// <summary>
/// Tests <see cref="LiveMirrorReader"/>: a snapshot the agent writes, read into a mirror entity with stand-ins and placeholders.
/// </summary>
public sealed class LiveMirrorReaderTests
{
    private readonly Entity player = new("Player");
    private readonly Entity enemy = new("Enemy");
    private readonly PlayTestComponent component;
    private readonly PlayTestSecondComponent second = new() { Speed = 4 };
    private readonly Dictionary<Entity, int> handles;

    public LiveMirrorReaderTests()
    {
        var enemyComponent = new PlayTestComponent { Health = 3 };
        enemy.Add(enemyComponent);
        component = new PlayTestComponent
        {
            Health = 10,
            Label = "Hero",
            Target = enemy,
            TargetTransform = enemy.Transform,
            Other = enemyComponent,
        };
        player.Add(component);
        player.Add(second);
        handles = new(ReferenceEqualityComparer.Instance) { [player] = 7, [enemy] = 8 };
    }

    [Fact]
    public void TheMirrorHasTheGameValues()
    {
        var mirror = new LiveMirrorReader(new PlayAssemblyCheck()).Read(Write(player));

        Assert.Equal((7, player.Id, "Player"), (mirror.Handle, mirror.Id, mirror.Name));
        Assert.Equal(player.Id, mirror.Entity.Id);
        Assert.Equal(player.Components.Select(x => x.Id), mirror.Entity.Components.Select(x => x.Id));
        Assert.All(mirror.Components, x => Assert.Equal(LiveComponentStatus.Read, x.Status));
        var read = mirror.Entity.Get<PlayTestComponent>();
        Assert.NotSame(component, read);
        Assert.Equal((10, "Hero"), (read.Health, read.Label));
        Assert.Equal(4, mirror.Entity.Get<PlayTestSecondComponent>().Speed);
        Assert.Same(mirror.Entity, read.Entity);
    }

    [Fact]
    public void ReferencesToOtherEntitiesAreStandIns()
    {
        var mirror = new LiveMirrorReader(new PlayAssemblyCheck()).Read(Write(player));
        var read = mirror.Entity.Get<PlayTestComponent>();

        var target = Assert.IsType<Entity>(read.Target);
        Assert.Equal((enemy.Id, "Enemy"), (target.Id, target.Name));
        Assert.Equal(8, mirror.StandIns[target].EntityHandle);
        Assert.Same(target.Transform, read.TargetTransform);
        Assert.Equal(enemy.Transform.Id, read.TargetTransform!.Id);
        Assert.Equal(PlayMirrorReferenceKind.Component, mirror.StandIns[read.TargetTransform].Kind);
        // A component of the right type, on the same stand-in entity, with no value of the game's
        var other = Assert.IsType<PlayTestComponent>(read.Other);
        Assert.Same(target, other.Entity);
        Assert.Equal(enemy.Get<PlayTestComponent>().Id, other.Id);
        Assert.Equal(0, other.Health);
        Assert.Equal(8, mirror.StandIns[other].EntityHandle);
        Assert.DoesNotContain(mirror.Entity, mirror.StandIns.Keys);
    }

    [Fact]
    public void ReferencesToTheMirrorsOwnComponentsAreTheMirrors()
    {
        component.Sibling = component;

        var mirror = new LiveMirrorReader(new PlayAssemblyCheck()).Read(Write(player));
        var read = mirror.Entity.Get<PlayTestComponent>();

        Assert.Same(read, read.Sibling);
    }

    [Fact]
    public void AStaleTypeIsAPlaceholder()
    {
        var assemblies = new PlayAssemblyCheck();
        assemblies.Add([new PlayAssemblyInfo(typeof(PlayTestComponent).Assembly.GetName().Name!, null, Guid.NewGuid())]);

        var mirror = new LiveMirrorReader(assemblies).Read(Write(player));

        var stale = mirror.Components.Where(x => x.TypeName.StartsWith(typeof(PlayTestComponent).Namespace!, StringComparison.Ordinal)).ToList();
        Assert.Equal(2, stale.Count);
        Assert.All(stale, x =>
        {
            Assert.Equal(LiveComponentStatus.StaleType, x.Status);
            Assert.Contains("reload", x.Error);
            var placeholder = Assert.IsAssignableFrom<IUnloadable>(x.Component);
            Assert.Equal(x.TypeName, $"{placeholder.TypeName}, {placeholder.AssemblyName}");
            Assert.Equal(x.Id, x.Component.Id);
            Assert.Same(x.Component, mirror.Entity.Components.Single(c => c.Id == x.Id));
        });
        // The transform, of the engine, is read
        Assert.Equal(LiveComponentStatus.Read, mirror.Components.Single(x => x.Component is TransformComponent).Status);
    }

    [Fact]
    public void AnUnknownTypeIsAPlaceholder()
    {
        var snapshot = Write(player);
        var unknown = snapshot.Components[1] with { TypeName = "MyGame.Unknown, MyGame" };
        var unknownInKnownAssembly = snapshot.Components[2] with { TypeName = $"Stride.Editor.Tests.Nothing, {typeof(PlayTestComponent).Assembly.GetName().Name}" };
        snapshot = snapshot with { Components = [snapshot.Components[0], unknown, unknownInKnownAssembly] };

        var mirror = new LiveMirrorReader(new PlayAssemblyCheck()).Read(snapshot);

        Assert.Equal(LiveComponentStatus.Read, mirror.Components[0].Status);
        Assert.Equal(LiveComponentStatus.UnknownType, mirror.Components[1].Status);
        Assert.Equal(LiveComponentStatus.UnknownType, mirror.Components[2].Status);
        Assert.Equal("MyGame.Unknown", Assert.IsAssignableFrom<IUnloadable>(mirror.Components[1].Component).TypeName);
        Assert.IsAssignableFrom<IUnloadable>(mirror.Components[2].Component);
    }

    [Fact]
    public void AComponentTheGameCouldNotWriteIsAPlaceholder()
    {
        var snapshot = Write(player);
        var notWritten = snapshot.Components[1] with { Format = PlaySnapshotFormat.None, Payload = ReadOnlyMemory<byte>.Empty, Error = "Boom." };
        snapshot = snapshot with { Components = [snapshot.Components[0], notWritten, snapshot.Components[2]] };

        var mirror = new LiveMirrorReader(new PlayAssemblyCheck()).Read(snapshot);

        Assert.Equal(LiveComponentStatus.NotWritten, mirror.Components[1].Status);
        Assert.Equal("Boom.", mirror.Components[1].Error);
        Assert.Equal("Boom.", Assert.IsAssignableFrom<IUnloadable>(mirror.Components[1].Component).Error);
        Assert.Equal(LiveComponentStatus.Read, mirror.Components[2].Status);
    }

    [Fact]
    public void APayloadThatDoesNotReadIsAPlaceholder()
    {
        var snapshot = Write(player);
        var broken = snapshot.Components[1] with { Payload = new byte[] { 1, 2, 3 } };
        snapshot = snapshot with { Components = [snapshot.Components[0], broken, snapshot.Components[2]] };

        var mirror = new LiveMirrorReader(new PlayAssemblyCheck()).Read(snapshot);

        Assert.Equal(LiveComponentStatus.ReadFailed, mirror.Components[1].Status);
        Assert.NotNull(mirror.Components[1].Error);
        Assert.IsAssignableFrom<IUnloadable>(mirror.Entity.Components[1]);
        Assert.Equal(LiveComponentStatus.Read, mirror.Components[2].Status);
    }

    [Fact]
    public void TheTransformKeepsNoChildren()
    {
        player.AddChild(new Entity("Child"));

        var mirror = new LiveMirrorReader(new PlayAssemblyCheck()).Read(Write(player));

        Assert.Empty(mirror.Entity.Transform.Children);
    }

    [Fact]
    public void ASnapshotReadsEachEntity()
    {
        var snapshot = new PlaySnapshot(3, 1.5, [Write(player), Write(enemy)]);

        var live = new LiveMirrorReader(new PlayAssemblyCheck()).Read(snapshot);

        Assert.Equal((3, 1.5), (live.Frame, live.GameTime));
        Assert.Equal(["Player", "Enemy"], live.Entities.Select(x => x.Name));
        Assert.Equal(3, live.Entities[1].Entity.Get<PlayTestComponent>().Health);
    }

    [Fact]
    public void ContentIsReadByUrlAndContentMadeAtRunTimeWithout()
    {
        var model = new ModelComponent { Model = AttachedReferenceManager.CreateProxyObject<Model>(AssetId.Empty, "Models/Box") };
        model.Materials[0] = new Material();
        player.Add(model);

        var mirror = new LiveMirrorReader(new PlayAssemblyCheck()).Read(Write(player));

        Assert.Equal(LiveComponentStatus.Read, mirror.Components.Single(x => x.Component is ModelComponent).Status);
        var read = mirror.Entity.Get<ModelComponent>();
        Assert.Equal("Models/Box", AttachedReferenceManager.GetUrl(read.Model));
        // The game made the material: a proxy with no URL and no id
        var material = Assert.IsType<Material>(read.Materials[0]);
        Assert.Equal((null, AssetId.Empty), (AttachedReferenceManager.GetUrl(material), AttachedReferenceManager.GetAttachedReference(material)!.Id));
    }

    private PlayEntitySnapshot Write(Entity entity) => PlaySnapshotWriter.Write(entity, x => handles.GetValueOrDefault(x));
}
