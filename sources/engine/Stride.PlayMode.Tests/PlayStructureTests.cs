// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.IO.Pipes;
using System.Threading.Channels;
using Stride.BepuPhysics;
using Stride.Core;
using Stride.Core.IO;
using Stride.Core.Mathematics;
using Stride.Core.Serialization.Contents;
using Stride.Core.Storage;
using Stride.Engine;
using Stride.Games;
using Stride.Physics;
using Stride.PlayMode.Agent;
using Stride.PlayMode.Protocol;
using Stride.PlayMode.Snapshots;

namespace Stride.PlayMode.Tests;

/// <summary>
/// Tests the structure commands of the play agent (<see cref="PlayStructureApplier"/>) and the moves of physics bodies
/// (<see cref="PlayPhysicsTeleport"/>) on a game that is not run: its scene instance holds real entities, and the agent's frames
/// run by hand. The physics bodies are stand-ins with the names of the engines' types.
/// </summary>
public sealed class PlayStructureTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly string folder = $"{nameof(PlayStructureTests)}/{Guid.NewGuid():N}";
    private readonly ObjectDatabase database;
    private readonly ContentManager content;
    private readonly PlayContentLoads loads;
    private readonly Game game = new();
    private readonly Scene scene = new() { Name = "Root" };
    private readonly Scene level = new() { Name = "Level" };
    private readonly SceneInstance instance;
    private readonly PlayTreeTracker tracker = new();
    private readonly PlayStructureApplier applier;
    private readonly Entity player = new("Player");
    private readonly Entity weapon = new("Weapon");
    private readonly Entity box = new("Box");
    private readonly EditableComponent editable = new();
    private readonly List<IDisposable> disposables = [];
    private int nextId;

    public PlayStructureTests()
    {
        database = new ObjectDatabase($"/tmp/{folder}", "index", loadDefaultBundle: false);
        var provider = new DatabaseFileProviderService(new DatabaseFileProvider(database));
        var writer = new ContentManager(provider);
        var prefabRoot = new Entity("Crate");
        prefabRoot.AddChild(new Entity("Lid"));
        writer.Save("Prefabs/Crate", new Prefab { Entities = { prefabRoot, new Entity("Pallet") } });
        content = new ContentManager(provider);
        loads = new PlayContentLoads(new PlayGameContent(() => content));

        player.Add(editable);
        player.AddChild(weapon);
        box.Transform.Position = new Vector3(5, 0, 0);
        scene.Entities.Add(player);
        scene.Entities.Add(box);
        level.Offset = new Vector3(0, 10, 0);
        scene.Children.Add(level);
        instance = new SceneInstance(game.Services, scene);
        tracker.Update(instance);
        applier = new PlayStructureApplier(tracker, loads);
    }

    public void Dispose()
    {
        foreach (var disposable in disposables)
            disposable.Dispose();
        loads.ReleaseAll();
        tracker.Dispose();
        instance.Dispose();
        game.Dispose();
        database.Dispose();
        var path = Path.Combine(PlatformFolders.ApplicationTemporaryDirectory, folder);
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
    }

    [Fact]
    public void AComponentIsAddedByTypeNameAndItsIdAnswered()
    {
        var result = Apply(Command(PlayStructureOperation.AddComponent, player, typeName: TypeName<RepeatableComponent>()));

        Assert.Equal(PlayEditStatus.Applied, result.Status);
        var added = Assert.IsType<RepeatableComponent>(player.Components[^1]);
        Assert.Equal(added.Id, result.ComponentId);
        Assert.Empty(result.Entities);
    }

    [Fact]
    public void AComponentIsInsertedAtItsIndex()
    {
        var result = Apply(Command(PlayStructureOperation.AddComponent, player, typeName: TypeName<RepeatableComponent>()) with { Index = 1 });

        Assert.Equal(PlayEditStatus.Applied, result.Status);
        Assert.IsType<RepeatableComponent>(player.Components[1]);
        Assert.Same(editable, player.Components[2]);
    }

    [Fact]
    public void AComponentAddedTwiceIsRefusedUnlessTheTypeAllowsIt()
    {
        var once = Apply(Command(PlayStructureOperation.AddComponent, player, typeName: TypeName<EditableComponent>()));
        var first = Apply(Command(PlayStructureOperation.AddComponent, player, typeName: TypeName<RepeatableComponent>()));
        var second = Apply(Command(PlayStructureOperation.AddComponent, player, typeName: TypeName<RepeatableComponent>()));

        Assert.Equal(PlayEditStatus.Failed, once.Status);
        Assert.Contains("Player already has a EditableComponent, and an entity can have only one", once.Error);
        Assert.Equal((PlayEditStatus.Applied, PlayEditStatus.Applied), (first.Status, second.Status));
        Assert.Equal(2, player.Components.OfType<RepeatableComponent>().Count());
    }

    [Theory]
    [InlineData("Stride.PlayMode.Tests.MissingComponent, Stride.PlayMode.Tests", "MissingComponent")]
    [InlineData("Stride.PlayMode.Tests.AbstractTestComponent, Stride.PlayMode.Tests", "abstract")]
    [InlineData("Stride.Engine.TransformComponent, Stride.Engine", "already has its transform")]
    [InlineData("Stride.PlayMode.Tests.TestContent, Stride.PlayMode.Tests", "is not a component")]
    public void AComponentTheGameCannotMakeIsRefused(string typeName, string reason)
    {
        var count = player.Components.Count;

        var result = Apply(Command(PlayStructureOperation.AddComponent, player, typeName: typeName));

        Assert.Equal(PlayEditStatus.Failed, result.Status);
        Assert.Contains(reason, result.Error);
        Assert.Equal(count, player.Components.Count);
    }

    [Fact]
    public void ABulletBodyIsNotAddedWhileTheGameRuns()
    {
        var count = player.Components.Count;

        var result = Apply(Command(PlayStructureOperation.AddComponent, player, typeName: TypeName<RigidbodyComponent>()));

        Assert.Equal(PlayEditStatus.Failed, result.Status);
        Assert.Contains("A Bullet physics component added while the game runs has no collider shape, and its shapes cannot change until the game restarts: add the RigidbodyComponent in the scene editor.", result.Error);
        Assert.Equal(count, player.Components.Count);
    }

    [Fact]
    public void AComponentIsRemovedButNotTheTransform()
    {
        var removed = Apply(Command(PlayStructureOperation.RemoveComponent, player) with { ComponentId = editable.Id });
        var transform = Apply(Command(PlayStructureOperation.RemoveComponent, player) with { ComponentId = player.Transform.Id });
        var again = Apply(Command(PlayStructureOperation.RemoveComponent, player) with { ComponentId = editable.Id });

        Assert.Equal(PlayEditStatus.Applied, removed.Status);
        Assert.DoesNotContain(editable, player.Components);
        Assert.Equal(PlayEditStatus.Failed, transform.Status);
        Assert.Contains("every entity has one", transform.Error);
        Assert.Equal(PlayEditStatus.Gone, again.Status);
    }

    [Fact]
    public void AComponentAnotherRequiresIsNotRemoved()
    {
        player.Add(new NeedsEditableComponent());

        var result = Apply(Command(PlayStructureOperation.RemoveComponent, player) with { ComponentId = editable.Id });

        Assert.Equal(PlayEditStatus.Failed, result.Status);
        Assert.Contains("NeedsEditableComponent of Player requires a EditableComponent: remove it first", result.Error);
        Assert.Contains(editable, player.Components);
    }

    [Fact]
    public void AnEntityIsCreatedAtTheRootOfTheRootSceneByDefault()
    {
        var result = Apply(new PlayStructureEdit(++nextId, PlayStructureOperation.CreateEntity));

        Assert.Equal(PlayEditStatus.Applied, result.Status);
        var created = tracker.Entities[Assert.Single(result.Entities)];
        Assert.Equal(PlayStructureApplier.DefaultEntityName, created.Name);
        Assert.Same(scene, created.Scene);
        Assert.Same(created, scene.Entities[^1]);
    }

    [Fact]
    public void AnEntityIsCreatedUnderAnEntityOrInAChildScene()
    {
        var under = Apply(new PlayStructureEdit(++nextId, PlayStructureOperation.CreateEntity, ParentHandle: Handle(player), Name: "Shield"));
        var inLevel = Apply(new PlayStructureEdit(++nextId, PlayStructureOperation.CreateEntity, SceneHandle: SceneHandle(level), Name: "Spawn point"));

        var shield = tracker.Entities[Assert.Single(under.Entities)];
        Assert.Equal("Shield", shield.Name);
        Assert.Same(player.Transform, shield.Transform.Parent);
        var spawnPoint = tracker.Entities[Assert.Single(inLevel.Entities)];
        Assert.Same(level, spawnPoint.Scene);
    }

    [Fact]
    public void AnEntityIsDeletedWithItsChildren()
    {
        var result = Apply(Command(PlayStructureOperation.DeleteEntity, player));
        var again = Apply(Command(PlayStructureOperation.DeleteEntity, player) with { EntityHandle = Handle(player) });

        Assert.Equal(PlayEditStatus.Applied, result.Status);
        Assert.DoesNotContain(player, scene.Entities);
        Assert.Null(player.EntityManager);
        Assert.Null(weapon.EntityManager);
        Assert.Equal(PlayEditStatus.Gone, again.Status);
    }

    [Fact]
    public void AChildIsDeletedFromItsParent()
    {
        var result = Apply(Command(PlayStructureOperation.DeleteEntity, weapon));

        Assert.Equal(PlayEditStatus.Applied, result.Status);
        Assert.Empty(player.Transform.Children);
        Assert.Contains(player, scene.Entities);
    }

    [Fact]
    public void ADuplicateComesRightAfterItsOriginalWithItsChildren()
    {
        var result = Apply(Command(PlayStructureOperation.DuplicateEntity, player));

        Assert.Equal(PlayEditStatus.Applied, result.Status);
        Assert.Equal(2, result.Entities.Count);
        var copy = tracker.Entities[result.Entities[0]];
        Assert.NotSame(player, copy);
        Assert.Equal("Player", copy.Name);
        Assert.Same(copy, scene.Entities[scene.Entities.IndexOf(player) + 1]);
        var copiedWeapon = tracker.Entities[result.Entities[1]];
        Assert.Same(copy.Transform, copiedWeapon.Transform.Parent);
        Assert.NotNull(copy.Get<EditableComponent>());
    }

    [Fact]
    public void AChildIsDuplicatedUnderTheSameParent()
    {
        var result = Apply(Command(PlayStructureOperation.DuplicateEntity, weapon));

        var copy = tracker.Entities[Assert.Single(result.Entities)];
        Assert.Equal([weapon.Transform, copy.Transform], player.Transform.Children);
    }

    [Fact]
    public void AMoveUnderAnEntityKeepsTheLocalTransformAndTheHandle()
    {
        var handle = Handle(box);

        var result = Apply(Move(box, parent: player));

        Assert.Equal(PlayEditStatus.Applied, result.Status);
        Assert.Same(player.Transform, box.Transform.Parent);
        Assert.Equal(new Vector3(5, 0, 0), box.Transform.Position);
        Assert.Equal(handle, Handle(box));
        Assert.Same(player.Transform.Children[^1], box.Transform);
    }

    [Fact]
    public void AMoveToAnotherSceneCanKeepThePlaceInTheWorld()
    {
        var result = Apply(Move(box, scene: level, keepWorld: true));

        Assert.Equal(PlayEditStatus.Applied, result.Status);
        Assert.Same(level, box.Scene);
        // The level is 10 up: the box goes 10 down in it, and stays where it was
        AssertNear(new Vector3(5, -10, 0), box.Transform.Position);
        box.Transform.UpdateWorldMatrix();
        AssertNear(new Vector3(5, 0, 0), box.Transform.WorldMatrix.TranslationVector);
    }

    [Fact]
    public void AMoveUnderItselfOrItsChildIsRefused()
    {
        var underChild = Apply(Move(player, parent: weapon));
        var underItself = Apply(Move(player, parent: player));

        Assert.Equal(PlayEditStatus.Failed, underChild.Status);
        Assert.Contains("Player cannot go under itself or one of its children", underChild.Error);
        Assert.Equal(PlayEditStatus.Failed, underItself.Status);
        Assert.Contains(player, scene.Entities);
    }

    [Fact]
    public void AChildMovedToTheRootOfItsSceneLeavesItsParent()
    {
        var result = Apply(Move(weapon, scene: scene));

        Assert.Equal(PlayEditStatus.Applied, result.Status);
        Assert.Null(weapon.Transform.Parent);
        Assert.Same(scene, weapon.Scene);
        Assert.NotEqual(0, Handle(weapon));
    }

    [Fact]
    public void APrefabOfTheBuildIsSpawnedWithItsRootsAndChildren()
    {
        var result = Apply(new PlayStructureEdit(++nextId, PlayStructureOperation.SpawnPrefab, SceneHandle: SceneHandle(level), Url: "Prefabs/Crate"));

        Assert.Equal(PlayEditStatus.Applied, result.Status);
        Assert.Equal(["Crate", "Lid", "Pallet"], result.Entities.Select(x => tracker.Entities[x].Name));
        Assert.Equal(["Crate", "Pallet"], level.Entities.Select(x => x.Name));
        // Its content stays loaded until the game exits
        Assert.Equal(1, loads.Count);
        loads.ReleaseAll();
        Assert.Equal(0, loads.Count);
    }

    [Fact]
    public void APrefabIsSpawnedUnderAnEntity()
    {
        var result = Apply(new PlayStructureEdit(++nextId, PlayStructureOperation.SpawnPrefab, ParentHandle: Handle(player), Url: "Prefabs/Crate"));

        Assert.Equal(PlayEditStatus.Applied, result.Status);
        Assert.Equal(["Weapon", "Crate", "Pallet"], player.Transform.Children.Select(x => x.Entity.Name));
    }

    [Fact]
    public void APrefabNotInTheBuildIsRefusedAndNothingStaysLoaded()
    {
        var result = Apply(new PlayStructureEdit(++nextId, PlayStructureOperation.SpawnPrefab, Url: "Prefabs/Missing"));

        Assert.Equal(PlayEditStatus.Failed, result.Status);
        Assert.Contains("Prefabs/Missing", result.Error);
        Assert.Equal(0, loads.Count);
        Assert.Equal(2, scene.Entities.Count);
    }

    [Fact]
    public void APrefabIsNotLoadedWhenItsParentIsGone()
    {
        var gone = Handle(weapon);
        weapon.Transform.Parent = null;

        var result = Apply(new PlayStructureEdit(++nextId, PlayStructureOperation.SpawnPrefab, ParentHandle: gone, Url: "Prefabs/Crate"));

        Assert.Equal(PlayEditStatus.Gone, result.Status);
        Assert.Equal(0, loads.Count);
        Assert.False(content.IsLoaded("Prefabs/Crate"));
    }

    [Fact]
    public void AGameWithoutContentLoadsRefusesSpawns()
    {
        var withoutContent = new PlayStructureApplier(tracker);

        var result = withoutContent.Apply(new PlayStructureEdit(++nextId, PlayStructureOperation.SpawnPrefab, Url: "Prefabs/Crate"), 1);

        Assert.Equal(PlayEditStatus.Failed, result.Status);
        Assert.Contains("does not load content", result.Error);
    }

    [Fact]
    public void ACommandOnAnEntityThatLeftIsGone()
    {
        var handle = Handle(box);
        scene.Entities.Remove(box);

        var results = new[]
        {
            Apply(new PlayStructureEdit(++nextId, PlayStructureOperation.AddComponent, EntityHandle: handle, TypeName: TypeName<RepeatableComponent>())),
            Apply(new PlayStructureEdit(++nextId, PlayStructureOperation.DuplicateEntity, EntityHandle: handle)),
            Apply(new PlayStructureEdit(++nextId, PlayStructureOperation.MoveEntity, EntityHandle: handle, ParentHandle: Handle(player))),
            Apply(new PlayStructureEdit(++nextId, PlayStructureOperation.CreateEntity, ParentHandle: handle)),
        };

        Assert.All(results, x => Assert.Equal(PlayEditStatus.Gone, x.Status));
    }

    [Fact]
    public void ABepuBodyIsTeleportedWhereItsEntityMoves()
    {
        var body = new BodyComponent { CenterOfMass = new Vector3(0, 1, 0) };
        box.Add(body);

        Apply(Move(box, scene: level));

        var (position, orientation) = Assert.Single(body.Teleports);
        // The level is 10 up, and the center of mass 1 up from the entity
        AssertNear(new Vector3(5, 11, 0), position);
        Assert.Equal(Quaternion.Identity, orientation);
    }

    [Fact]
    public void ABulletRigidBodyIsMovedAndWokenUpWithItsInterpolation()
    {
        var body = new RigidbodyComponent { NativeCollisionObject = new FakeCollisionObject() };
        var child = new StaticColliderComponent { NativeCollisionObject = new FakeCollisionObject() };
        box.Add(body);
        weapon.Add(child);

        Apply(Move(box, parent: player));

        Assert.Equal(1, body.Updates);
        Assert.True(body.Activated);
        Assert.Equal(body.NativeCollisionObject.WorldTransform, body.NativeCollisionObject.InterpolationWorldTransform);
        // The bodies under the moved entity are not moved: only the moved entity and its descendants
        Assert.Equal(0, child.Updates);
        Assert.Equal("Bullet rigid body", PlayPhysicsTeleport.DescribeBody(body));
        Assert.Equal("Bullet collider", PlayPhysicsTeleport.DescribeBody(child));
        Assert.Equal("Bepu body", PlayPhysicsTeleport.DescribeBody(new BodyComponent()));
        Assert.Null(PlayPhysicsTeleport.DescribeBody(editable));
    }

    [Fact]
    public void TheDescendantsBodiesMoveWithTheEntity()
    {
        var child = new StaticColliderComponent { NativeCollisionObject = new FakeCollisionObject() };
        weapon.Add(child);

        var moved = PlayPhysicsTeleport.Teleport(player);

        Assert.Equal(1, moved);
        Assert.Equal(1, child.Updates);
    }

    [Fact]
    public void ABulletCharacterGetsTheInverseTurnOfItsOrientation()
    {
        var turn = Quaternion.RotationY(MathUtil.PiOverTwo);
        var character = new CharacterComponent { Orientation = turn, NativeCollisionObject = new FakeCollisionObject() };
        box.Add(character);

        PlayPhysicsTeleport.Teleport(box);

        // The engine draws the entity at the body turned by the orientation: the entity ends where it was put
        var drawn = Matrix.RotationQuaternion(turn) * character.PhysicsWorldTransform;
        AssertNear(box.Transform.WorldMatrix.TranslationVector, drawn.TranslationVector);
        Assert.False(character.Activated);
    }

    [Fact]
    public void ABodyThatFollowsABoneOrIsNotInASimulationIsLeftAlone()
    {
        var bone = new RigidbodyComponent { BoneIndex = 2, NativeCollisionObject = new FakeCollisionObject() };
        var outside = new RigidbodyComponent();
        box.Add(bone);
        weapon.Add(outside);

        Assert.Equal(0, PlayPhysicsTeleport.Teleport(box) + PlayPhysicsTeleport.Teleport(weapon));
        Assert.Equal(0, bone.Updates + outside.Updates);
    }

    [Theory]
    [InlineData("Position", true)]
    [InlineData("Rotation", true)]
    [InlineData("Scale", true)]
    [InlineData("WorldMatrix", true)]
    [InlineData("Children", false)]
    public void AnEditOfATransformMovesTheBodies(string member, bool moves)
        => Assert.Equal(moves, PlayPhysicsTeleport.MovesEntity(member));

    [Fact]
    public void AnEditedPositionTeleportsTheBody()
    {
        var body = new BodyComponent();
        box.Add(body);
        var edits = new PlayEditApplier();
        var edit = new PlayEdit(++nextId, Handle(box), box.Transform.Id, [PlayPathSegment.Member("Position")], PlayEditOperation.Set,
            PlayEditValue.Of(PlayMirrorSerializer.WriteValue(new Vector3(1, 2, 3), _ => 0)));

        var result = edits.Apply([(0, edit)], 1, x => tracker.Entities.GetValueOrDefault(x), tracker.GetHandle)[0].Result;

        Assert.Equal(PlayEditStatus.Applied, result.Status);
        AssertNear(new Vector3(1, 2, 3), Assert.Single(body.Teleports).Position);
    }

    [Fact]
    public async Task TheAgentAppliesCommandsWithTheEditsInOrderAndAnswersBeforeTheTreeBatch()
    {
        var agent = new PlayAgentSystem(game);
        disposables.Add(agent);
        game.SceneSystem.SceneInstance = instance;
        var editor = await ConnectAsync(agent);
        // The tree comes first, at the end of a frame
        await editor.ReadAsync<PlayTree>(agent, runFrames: true);
        var handle = agent.Tracker.GetHandle(player);

        // An edit, then a duplicate: the copy has the edited value
        editor.Send(new PlayEdit(++nextId, handle, editable.Id, [PlayPathSegment.Member("Speed")], PlayEditOperation.Set, PlayEditValue.Of(PlayMirrorSerializer.WriteValue(9f, _ => 0))));
        editor.Send(new PlayStructureEdit(++nextId, PlayStructureOperation.DuplicateEntity, EntityHandle: handle));
        await WaitAsync(() => agent.PendingEdits == 2);
        agent.EditSystem.Update(new GameTime());
        agent.Update(new GameTime());

        var edit = await editor.ReadAsync<PlayEditResult>(agent);
        var duplicate = await editor.ReadAsync<PlayStructureResult>(agent);
        var batch = await editor.ReadAsync<PlayTreeBatch>(agent);
        Assert.Equal(PlayEditStatus.Applied, edit.Status);
        Assert.Equal(PlayEditStatus.Applied, duplicate.Status);
        Assert.Equal(duplicate.Frame, batch.Frame);
        var copy = agent.Tracker.Entities[duplicate.Entities[0]];
        Assert.Equal(9f, copy.Get<EditableComponent>()!.Speed);
        Assert.Contains(batch.Operations, x => x is PlayEntityAdded added && added.Entity.Handle == duplicate.Entities[0]);
    }

    private PlayStructureResult Apply(PlayStructureEdit edit) => applier.Apply(edit, 1);

    private PlayStructureEdit Command(PlayStructureOperation operation, Entity entity, string? typeName = null)
        => new(++nextId, operation, EntityHandle: Handle(entity), TypeName: typeName);

    private PlayStructureEdit Move(Entity entity, Entity? parent = null, Scene? scene = null, bool keepWorld = false)
        => new(++nextId, PlayStructureOperation.MoveEntity, EntityHandle: Handle(entity), ParentHandle: parent is null ? 0 : Handle(parent),
            SceneHandle: scene is null ? 0 : SceneHandle(scene), KeepWorldTransform: keepWorld);

    private int Handle(Entity entity) => tracker.GetHandle(entity);

    private int SceneHandle(Scene target) => tracker.Scenes.First(x => x.Value == target).Key;

    private static string TypeName<T>() => $"{typeof(T).FullName}, {typeof(T).Assembly.GetName().Name}";

    private static void AssertNear(Vector3 expected, Vector3 actual)
        => Assert.True((expected - actual).Length() < 1e-4f, $"Expected {expected}, got {actual}.");

    private static async Task WaitAsync(Func<bool> condition)
    {
        var start = DateTime.UtcNow;
        while (!condition())
        {
            if (DateTime.UtcNow - start > Timeout)
                throw new TimeoutException("The agent did not receive the commands.");
            await Task.Delay(5);
        }
    }

    private async Task<EditorEnd> ConnectAsync(PlayAgentSystem agent)
    {
        var name = $"stride-play-structure-test-{Guid.NewGuid():N}";
        var server = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        var client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
        await Task.WhenAll(server.WaitForConnectionAsync(), client.ConnectAsync()).WaitAsync(Timeout);
        var editor = new EditorEnd(server);
        disposables.Add(editor);
        agent.AddSubscriber(client);
        return editor;
    }

    /// <summary>
    /// The editor's end of a connection.
    /// </summary>
    private sealed class EditorEnd : IDisposable
    {
        private readonly PlayConnection connection;
        private readonly Channel<PlayMessage> received = Channel.CreateUnbounded<PlayMessage>();

        public EditorEnd(Stream stream)
        {
            connection = new PlayConnection(stream);
            connection.Start(x => received.Writer.TryWrite(x));
        }

        public void Send(PlayMessage message) => Assert.True(connection.Send(message));

        /// <summary>
        /// The next message of a type, the agent's frames running meanwhile.
        /// </summary>
        public async Task<T> ReadAsync<T>(PlayAgentSystem agent, bool runFrames = false)
            where T : PlayMessage
        {
            var start = DateTime.UtcNow;
            while (true)
            {
                while (received.Reader.TryRead(out var message))
                {
                    if (message is T typed)
                        return typed;
                }
                if (DateTime.UtcNow - start > Timeout)
                    throw new TimeoutException($"The agent sent no {typeof(T).Name}.");
                if (runFrames)
                    agent.Update(new GameTime());
                await Task.Delay(10);
            }
        }

        public void Dispose() => connection.DisposeAsync().AsTask().Wait(Timeout);
    }
}
