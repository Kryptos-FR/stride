// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.ObjectModel;
using Stride.Editor.PlayMode;
using Stride.PlayMode.Protocol;

namespace Stride.Editor.Tests.PlayMode;

/// <summary>
/// Tests <see cref="LiveTree"/>: the tree of the running scenes from the agent's tree and batches, and its rows.
/// </summary>
public sealed class LiveTreeTests
{
    internal const string Transform = "Stride.Engine.TransformComponent, Stride.Engine";
    internal const string Model = "Stride.Engine.ModelComponent, Stride.Engine";

    internal static readonly Guid SceneId = Guid.NewGuid();
    internal static readonly Guid PlayerId = Guid.NewGuid();
    internal static readonly Guid CameraId = Guid.NewGuid();
    internal static readonly Guid GroundId = Guid.NewGuid();
    internal static readonly Guid BulletId = Guid.NewGuid();

    [Fact]
    public void TheTreeShowsTheScenesAndEntitiesInTheGamesOrder()
    {
        var tree = new LiveTree();

        var removed = tree.Reset(Tree());

        Assert.Empty(removed);
        Assert.Equal(3, tree.EntityCount);
        var scene = Assert.Single(tree.Roots);
        Assert.Equal("MainScene", scene.Name);
        Assert.True(scene.IsExpanded);
        Assert.Equal(["Player", "Ground"], scene.Children.Select(x => x.Name));
        var player = tree.FindEntity(2)!;
        Assert.Equal(["Camera"], player.Children.Select(x => x.Name));
        Assert.Same(player, tree.FindEntity(3)!.Parent);
        Assert.Equal([Transform, Model], tree.FindEntity(4)!.ComponentTypeNames);
        // The entities are collapsed: the camera under the player is not a row
        Assert.Equal(["MainScene", "Player", "Ground"], tree.Rows.Select(x => x.Name));
        Assert.Equal([0, 1, 1], tree.Rows.Select(x => x.Depth));
        Assert.True(player.HasChildren);
        Assert.False(tree.FindEntity(4)!.HasChildren);
    }

    [Fact]
    public void ExpandingAnEntityShowsItsChildren()
    {
        var tree = new LiveTree();
        tree.Reset(Tree());

        tree.FindEntity(2)!.IsExpanded = true;

        Assert.Equal(["MainScene", "Player", "Camera", "Ground"], tree.Rows.Select(x => x.Name));
        Assert.Equal(2, tree.FindEntity(3)!.Depth);

        tree.SetAllExpanded(false);
        Assert.Equal(["MainScene", "Player", "Ground"], tree.Rows.Select(x => x.Name));
        tree.SetAllExpanded(true);
        Assert.Equal(4, tree.Rows.Count);
    }

    [Fact]
    public void BatchesAddRemoveMoveRenameAndDisableEntities()
    {
        var tree = new LiveTree();
        tree.Reset(Tree());
        tree.SetAllExpanded(true);
        var player = tree.FindEntity(2)!;
        var ground = tree.FindEntity(4)!;
        var rows = tree.Rows;
        var changes = new List<string>();
        ground.PropertyChanged += (_, e) => changes.Add(e.PropertyName!);

        // Added: a bullet and its trail; the camera moves to the root of the scene; the ground is renamed and disabled
        var removed = tree.Apply(new PlayTreeBatch(10,
        [
            new PlayEntityAdded(Entity(5, BulletId, "Bullet", 0, Model)),
            new PlayEntityAdded(Entity(6, Guid.NewGuid(), "Trail", 5)),
            new PlayEntityMoved(3, 0, 1),
            new PlayEntityStateChanged(4, "Floor", false),
            new PlayEntityComponentsChanged(2, [new PlayComponentInfo(Guid.NewGuid(), Transform)]),
        ]));

        Assert.Empty(removed);
        Assert.Same(rows, tree.Rows);
        Assert.Equal(["MainScene", "Player", "Floor", "Bullet", "Camera"], tree.Rows.Select(x => x.Name));
        Assert.False(player.HasChildren);
        Assert.Equal("Floor", ground.Name);
        Assert.False(ground.IsEnabled);
        Assert.Contains(nameof(LiveEntityItem.Name), changes);
        Assert.Contains(nameof(LiveEntityItem.IsEnabled), changes);
        Assert.Equal([Transform], player.ComponentTypeNames);
        Assert.Equal(5, tree.EntityCount);

        // Removed: the bullet goes with its trail
        removed = tree.Apply(new PlayTreeBatch(11, [new PlayEntityRemoved(5)]));

        Assert.Equal([5, 6], removed.Order());
        Assert.Null(tree.FindEntity(6));
        Assert.Equal(["MainScene", "Player", "Floor", "Camera"], tree.Rows.Select(x => x.Name));
        Assert.Equal(3, tree.EntityCount);
    }

    [Fact]
    public void TheRowsThatStayAreTheSameObjects()
    {
        var tree = new LiveTree();
        tree.Reset(Tree());
        var before = tree.Rows.ToList();
        var notifications = new List<System.Collections.Specialized.NotifyCollectionChangedAction>();
        tree.Rows.CollectionChanged += (_, e) => notifications.Add(e.Action);

        tree.Apply(new PlayTreeBatch(10, [new PlayEntityAdded(Entity(5, BulletId, "Bullet", 0))]));

        // One insert: the rows that were there are not removed and added again
        Assert.Equal([System.Collections.Specialized.NotifyCollectionChangedAction.Add], notifications);
        Assert.Equal(before, tree.Rows.Take(before.Count));
    }

