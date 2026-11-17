// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Stride.Core;
using Stride.Core.Collections;
using Stride.Core.Serialization;
using Stride.Engine;
using Stride.Engine.Design;

namespace Stride.PlayMode.Spike;

/// <summary>
/// Sends the tree of the running scenes at connect, then the changes, batched per frame and coalesced.
/// </summary>
/// <remarks>
/// It also keeps the table of every entity and component of the scene instance by id, which the binary snapshots use to
/// write references to objects outside the snapshot as ids.
/// </remarks>
internal sealed class TreeTracker(Action<PlayMessageType, byte[]> send)
{
    private readonly List<Op> pending = [];
    private readonly Dictionary<Guid, int> pendingAdds = [];
    private readonly HashSet<Scene> trackedScenes = [];
    private SceneInstance? sceneInstance;
    private Scene? rootScene;
    private long lastPollFrame;

    /// <summary>Every entity and component of the scene instance, by id.</summary>
    public Dictionary<Guid, IIdentifiable> Identifiables { get; } = [];

    /// <summary>The number of batches sent, and the time spent building them.</summary>
    public int BatchesSent { get; private set; }

    public int OpsSent { get; private set; }

    public int OpsCoalesced { get; private set; }

    public void Update(SceneInstance instance, long frame)
    {
        if (!ReferenceEquals(instance, sceneInstance) || !ReferenceEquals(instance.RootScene, rootScene))
        {
            Attach(instance);
            SendTree();
            return;
        }

        // Entity names and component enabled states raise no event: poll them twice a second (at 60 fps).
        if (frame - lastPollFrame >= 30)
        {
            lastPollFrame = frame;
            PollNamesAndEnabled();
        }

        if (pending.Count > 0)
            SendBatch(frame);
    }

    private void Attach(SceneInstance instance)
    {
        if (sceneInstance is not null)
        {
            sceneInstance.EntityAdded -= OnEntityAdded;
            sceneInstance.EntityRemoved -= OnEntityRemoved;
            sceneInstance.HierarchyChanged -= OnHierarchyChanged;
            sceneInstance.ComponentChanged -= OnComponentChanged;
        }
        foreach (var scene in trackedScenes)
            scene.Children.CollectionChanged -= OnSceneChildrenChanged;
        trackedScenes.Clear();

        sceneInstance = instance;
        rootScene = instance.RootScene;
        instance.EntityAdded += OnEntityAdded;
        instance.EntityRemoved += OnEntityRemoved;
        instance.HierarchyChanged += OnHierarchyChanged;
        instance.ComponentChanged += OnComponentChanged;
        if (rootScene is not null)
            TrackScene(rootScene);

        Identifiables.Clear();
        foreach (var entity in instance)
            AddIdentifiables(entity);
        pending.Clear();
        pendingAdds.Clear();
    }

    private void TrackScene(Scene scene)
    {
        if (!trackedScenes.Add(scene))
            return;
        scene.Children.CollectionChanged += OnSceneChildrenChanged;
        foreach (var child in scene.Children)
            TrackScene(child);
    }

    private void AddIdentifiables(Entity entity)
    {
        Identifiables[entity.Id] = entity;
        foreach (var component in entity.Components)
            Identifiables[component.Id] = component;
    }

    private void OnEntityAdded(object? sender, Entity entity)
    {
        AddIdentifiables(entity);
        pendingAdds[entity.Id] = pending.Count;
        pending.Add(new Op("add", entity));
    }

    private void OnEntityRemoved(object? sender, Entity entity)
    {
        Identifiables.Remove(entity.Id);
        foreach (var component in entity.Components)
            Identifiables.Remove(component.Id);

        // Added and removed in the same batch: neither is sent.
        if (pendingAdds.Remove(entity.Id, out var index))
        {
            pending[index] = pending[index] with { Kind = null };
            OpsCoalesced += 2;
            return;
        }
        pending.Add(new Op("remove", entity));
    }

    private void OnHierarchyChanged(object? sender, Entity entity)
    {
        // The add of this batch is written with the parent at the time of sending.
        if (pendingAdds.ContainsKey(entity.Id))
        {
            OpsCoalesced++;
            return;
        }
        pending.Add(new Op("parent", entity));
    }

    private void OnComponentChanged(object? sender, EntityComponentEventArgs e)
    {
        if (e.PreviousComponent is not null)
            Identifiables.Remove(e.PreviousComponent.Id);
        if (e.NewComponent is not null)
            Identifiables[e.NewComponent.Id] = e.NewComponent;
        if (pendingAdds.ContainsKey(e.Entity.Id))
        {
            OpsCoalesced++;
            return;
        }
        pending.Add(new Op("components", e.Entity));
    }

    private void OnSceneChildrenChanged(object? sender, TrackingCollectionChangedEventArgs e)
    {
        if (e.Item is not Scene scene)
            return;
        if (e.Action == NotifyCollectionChangedAction.Add)
        {
            TrackScene(scene);
            pending.Add(new Op("scene+", null, scene));
        }
        else if (e.Action == NotifyCollectionChangedAction.Remove)
        {
            pending.Add(new Op("scene-", null, scene));
        }
    }

