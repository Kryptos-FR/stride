// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.Specialized;
using Stride.Core.Collections;
using Stride.Core.Serialization;
using Stride.Engine;
using Stride.Engine.Design;
using Stride.PlayMode.Protocol;
using Stride.PlayMode.Snapshots;

namespace Stride.PlayMode.Agent;

/// <summary>
/// Follows the scenes and entities of a running <see cref="SceneInstance"/> on the game thread: gives each scene and entity
/// a runtime handle, and turns the engine's events of a frame into one coalesced <see cref="PlayTreeBatch"/>.
/// </summary>
/// <remarks>
/// <para>
/// Handles are never reused for the life of the tracker: the instances of a prefab keep the prefab's ids, so the id cannot
/// name a live entity. Names and enabled states raise no event: <see cref="Poll"/> compares them with their last values.
/// </para>
/// <para>
/// Not thread-safe: everything runs on the game thread, from the engine's events and from <see cref="Update"/>.
/// </para>
/// </remarks>
public sealed class PlayTreeTracker : IDisposable
{
    private readonly Dictionary<Entity, int> entityHandles = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<int, Entity> entities = [];
    private readonly Dictionary<Scene, int> sceneHandles = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<int, Scene> scenes = [];
    private readonly Dictionary<Entity, (string? Name, bool Enabled)> states = new(ReferenceEqualityComparer.Instance);
    private readonly List<Pending> pending = [];
    private readonly Dictionary<Entity, int> addedThisFrame = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<Scene, int> scenesAddedThisFrame = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<Entity, (int Handle, int Index)> removedThisFrame = new(ReferenceEqualityComparer.Instance);
    private Scene? rootScene;
    private int nextHandle = 1;

    /// <summary>
    /// The scene instance followed, or <c>null</c> before the first <see cref="Update"/> with one.
    /// </summary>
    public SceneInstance? SceneInstance { get; private set; }

    /// <summary>
    /// The number of operations dropped by coalescing since the start (an entity added and removed in the same frame, a
    /// change of an entity added or removed in the frame).
    /// </summary>
    public int CoalescedOperations { get; private set; }

    /// <summary>
    /// The entities followed, by handle.
    /// </summary>
    public IReadOnlyDictionary<int, Entity> Entities => entities;

    /// <summary>
    /// Gives the handle of an entity of the running scenes, or 0 for an entity the tracker does not follow.
    /// </summary>
    public int GetHandle(Entity entity) => entity is not null && entityHandles.TryGetValue(entity, out var handle) ? handle : 0;

    /// <summary>
    /// Follows a scene instance, or the new root scene of the same instance.
    /// </summary>
    /// <param name="sceneInstance">The game's scene instance.</param>
    /// <returns><c>true</c> when the tree was replaced (a new instance or root scene): subscribers need the whole tree
    /// (<see cref="BuildTree"/>), and the pending changes were dropped.</returns>
    public bool Update(SceneInstance? sceneInstance)
    {
        if (ReferenceEquals(sceneInstance, SceneInstance) && ReferenceEquals(sceneInstance?.RootScene, rootScene))
            return false;
        Attach(sceneInstance);
        return true;
    }

    /// <summary>
    /// Compares the names and enabled states of the entities with their last values, and records the changes.
    /// </summary>
    public void Poll()
    {
        foreach (var (entity, previous) in states.ToList())
        {
            var state = (entity.Name, IsEnabled(entity));
            if (state == previous)
                continue;
            states[entity] = state;
            if (!addedThisFrame.ContainsKey(entity))
                pending.Add(new Pending(PlayTreeOperationKind.StateChanged, entity, null));
        }
    }

    /// <summary>
    /// Builds the whole tree of the running scenes.
    /// </summary>
    /// <param name="frame">The game's frame.</param>
    public PlayTree BuildTree(long frame)
    {
        var sceneInfos = new List<PlaySceneInfo>();
        var entityInfos = new List<PlayEntityInfo>();
        if (rootScene is not null)
            AddScene(rootScene);
        return new PlayTree(frame, sceneInfos, entityInfos);

        void AddScene(Scene scene)
        {
            sceneInfos.Add(Describe(scene));
            foreach (var entity in scene.Entities)
                AddEntity(entity);
            foreach (var child in scene.Children)
                AddScene(child);
        }

        void AddEntity(Entity entity)
        {
            if (!entityHandles.ContainsKey(entity))
                return;
            entityInfos.Add(Describe(entity));
            foreach (var child in entity.Transform.Children)
            {
                if (child.Entity is { } childEntity)
                    AddEntity(childEntity);
            }
        }
    }

