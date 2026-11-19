// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Reflection;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Engine.Design;
using Stride.PlayMode.Protocol;

namespace Stride.PlayMode.Agent;

/// <summary>
/// Changes the structure of the running game for the subscribers (<see cref="PlayStructureEdit"/>), on the game thread, at the
/// start of a frame with the edits: components added and removed, entities created, deleted, duplicated and moved, prefabs
/// spawned.
/// </summary>
/// <remarks>
/// <para>
/// Everything goes through the engine's own collections, as a script would do it, so the processors see the change at once:
/// a script added is started by the script system later in the same frame (at the next update that runs scripts, when the game
/// is paused), a script removed has its micro-thread cancelled, a Bepu body added joins the simulation. A Bullet body is
/// refused: it would join with no shape, and its shapes cannot change while it is in the simulation. The tree tracker
/// sees the change through the scene instance's events, and the tree batch of the frame carries it.
/// </para>
/// <para>
/// The engine moves an entity between the root of a scene and an entity, or between scenes, by removing it from the game and
/// adding it back in the same frame: its scripts are cancelled and started again, and its bodies leave the simulation and join
/// it again. A move between two entities of the same running scenes keeps them as they are. The tracker gives the moved
/// entity its handle back.
/// </para>
/// </remarks>
/// <param name="tracker">The handles of the running scenes and entities.</param>
/// <param name="contentLoads">Loads the prefabs spawned; <c>null</c> refuses spawns.</param>
internal sealed class PlayStructureApplier(PlayTreeTracker tracker, PlayContentLoads? contentLoads = null)
{
    /// <summary>
    /// The name of an entity created without one, as the scene editor names an empty entity.
    /// </summary>
    public const string DefaultEntityName = "Entity";

    /// <summary>
    /// Applies a command.
    /// </summary>
    /// <param name="edit">The command.</param>
    /// <param name="frame">The agent's frame.</param>
    /// <returns>Its result.</returns>
    public PlayStructureResult Apply(PlayStructureEdit edit, long frame)
    {
        ArgumentNullException.ThrowIfNull(edit);
        try
        {
            var (entities, componentId) = edit.Operation switch
            {
                PlayStructureOperation.AddComponent => AddComponent(edit),
                PlayStructureOperation.RemoveComponent => RemoveComponent(edit),
                PlayStructureOperation.CreateEntity => CreateEntity(edit),
                PlayStructureOperation.DeleteEntity => DeleteEntity(edit),
                PlayStructureOperation.DuplicateEntity => DuplicateEntity(edit),
                PlayStructureOperation.MoveEntity => MoveEntity(edit),
                PlayStructureOperation.SpawnPrefab => SpawnPrefab(edit),
                _ => throw new PlayEditException($"The game does not know structure commands of kind {(byte)edit.Operation}."),
            };
            return new PlayStructureResult(edit.Id, frame, PlayEditStatus.Applied, entities, componentId, null);
        }
        catch (Exception e)
        {
            contentLoads?.Abort();
            var status = e is PlayEditGoneException ? PlayEditStatus.Gone : PlayEditStatus.Failed;
            return new PlayStructureResult(edit.Id, frame, status, [], Guid.Empty, PlayEditApplier.Describe(e));
        }
    }

