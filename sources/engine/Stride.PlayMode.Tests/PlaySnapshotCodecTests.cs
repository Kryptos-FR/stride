// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core.Mathematics;
using Stride.Core.Serialization;
using Stride.Engine;
using Stride.PlayMode.Agent;
using Stride.PlayMode.Protocol;
using Stride.PlayMode.Snapshots;
using Stride.Rendering;

namespace Stride.PlayMode.Tests;

/// <summary>
/// Tests the snapshot codec (<see cref="PlayMirrorSerializer"/>, <see cref="PlaySnapshotWriter"/>): a component written at
/// its entity's boundary and read back into another instance, with what its references stand for.
/// </summary>
public sealed class PlaySnapshotCodecTests
{
    private readonly Entity player = new("Player");
    private readonly Entity enemy = new("Enemy");
    private readonly TestComponent component;
    private readonly ModelComponent sibling = new();
    private readonly Dictionary<Entity, int> handles;

    public PlaySnapshotCodecTests()
    {
        var model = new Model();
        AttachedReferenceManager.SetUrl(model, "Models/Cube");
        component = new TestComponent
        {
            Speed = 2.5f,
            Offset = new Vector3(1, 2, 3),
            Label = "Hero",
            Values = [1, 2, 3],
            Target = enemy,
            TargetTransform = enemy.Transform,
            Sibling = sibling,
            Model = model,
        };
        player.Add(component);
        player.Add(sibling);
        handles = new(ReferenceEqualityComparer.Instance) { [player] = 1, [enemy] = 2 };
    }

    [Fact]
    public void AComponentReadsBackWithItsValues()
    {
        var payload = PlayMirrorSerializer.Write(component, HandleOf);
        var mirror = ReadIntoMirror(payload, out _);
        var read = mirror.Get<TestComponent>();

        Assert.Equal(2.5f, read.Speed);
        Assert.Equal(new Vector3(1, 2, 3), read.Offset);
        Assert.Equal("Hero", read.Label);
        Assert.Equal([1, 2, 3], read.Values);
    }

    [Fact]
    public void ReferencesAreCutAtTheEntity()
    {
        var payload = PlayMirrorSerializer.Write(component, HandleOf);

        var references = PlayMirrorSerializer.ReadReferences(payload);

        // The component's own entity, the other entity, the other entity's transform and the sibling: never their content
        Assert.Contains(references, x => x is { Kind: PlayMirrorReferenceKind.Entity, EntityHandle: 1, EntityName: "Player" });
        Assert.Contains(references, x => x is { Kind: PlayMirrorReferenceKind.Entity, EntityHandle: 2, EntityName: "Enemy" } && x.EntityId == enemy.Id);
        Assert.Contains(references, x => x.Kind == PlayMirrorReferenceKind.Component && x.EntityHandle == 2 && x.ComponentId == enemy.Transform.Id
            && x.ComponentTypeName == "Stride.Engine.TransformComponent, Stride.Engine");
        Assert.Contains(references, x => x.Kind == PlayMirrorReferenceKind.Component && x.EntityHandle == 1 && x.ComponentId == sibling.Id);
        Assert.True(payload.Length < 4096, $"The payload takes {payload.Length} bytes: an entity's content was written.");
    }

    [Fact]
    public void ReferencesReadAsWhatTheyStandFor()
    {
        var payload = PlayMirrorSerializer.Write(component, HandleOf);
        var mirror = ReadIntoMirror(payload, out var standIns);
        var read = mirror.Get<TestComponent>();

        Assert.Same(mirror, read.Entity);
        Assert.Same(mirror.Get<ModelComponent>(), read.Sibling);
        var target = Assert.IsType<Entity>(read.Target);
        Assert.Same(standIns[(2, Guid.Empty)], target);
        Assert.Equal("Enemy", target.Name);
        Assert.Same(standIns[(2, enemy.Transform.Id)], read.TargetTransform);
        Assert.Equal(enemy.Transform.Id, read.TargetTransform!.Id);
    }

    [Fact]
    public void ContentReadsAsAProxyWithItsUrl()
    {
        var payload = PlayMirrorSerializer.Write(component, HandleOf);
        var read = ReadIntoMirror(payload, out _).Get<TestComponent>();

        Assert.NotNull(read.Model);
        Assert.NotSame(component.Model, read.Model);
        Assert.Equal("Models/Cube", AttachedReferenceManager.GetUrl(read.Model));
    }

    [Fact]
    public void AnEntityIsWrittenComponentByComponent()
    {
        player.Add(new ThrowingComponent());

        var snapshot = PlaySnapshotWriter.Write(player, HandleOf);

        Assert.Equal((1, player.Id, "Player"), (snapshot.Handle, snapshot.Id, snapshot.Name));
        Assert.Equal(player.Components.Select(x => x.Id), snapshot.Components.Select(x => x.Id));
        var failed = snapshot.Components.Single(x => x.TypeName.StartsWith(typeof(ThrowingComponent).FullName!, StringComparison.Ordinal));
        Assert.Equal(PlaySnapshotFormat.None, failed.Format);
        Assert.Contains("Boom.", failed.Error);
        Assert.True(failed.Payload.IsEmpty);
        // The others are written
        Assert.All(snapshot.Components.Where(x => x != failed), x => Assert.Equal(PlaySnapshotFormat.Binary, x.Format));
    }

    [Fact]
    public void ACyclicReferenceInsideTheComponentReadsBack()
    {
        // The component refers to itself through its sibling field
        component.Sibling = component;

        var read = ReadIntoMirror(PlayMirrorSerializer.Write(component, HandleOf), out _).Get<TestComponent>();

        Assert.Same(read, read.Sibling);
    }

    private int HandleOf(Entity entity) => handles.GetValueOrDefault(entity);

    /// <summary>
    /// What the editor's reader does: a mirror entity with an instance of each component, then the payload read into its
    /// instance, with stand-ins for the other entities.
    /// </summary>
    private Entity ReadIntoMirror(byte[] payload, out Dictionary<(int Handle, Guid Id), object> standIns)
    {
        var mirror = new Entity(player.Name) { Id = player.Id };
        mirror.Components.Clear();
        var target = new TestComponent { Id = component.Id };
        mirror.Add(target);
        mirror.Add(new ModelComponent { Id = sibling.Id });

        var made = new Dictionary<(int Handle, Guid Id), object>();
        PlayMirrorSerializer.Read(payload, target, reference =>
        {
            var own = reference.EntityHandle == 1;
            switch (reference.Kind)
            {
                case PlayMirrorReferenceKind.Entity:
                    return own ? mirror : Make((reference.EntityHandle, Guid.Empty), () => new Entity(reference.EntityName) { Id = reference.EntityId });
                case PlayMirrorReferenceKind.Component when own:
                    return mirror.Components.FirstOrDefault(x => x.Id == reference.ComponentId);
                case PlayMirrorReferenceKind.Component:
                    var owner = (Entity)Make((reference.EntityHandle, Guid.Empty), () => new Entity(reference.EntityName) { Id = reference.EntityId });
                    return Make((reference.EntityHandle, reference.ComponentId), () =>
                    {
                        owner.Transform.Id = reference.ComponentId;
                        return owner.Transform;
                    });
                default:
                    return null;
            }
        });
        standIns = made;
        return mirror;

        object Make((int, Guid) key, Func<object> create)
        {
            if (!made.TryGetValue(key, out var value))
                made[key] = value = create();
            return value;
        }
    }
}