    private readonly Dictionary<Guid, (string? Name, bool Enabled)> polled = [];

    private void PollNamesAndEnabled()
    {
        foreach (var identifiable in Identifiables.Values)
        {
            if (identifiable is not Entity entity)
                continue;
            var state = (entity.Name, IsEnabled(entity));
            if (polled.TryGetValue(entity.Id, out var previous) && previous != state && !pendingAdds.ContainsKey(entity.Id))
                pending.Add(new Op("state", entity));
            polled[entity.Id] = state;
        }
    }

    private void SendBatch(long frame)
    {
        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream))
        {
            json.WriteStartObject();
            json.WriteNumber("frame", frame);
            json.WriteStartArray("ops");
            foreach (var op in pending)
            {
                if (op.Kind is null)
                    continue;
                json.WriteStartObject();
                json.WriteString("op", op.Kind);
                if (op.Entity is { } entity)
                {
                    switch (op.Kind)
                    {
                        case "add":
                            WriteEntity(json, entity, withChildren: false);
                            break;
                        case "remove":
                            json.WriteString("id", entity.Id);
                            break;
                        case "parent":
                            json.WriteString("id", entity.Id);
                            WriteParent(json, entity);
                            break;
                        case "components":
                            json.WriteString("id", entity.Id);
                            WriteComponents(json, entity);
                            break;
                        case "state":
                            json.WriteString("id", entity.Id);
                            json.WriteString("name", entity.Name);
                            json.WriteBoolean("enabled", IsEnabled(entity));
                            break;
                    }
                }
                else if (op.Scene is { } scene)
                {
                    json.WriteString("id", scene.Id);
                    json.WriteString("parent", scene.Parent?.Id.ToString());
                    if (op.Kind == "scene+")
                        WriteScene(json, scene, "scene");
                }
                json.WriteEndObject();
                OpsSent++;
            }
            json.WriteEndArray();
            json.WriteEndObject();
        }
        pending.Clear();
        pendingAdds.Clear();
        BatchesSent++;
        send(PlayMessageType.TreeBatch, stream.ToArray());
    }

    private void SendTree()
    {
        var start = Stopwatch.GetTimestamp();
        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream))
        {
            json.WriteStartObject();
            json.WriteNumber("entities", sceneInstance!.Count);
            if (rootScene is not null)
                WriteScene(json, rootScene, "root");
            json.WriteNumber("buildMs", Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            json.WriteEndObject();
        }
        send(PlayMessageType.Tree, stream.ToArray());
    }

    private static void WriteScene(Utf8JsonWriter json, Scene scene, string propertyName)
    {
        json.WriteStartObject(propertyName);
        json.WriteString("id", scene.Id);
        json.WriteString("name", scene.Name);
        json.WriteString("url", AttachedReferenceManager.GetUrl(scene));
        json.WriteStartArray("entities");
        foreach (var entity in scene.Entities)
        {
            json.WriteStartObject();
            WriteEntity(json, entity, withChildren: true);
            json.WriteEndObject();
        }
        json.WriteEndArray();
        json.WriteStartArray("children");
        foreach (var child in scene.Children)
        {
            json.WriteStartObject();
            WriteScene(json, child, "scene");
            json.WriteEndObject();
        }
        json.WriteEndArray();
        json.WriteEndObject();
    }

    private static void WriteEntity(Utf8JsonWriter json, Entity entity, bool withChildren)
    {
        json.WriteString("id", entity.Id);
        json.WriteString("name", entity.Name);
        json.WriteBoolean("enabled", IsEnabled(entity));
        if (!withChildren)
        {
            WriteParent(json, entity);
            json.WriteString("scene", entity.Scene?.Id.ToString());
        }
        WriteComponents(json, entity);
        if (withChildren && entity.Transform is { Children.Count: > 0 } transform)
        {
            json.WriteStartArray("children");
            foreach (var child in transform.Children)
            {
                json.WriteStartObject();
                WriteEntity(json, child.Entity, withChildren: true);
                json.WriteEndObject();
            }
            json.WriteEndArray();
        }
    }

    private static void WriteParent(Utf8JsonWriter json, Entity entity)
        => json.WriteString("parent", entity.Transform?.Parent?.Entity?.Id.ToString());

    private static void WriteComponents(Utf8JsonWriter json, Entity entity)
    {
        json.WriteStartArray("components");
        foreach (var component in entity.Components)
        {
            json.WriteStartObject();
            json.WriteString("id", component.Id);
            json.WriteString("type", component.GetType().FullName);
            json.WriteEndObject();
        }
        json.WriteEndArray();
    }

    private static bool IsEnabled(Entity entity)
    {
        foreach (var component in entity.Components)
        {
            if (component is ActivableEntityComponent { Enabled: false })
                return false;
        }
        return true;
    }

    private readonly record struct Op(string? Kind, Entity? Entity, Scene? Scene = null);
}