    /// <summary>
    /// Gives the changes of the frame, coalesced, and starts the next frame.
    /// </summary>
    /// <param name="frame">The game's frame.</param>
    /// <returns>The batch, or <c>null</c> when nothing changed.</returns>
    public PlayTreeBatch? TakeBatch(long frame)
    {
        if (pending.Count == 0)
            return null;

        var removed = new HashSet<Entity>(ReferenceEqualityComparer.Instance);
        foreach (var change in pending)
        {
            if (change.Kind == PlayTreeOperationKind.EntityRemoved)
                removed.Add(change.Entity!);
        }

        // Scenes added first (their entities refer to them), then entities added, parents before children (the engine adds
        // the children first), then the other changes, and scenes removed last (their entities go before them)
        var operations = new List<PlayTreeOperation>(pending.Count);
        foreach (var change in pending)
        {
            if (change.Kind == PlayTreeOperationKind.SceneAdded)
                operations.Add(new PlaySceneAdded(Describe(change.Scene!)));
        }
        foreach (var change in pending.Where(x => x.Kind == PlayTreeOperationKind.EntityAdded).OrderBy(x => Depth(x.Entity!)))
            operations.Add(new PlayEntityAdded(Describe(change.Entity!)));
        var moved = new HashSet<Entity>(ReferenceEqualityComparer.Instance);
        foreach (var change in pending)
        {
            switch (change.Kind)
            {
                case PlayTreeOperationKind.EntityAdded:
                    break;
                case PlayTreeOperationKind.EntityRemoved:
                    operations.Add(new PlayEntityRemoved(change.Handle));
                    break;
                case PlayTreeOperationKind.EntityMoved when !removed.Contains(change.Entity!) && moved.Add(change.Entity!):
                    operations.Add(new PlayEntityMoved(GetHandle(change.Entity!), ParentHandle(change.Entity!), SceneHandle(change.Entity!)));
                    break;
                case PlayTreeOperationKind.ComponentsChanged when !removed.Contains(change.Entity!):
                    operations.Add(new PlayEntityComponentsChanged(GetHandle(change.Entity!), DescribeComponents(change.Entity!)));
                    break;
                case PlayTreeOperationKind.StateChanged when !removed.Contains(change.Entity!):
                    var (name, enabled) = states.GetValueOrDefault(change.Entity!);
                    operations.Add(new PlayEntityStateChanged(GetHandle(change.Entity!), name, enabled));
                    break;
                case PlayTreeOperationKind.SceneAdded or PlayTreeOperationKind.SceneRemoved or 0:
                    // Scenes go before and after; 0 is a change coalesced away
                    break;
                default:
                    CoalescedOperations++;
                    break;
            }
        }
        foreach (var change in pending)
        {
            if (change.Kind == PlayTreeOperationKind.SceneRemoved)
                operations.Add(new PlaySceneRemoved(change.Handle));
        }

        pending.Clear();
        addedThisFrame.Clear();
        scenesAddedThisFrame.Clear();
        removedThisFrame.Clear();
        return operations.Count > 0 ? new PlayTreeBatch(frame, operations) : null;
    }

    /// <inheritdoc/>
    public void Dispose() => Attach(null);

    private void Attach(SceneInstance? sceneInstance)
    {
        if (SceneInstance is { } previous)
        {
            previous.EntityAdded -= OnEntityAdded;
            previous.EntityRemoved -= OnEntityRemoved;
            previous.HierarchyChanged -= OnHierarchyChanged;
            previous.ComponentChanged -= OnComponentChanged;
        }
        foreach (var scene in sceneHandles.Keys)
            scene.Children.CollectionChanged -= OnSceneChildrenChanged;

        entityHandles.Clear();
        entities.Clear();
        sceneHandles.Clear();
        scenes.Clear();
        states.Clear();
        pending.Clear();
        addedThisFrame.Clear();
        scenesAddedThisFrame.Clear();
        removedThisFrame.Clear();

        SceneInstance = sceneInstance;
        rootScene = sceneInstance?.RootScene;
        if (sceneInstance is null)
            return;
        sceneInstance.EntityAdded += OnEntityAdded;
        sceneInstance.EntityRemoved += OnEntityRemoved;
        sceneInstance.HierarchyChanged += OnHierarchyChanged;
        sceneInstance.ComponentChanged += OnComponentChanged;
        if (rootScene is not null)
            TrackScene(rootScene);
        foreach (var entity in sceneInstance)
            Track(entity);
    }

    private void TrackScene(Scene scene)
    {
        if (sceneHandles.ContainsKey(scene))
            return;
        var handle = nextHandle++;
        sceneHandles.Add(scene, handle);
        scenes.Add(handle, scene);
        scene.Children.CollectionChanged += OnSceneChildrenChanged;
        foreach (var child in scene.Children)
            TrackScene(child);
    }

    private void UntrackScene(Scene scene)
    {
        if (!sceneHandles.Remove(scene, out var handle))
            return;
        scenes.Remove(handle);
        scene.Children.CollectionChanged -= OnSceneChildrenChanged;
        foreach (var child in scene.Children)
            UntrackScene(child);
        if (scenesAddedThisFrame.Remove(scene, out var index))
        {
            // Added and removed in the same frame: neither is sent
            pending[index] = default;
            CoalescedOperations += 2;
            return;
        }
        pending.Add(new Pending(PlayTreeOperationKind.SceneRemoved, null, null, handle));
    }