    private (IReadOnlyList<int>, Guid) AddComponent(PlayStructureEdit edit)
    {
        var entity = FindEntity(edit.EntityHandle);
        var type = PlayEditApplier.FindType(edit.TypeName ?? string.Empty);
        if (!typeof(EntityComponent).IsAssignableFrom(type))
            throw new PlayEditException($"{type.Name} is not a component in the game.");
        if (type.IsAbstract)
            throw new PlayEditException($"{type.Name} is abstract in the game: no instance of it can be made.");
        if (typeof(TransformComponent).IsAssignableFrom(type))
            throw new PlayEditException($"{entity.Name} already has its transform: an entity has one, which cannot be removed or added.");
        // A Bullet body joins the simulation with the shapes it has, and they cannot change while it is in it: one added now
        // would have none and stay inert
        if (IsBulletBody(type))
            throw new PlayEditException($"A Bullet physics component added while the game runs has no collider shape, and its shapes cannot change until the game restarts: add the {type.Name} in the scene editor.");
        // As the engine's collection checks it, with the reason the asset grid gives
        if (!EntityComponentAttributes.Get(type).AllowMultipleComponents && entity.Components.Any(x => x.GetType() == type))
            throw new PlayEditException($"{entity.Name} already has a {type.Name}, and an entity can have only one.");
        var component = (EntityComponent)PlayEditApplier.CreateInstance(type);
        if (edit.Index >= 0 && edit.Index < entity.Components.Count)
            entity.Components.Insert(edit.Index, component);
        else
            entity.Components.Add(component);
        return ([], component.Id);
    }

    private (IReadOnlyList<int>, Guid) RemoveComponent(PlayStructureEdit edit)
    {
        var entity = FindEntity(edit.EntityHandle);
        var component = entity.Components.FirstOrDefault(x => x.Id == edit.ComponentId)
            ?? throw new PlayEditGoneException($"{entity.Name} no longer has this component.");
        if (component is TransformComponent)
            throw new PlayEditException($"The transform of {entity.Name} cannot be removed: every entity has one.");
        // [RequireComponent] says which components a component needs; the engine declares it and checks nothing
        foreach (var other in entity.Components)
        {
            if (other == component)
                continue;
            foreach (var required in other.GetType().GetCustomAttributes<RequireComponentAttribute>(inherit: true))
            {
                if (required.Type is { } needed && needed.IsInstanceOfType(component) && !entity.Components.Any(x => x != component && needed.IsInstanceOfType(x)))
                    throw new PlayEditException($"{other.GetType().Name} of {entity.Name} requires a {needed.Name}: remove it first.");
            }
        }
        entity.Components.Remove(component);
        return ([], Guid.Empty);
    }

    private (IReadOnlyList<int>, Guid) CreateEntity(PlayStructureEdit edit)
    {
        var entity = new Entity(string.IsNullOrEmpty(edit.Name) ? DefaultEntityName : edit.Name);
        AddTo(entity, edit.ParentHandle, edit.SceneHandle);
        return (Handles([entity]), Guid.Empty);
    }

    private (IReadOnlyList<int>, Guid) DeleteEntity(PlayStructureEdit edit)
    {
        var entity = FindEntity(edit.EntityHandle);
        // A child leaves its parent, which takes it out of the game; an entity at the root leaves its scene
        if (entity.Transform.Parent is not null)
            entity.Transform.Parent = null;
        else
            entity.Scene = null;
        return ([], Guid.Empty);
    }

    private (IReadOnlyList<int>, Guid) DuplicateEntity(PlayStructureEdit edit)
    {
        var entity = FindEntity(edit.EntityHandle);
        var copy = entity.Clone();
        // Right after the original, under the same parent
        if (entity.Transform.Parent is { } parent)
            parent.Children.Insert(parent.Children.IndexOf(entity.Transform) + 1, copy.Transform);
        else if (entity.Scene is { } scene)
            scene.Entities.Insert(scene.Entities.IndexOf(entity) + 1, copy);
        else
            throw new PlayEditGoneException($"{entity.Name} is no longer in a scene of the game.");
        // The clone's bodies join the simulation where its transform is, as the original's did
        return (Handles([copy]), Guid.Empty);
    }

