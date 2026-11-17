// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Engine;
using Stride.PlayMode.Agent;
using Stride.PlayMode.Protocol;

namespace Stride.PlayMode.Tests;

/// <summary>
/// Tests <see cref="PlayTreeTracker"/> on a scene instance with no graphics.
/// </summary>
public sealed class PlayTreeTrackerTests : IDisposable
{
    private readonly Game game = new();
    private readonly Scene root = new() { Name = "Root" };
    private readonly SceneInstance instance;
    private readonly PlayTreeTracker tracker = new();

    public PlayTreeTrackerTests()
    {
        root.Entities.Add(new Entity("First"));
        instance = new SceneInstance(game.Services, root);
    }

    public void Dispose()
    {
        tracker.Dispose();
        instance.Dispose();
        game.Dispose();
    }

    [Fact]
    public void TheTreeHasTheScenesAndEntities()
    {
        var child = new Scene { Name = "Child" };
        var parent = new Entity("Parent");
        var nested = new Entity("Nested");
        parent.AddChild(nested);
        child.Entities.Add(parent);
        root.Children.Add(child);

        Assert.True(tracker.Update(instance));
        var tree = tracker.BuildTree(1);

        Assert.Equal(["Root", "Child"], tree.Scenes.Select(x => x.Name));
        Assert.Equal(tree.Scenes[0].Handle, tree.Scenes[1].ParentHandle);
        Assert.Equal(["First", "Parent", "Nested"], tree.Entities.Select(x => x.Name));
        var parentInfo = tree.Entities[1];
        var nestedInfo = tree.Entities[2];
        Assert.Equal(parentInfo.Handle, nestedInfo.ParentHandle);
        Assert.Equal(tree.Scenes[1].Handle, nestedInfo.SceneHandle);
        Assert.Contains(nestedInfo.Components, x => x.TypeName == "Stride.Engine.TransformComponent, Stride.Engine");
        Assert.All(tree.Entities, x => Assert.Equal(x.Handle, tracker.GetHandle(tracker.Entities[x.Handle])));
        // Nothing changed since the tree
        Assert.Null(tracker.TakeBatch(1));
        Assert.False(tracker.Update(instance));
    }

    [Fact]
    public void AnAddedEntityIsSentWithItsChildren()
    {
        tracker.Update(instance);
        var parent = new Entity("Parent");
        parent.AddChild(new Entity("Child"));

        root.Entities.Add(parent);
        var batch = tracker.TakeBatch(2);

        Assert.NotNull(batch);
        Assert.Equal(2, batch.Frame);
        var added = batch.Operations.Cast<PlayEntityAdded>().Select(x => x.Entity).ToList();
        // Parents first, although the engine adds the children first
        Assert.Equal(["Parent", "Child"], added.Select(x => x.Name));
        Assert.Equal(added.Single(x => x.Name == "Parent").Handle, added.Single(x => x.Name == "Child").ParentHandle);
        Assert.Null(tracker.TakeBatch(3));
    }

    [Fact]
    public void ARemovedEntityIsSentByHandle()
    {
        tracker.Update(instance);
        var first = root.Entities[0];
        var handle = tracker.GetHandle(first);

        root.Entities.Remove(first);
        var batch = tracker.TakeBatch(2);

        Assert.Equal(handle, Assert.IsType<PlayEntityRemoved>(Assert.Single(batch!.Operations)).Handle);
        Assert.Equal(0, tracker.GetHandle(first));
        Assert.False(tracker.Entities.ContainsKey(handle));
    }

    [Fact]
    public void AReparentedEntityIsSentAsAMove()
    {
        var parent = new Entity("Parent");
        root.Entities.Add(parent);
        tracker.Update(instance);
        var first = root.Entities[0];

        first.Transform.Parent = parent.Transform;
        var batch = tracker.TakeBatch(2);

        var moved = Assert.IsType<PlayEntityMoved>(Assert.Single(batch!.Operations));
        Assert.Equal(tracker.GetHandle(first), moved.Handle);
        Assert.Equal(tracker.GetHandle(parent), moved.ParentHandle);
    }

    [Fact]
    public void AnEntityAddedAndRemovedInOneFrameIsNotSent()
    {
        tracker.Update(instance);
        var entity = new Entity("Bullet");

        root.Entities.Add(entity);
        root.Entities.Remove(entity);

        Assert.Null(tracker.TakeBatch(2));
        Assert.Equal(2, tracker.CoalescedOperations);
        Assert.Equal(0, tracker.GetHandle(entity));
    }