    [Fact]
    public void ChildScenesAreAddedAndRemovedWithTheirEntities()
    {
        var tree = new LiveTree();
        tree.Reset(Tree());

        tree.Apply(new PlayTreeBatch(10,
        [
            new PlaySceneAdded(new PlaySceneInfo(20, Guid.NewGuid(), "Level2", "Scenes/Level2", 1)),
            new PlayEntityAdded(Entity(21, Guid.NewGuid(), "Door", 0, Model, scene: 20)),
        ]));

        Assert.Equal(["MainScene", "Player", "Ground", "Level2", "Door"], tree.Rows.Select(x => x.Name));
        Assert.Equal(2, tree.FindEntity(21)!.Depth);

        var removed = tree.Apply(new PlayTreeBatch(11, [new PlaySceneRemoved(20)]));

        Assert.Equal([21], removed);
        Assert.Null(tree.FindScene(20));
        Assert.Equal(["MainScene", "Player", "Ground"], tree.Rows.Select(x => x.Name));
    }

    [Fact]
    public void ANewTreeReplacesTheOldOne()
    {
        var tree = new LiveTree();
        tree.Reset(Tree());

        var removed = tree.Reset(new PlayTree(20, [new PlaySceneInfo(30, Guid.NewGuid(), "Menu", "Scenes/Menu", 0)], [Entity(31, Guid.NewGuid(), "Title", 0, [], scene: 30)]));

        Assert.Equal([2, 3, 4], removed.Order());
        Assert.Equal(["Menu", "Title"], tree.Rows.Select(x => x.Name));
    }

    [Fact]
    public void TheSearchShowsTheMatchesAndTheRowsThatLeadToThem()
    {
        var tree = new LiveTree();
        tree.Reset(Tree());

        tree.Filter = "cam";

        Assert.Equal(["MainScene", "Player", "Camera"], tree.Rows.Select(x => x.Name));
        Assert.True(tree.FindEntity(3)!.IsMatch);
        Assert.False(tree.FindEntity(2)!.IsMatch);
        // A collapsed row that leads to a match is shown with it, and stays collapsed once the search is cleared
        Assert.False(tree.FindEntity(2)!.IsExpanded);

        tree.Filter = "";
        Assert.Equal(["MainScene", "Player", "Ground"], tree.Rows.Select(x => x.Name));
        Assert.False(tree.FindEntity(3)!.IsMatch);

        tree.Filter = "nothing";
        Assert.Empty(tree.Rows);
    }

    [Fact]
    public void EntitiesWithoutACounterpartInTheSceneAssetsAreSpawned()
    {
        // The scene asset has the player, the camera and the ground; a bullet prefab is instantiated twice at run time
        var tree = new LiveTree(scene => scene.Id == SceneId ? [PlayerId, CameraId, GroundId] : null);
        tree.Reset(Tree());

        Assert.All(new[] { 2, 3, 4 }.Select(x => tree.FindEntity(x)!), x => Assert.False(x.IsSpawned));
        Assert.Same(tree.Roots[0], tree.FindEntity(2)!.CounterpartScene);

        tree.Apply(new PlayTreeBatch(10,
        [
            new PlayEntityAdded(Entity(5, BulletId, "Bullet", 0)),
            new PlayEntityAdded(Entity(6, BulletId, "Bullet", 0)),
            // A copy of the ground with its id (an instance of a prefab made of the scene's ground, for example)
            new PlayEntityAdded(Entity(7, GroundId, "Ground", 0)),
        ]));

        Assert.True(tree.FindEntity(5)!.IsSpawned);
        Assert.True(tree.FindEntity(6)!.IsSpawned);
        // Only the first live entity with the id of the scene's ground is its counterpart
        Assert.True(tree.FindEntity(7)!.IsSpawned);
        Assert.False(tree.FindEntity(4)!.IsSpawned);

        // A scene with no asset in the session: everything in it is spawned
        tree.Apply(new PlayTreeBatch(11,
        [
            new PlaySceneAdded(new PlaySceneInfo(20, Guid.NewGuid(), "Generated", null, 1)),
            new PlayEntityAdded(Entity(21, Guid.NewGuid(), "Room", 0, [], scene: 20)),
        ]));
        Assert.True(tree.FindEntity(21)!.IsSpawned);
    }

    [Fact]
    public void WithoutSceneAssetsEveryEntityIsSpawned()
    {
        var tree = new LiveTree();
        tree.Reset(Tree());

        Assert.True(tree.FindEntity(2)!.IsSpawned);
    }

    [Fact]
    public void PatchTurnsACollectionIntoAnother()
    {
        string a = "a", b = "b", c = "c", d = "d", e = "e";
        var target = new ObservableCollection<string>([a, b, c, d]);

        LiveTree.Patch(target, [e, c, a, d]);

        Assert.Equal([e, c, a, d], target);
    }

    internal static PlayTree Tree() => new(1,
        // A loaded scene keeps the default name of a scene: the tree shows the name of its asset
        [new PlaySceneInfo(1, SceneId, "Scene", "MainScene", 0)],
        [
            Entity(2, PlayerId, "Player", 0, Transform),
            Entity(3, CameraId, "Camera", 2, "Stride.Engine.CameraComponent, Stride.Engine"),
            Entity(4, GroundId, "Ground", 0, Transform, Model),
        ]);

    internal static PlayEntityInfo Entity(int handle, Guid id, string name, int parent, params string[] components)
        => Entity(handle, id, name, parent, components, scene: 1);

    internal static PlayEntityInfo Entity(int handle, Guid id, string name, int parent, string component, int scene)
        => Entity(handle, id, name, parent, [component], scene);

    internal static PlayEntityInfo Entity(int handle, Guid id, string name, int parent, string[] components, int scene = 1, bool enabled = true)
        => new(handle, id, name, enabled, parent, scene, [.. components.Select(x => new PlayComponentInfo(Guid.NewGuid(), x))]);
}