    private (IReadOnlyList<int>, Guid) MoveEntity(PlayStructureEdit edit)
    {
        var entity = FindEntity(edit.EntityHandle);
        Entity? parent = null;
        Scene scene;
        if (edit.ParentHandle != 0)
        {
            parent = FindEntity(edit.ParentHandle, "The new parent");
            for (var ancestor = parent; ancestor is not null; ancestor = ancestor.Transform.Parent?.Entity)
            {
                if (ancestor == entity)
                    throw new PlayEditException($"{entity.Name} cannot go under itself or one of its children.");
            }
            scene = parent.Scene;
        }
        else
        {
            scene = FindScene(edit.SceneHandle);
        }

        // Nothing to do when it is already there: the last child of its parent, or at the root of its scene
        if (parent is not null && entity.Transform.Parent == parent.Transform || parent is null && entity.Transform.Parent is null && entity.Scene == scene)
            return ([], Guid.Empty);

        entity.Transform.UpdateWorldMatrix();
        var world = entity.Transform.WorldMatrix;
        if (parent is not null)
        {
            // The engine's own move: between two entities of the same running scenes, the entity stays in the game
            entity.Transform.Parent = parent.Transform;
        }
        else
        {
            if (entity.Transform.Parent is not null)
                entity.Transform.Parent = null;
            entity.Scene = scene;
        }

        if (edit.KeepWorldTransform)
        {
            if (parent is not null)
                parent.Transform.UpdateWorldMatrix();
            else
                scene.UpdateWorldMatrix();
            var transform = entity.Transform;
            transform.WorldMatrix = world;
            transform.UpdateLocalFromWorld();
            transform.LocalMatrix.Decompose(out Vector3 scale, out Quaternion rotation, out Vector3 position);
            transform.Position = position;
            transform.Rotation = rotation;
            transform.Scale = scale;
        }
        // Its bodies, and its children's, go where the entity is now
        PlayPhysicsTeleport.Teleport(entity);
        return ([], Guid.Empty);
    }

    private (IReadOnlyList<int>, Guid) SpawnPrefab(PlayStructureEdit edit)
    {
        if (string.IsNullOrEmpty(edit.Url))
            throw new PlayEditException("No prefab was given to spawn.");
        if (contentLoads is null)
            throw new PlayEditException("This game does not load content for edits.");
        // Checked before the load: a prefab is spawned where the editor said, or not at all
        var (parentHandle, sceneHandle) = (edit.ParentHandle, edit.SceneHandle);
        if (parentHandle != 0)
            FindEntity(parentHandle, "The parent");
        else
            FindScene(sceneHandle);
        var prefab = (Prefab)contentLoads.Load(typeof(Prefab), edit.Url);
        // Its instances share the content it loaded (models, materials): the load stays until the game exits
        contentLoads.KeepUntilExit();
        var roots = prefab.Instantiate();
        foreach (var root in roots)
            AddTo(root, parentHandle, sceneHandle);
        return (Handles(roots), Guid.Empty);
    }

    private void AddTo(Entity entity, int parentHandle, int sceneHandle)
    {
        if (parentHandle != 0)
            FindEntity(parentHandle, "The parent").Transform.Children.Add(entity.Transform);
        else
            FindScene(sceneHandle).Entities.Add(entity);
    }

    /// <summary>
    /// The handles of entities just added to the game and of their descendants, each after its parent.
    /// </summary>
    private List<int> Handles(IEnumerable<Entity> roots)
    {
        var handles = new List<int>();
        foreach (var root in roots)
            Add(root);
        return handles;

        void Add(Entity entity)
        {
            if (tracker.GetHandle(entity) is var handle and > 0)
                handles.Add(handle);
            foreach (var child in entity.Transform.Children)
            {
                if (child.Entity is { } childEntity)
                    Add(childEntity);
            }
        }
    }

    private static bool IsBulletBody(Type type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.FullName == "Stride.Engine.PhysicsComponent")
                return true;
        }
        return false;
    }

    private Entity FindEntity(int handle, string what = "The entity")
        => tracker.Entities.TryGetValue(handle, out var entity) && entity.EntityManager is not null
            ? entity
            : throw new PlayEditGoneException($"{what} is no longer in the game.");

    private Scene FindScene(int handle)
    {
        // No scene named: the root scene
        if (handle == 0)
            return tracker.SceneInstance?.RootScene ?? throw new PlayEditGoneException("The game has no running scene.");
        return tracker.Scenes.TryGetValue(handle, out var scene) ? scene : throw new PlayEditGoneException("The scene is no longer running in the game.");
    }
}