    private int Track(Entity entity)
    {
        if (entityHandles.TryGetValue(entity, out var handle))
            return handle;
        handle = nextHandle++;
        entityHandles.Add(entity, handle);
        entities.Add(handle, entity);
        states[entity] = (entity.Name, IsEnabled(entity));
        return handle;
    }

    private void OnEntityAdded(object? sender, Entity entity)
    {
        if (entityHandles.ContainsKey(entity))
            return;
        if (removedThisFrame.Remove(entity, out var removed))
        {
            // Removed and added back in the same frame (moved to another scene): the same entity, with the same handle
            entityHandles.Add(entity, removed.Handle);
            entities.Add(removed.Handle, entity);
            states[entity] = (entity.Name, IsEnabled(entity));
            pending[removed.Index] = default;
            CoalescedOperations++;
            pending.Add(new Pending(PlayTreeOperationKind.EntityMoved, entity, null));
            return;
        }
        Track(entity);
        addedThisFrame[entity] = pending.Count;
        pending.Add(new Pending(PlayTreeOperationKind.EntityAdded, entity, null));
    }

    private void OnEntityRemoved(object? sender, Entity entity)
    {
        if (!entityHandles.Remove(entity, out var handle))
            return;
        entities.Remove(handle);
        states.Remove(entity);
        if (addedThisFrame.Remove(entity, out var index))
        {
            // Added and removed in the same frame: neither is sent
            pending[index] = default;
            CoalescedOperations += 2;
            return;
        }
        removedThisFrame[entity] = (handle, pending.Count);
        pending.Add(new Pending(PlayTreeOperationKind.EntityRemoved, entity, null, handle));
    }

    private void OnHierarchyChanged(object? sender, Entity entity)
    {
        // An entity added in this frame is sent with its parent at the end of the frame; the engine raises this event
        // before the add when a child is attached to an entity already in the scene
        if (addedThisFrame.ContainsKey(entity) || !entityHandles.ContainsKey(entity))
        {
            CoalescedOperations++;
            return;
        }
        pending.Add(new Pending(PlayTreeOperationKind.EntityMoved, entity, null));
    }

    private void OnComponentChanged(object? sender, EntityComponentEventArgs e)
    {
        if (addedThisFrame.ContainsKey(e.Entity) || !entityHandles.ContainsKey(e.Entity))
        {
            CoalescedOperations++;
            return;
        }
        if (e.NewComponent is not null)
            PlayMirrorSerializer.RegisterComponentTypes([e.NewComponent.GetType()]);
        pending.Add(new Pending(PlayTreeOperationKind.ComponentsChanged, e.Entity, null));
    }

    private void OnSceneChildrenChanged(object? sender, TrackingCollectionChangedEventArgs e)
    {
        if (e.Item is not Scene scene)
            return;
        if (e.Action == NotifyCollectionChangedAction.Add && !sceneHandles.ContainsKey(scene))
        {
            TrackScene(scene);
            scenesAddedThisFrame[scene] = pending.Count;
            pending.Add(new Pending(PlayTreeOperationKind.SceneAdded, null, scene));
        }
        else if (e.Action == NotifyCollectionChangedAction.Remove)
        {
            UntrackScene(scene);
        }
    }

    private PlaySceneInfo Describe(Scene scene)
        => new(sceneHandles.GetValueOrDefault(scene), scene.Id, scene.Name, AttachedReferenceManager.GetUrl(scene), scene.Parent is { } parent ? sceneHandles.GetValueOrDefault(parent) : 0);

    private PlayEntityInfo Describe(Entity entity)
    {
        var (name, enabled) = states.GetValueOrDefault(entity, (entity.Name, IsEnabled(entity)));
        return new PlayEntityInfo(GetHandle(entity), entity.Id, name, enabled, ParentHandle(entity), SceneHandle(entity), DescribeComponents(entity));
    }

    private static List<PlayComponentInfo> DescribeComponents(Entity entity)
    {
        var components = new List<PlayComponentInfo>(entity.Components.Count);
        foreach (var component in entity.Components)
            components.Add(new PlayComponentInfo(component.Id, PlayMirrorSerializer.TypeNameOf(component.GetType())));
        return components;
    }

    private static int Depth(Entity entity)
    {
        var depth = 0;
        for (var parent = entity.Transform.Parent; parent is not null; parent = parent.Parent)
            depth++;
        return depth;
    }

    private int ParentHandle(Entity entity) => entity.Transform.Parent?.Entity is { } parent ? GetHandle(parent) : 0;

    private int SceneHandle(Entity entity) => entity.Scene is { } scene ? sceneHandles.GetValueOrDefault(scene) : 0;

    private static bool IsEnabled(Entity entity)
    {
        foreach (var component in entity.Components)
        {
            if (component is ActivableEntityComponent { Enabled: false })
                return false;
        }
        return true;
    }

    private readonly record struct Pending(PlayTreeOperationKind Kind, Entity? Entity, Scene? Scene, int Handle = 0);
}