    [Fact]
    public void AnEntityMovedToAnotherSceneKeepsItsHandle()
    {
        var child = new Scene { Name = "Child" };
        root.Children.Add(child);
        tracker.Update(instance);
        var first = root.Entities[0];
        var handle = tracker.GetHandle(first);
        var tree = tracker.BuildTree(1);

        first.Scene = child;
        var batch = tracker.TakeBatch(2);

        var moved = Assert.IsType<PlayEntityMoved>(Assert.Single(batch!.Operations));
        Assert.Equal(handle, moved.Handle);
        Assert.Equal(tree.Scenes.Single(x => x.Name == "Child").Handle, moved.SceneHandle);
        Assert.Equal(handle, tracker.GetHandle(first));
    }

    [Fact]
    public void InstancesOfAPrefabGetTheirOwnHandles()
    {
        // The instances of a prefab keep the prefab's ids
        var id = Guid.NewGuid();
        var one = new Entity("Instance") { Id = id };
        var two = new Entity("Instance") { Id = id };
        tracker.Update(instance);

        root.Entities.Add(one);
        root.Entities.Add(two);
        var added = tracker.TakeBatch(2)!.Operations.Cast<PlayEntityAdded>().Select(x => x.Entity).ToList();

        Assert.Equal(2, added.Count);
        Assert.All(added, x => Assert.Equal(id, x.Id));
        Assert.NotEqual(added[0].Handle, added[1].Handle);
        Assert.Same(one, tracker.Entities[tracker.GetHandle(one)]);
        Assert.Same(two, tracker.Entities[tracker.GetHandle(two)]);
    }

    [Fact]
    public void HandlesAreNotReused()
    {
        tracker.Update(instance);
        var first = root.Entities[0];
        var handle = tracker.GetHandle(first);

        root.Entities.Remove(first);
        root.Entities.Add(new Entity("Second"));
        tracker.TakeBatch(2);
        root.Entities.Add(first);

        Assert.NotEqual(handle, tracker.GetHandle(first));
        Assert.NotEqual(handle, tracker.GetHandle(root.Entities[0]));
    }

    [Fact]
    public void ARenameIsFoundByThePoll()
    {
        tracker.Update(instance);
        var first = root.Entities[0];

        first.Name = "Renamed";
        Assert.Null(tracker.TakeBatch(2));
        tracker.Poll();
        var batch = tracker.TakeBatch(3);

        var changed = Assert.IsType<PlayEntityStateChanged>(Assert.Single(batch!.Operations));
        Assert.Equal((tracker.GetHandle(first), "Renamed", true), (changed.Handle, changed.Name, changed.Enabled));
        tracker.Poll();
        Assert.Null(tracker.TakeBatch(4));
    }

    [Fact]
    public void AnAddedComponentIsSent()
    {
        tracker.Update(instance);
        var first = root.Entities[0];

        first.Add(new TestComponent());
        var batch = tracker.TakeBatch(2);

        var changed = Assert.IsType<PlayEntityComponentsChanged>(Assert.Single(batch!.Operations));
        Assert.Contains(changed.Components, x => x.TypeName == $"{typeof(TestComponent).FullName}, Stride.PlayMode.Tests");
    }

    [Fact]
    public void AChildSceneIsSentBeforeItsEntitiesAndRemovedAfterThem()
    {
        tracker.Update(instance);
        var child = new Scene { Name = "Child" };
        child.Entities.Add(new Entity("InChild"));

        root.Children.Add(child);
        var added = tracker.TakeBatch(2)!.Operations;
        root.Children.Remove(child);
        var removed = tracker.TakeBatch(3)!.Operations;

        var scene = Assert.IsType<PlaySceneAdded>(added[0]).Scene;
        Assert.Equal("Child", scene.Name);
        Assert.Equal(scene.Handle, Assert.IsType<PlayEntityAdded>(added[1]).Entity.SceneHandle);
        Assert.IsType<PlayEntityRemoved>(removed[0]);
        Assert.Equal(scene.Handle, Assert.IsType<PlaySceneRemoved>(removed[^1]).Handle);
    }

    [Fact]
    public void ANewRootSceneReplacesTheTree()
    {
        tracker.Update(instance);
        root.Entities.Add(new Entity("Pending"));

        instance.RootScene = new Scene { Entities = { new Entity("Other") } };

        Assert.True(tracker.Update(instance));
        Assert.Null(tracker.TakeBatch(2));
        Assert.Equal(["Other"], tracker.BuildTree(2).Entities.Select(x => x.Name));
    }
}
