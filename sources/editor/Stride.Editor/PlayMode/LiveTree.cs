// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using Stride.PlayMode.Protocol;

namespace Stride.Editor.PlayMode;

/// <summary>
/// The tree of the running scenes and their entities, built from the agent's tree and batches, and its rows as a flat list
/// for a virtualized view: the rows of the expanded items, or those that lead to a match of the search.
/// </summary>
/// <remarks>
/// Used on the UI thread only. The rows are the same objects from one update to the next, so a view keeps its selection and
/// its scroll position; an item that goes leaves the rows.
/// </remarks>
[Experimental("STREXP012")]
public sealed class LiveTree
{
    private readonly Dictionary<int, LiveSceneItem> scenes = [];
    private readonly Dictionary<int, LiveEntityItem> entities = [];
    private readonly List<LiveSceneItem> roots = [];
    private readonly Func<PlaySceneInfo, IReadOnlyCollection<Guid>?>? sceneEntityIds;
    // The entity ids of the scene assets of the running scenes, with the scene whose asset holds each
    private readonly Dictionary<Guid, LiveSceneItem> assetEntities = [];
    // The live entity that is the counterpart of each of these ids, when there is one
    private readonly Dictionary<Guid, LiveEntityItem> counterparts = [];
    private bool deferRows;

    /// <summary>
    /// Initializes a new instance of the <see cref="LiveTree"/> class.
    /// </summary>
    /// <param name="sceneEntityIds">The ids of the entities of a running scene's asset, to tell which live entities have a
    /// counterpart there; <c>null</c> when the scene has no asset in the session. <c>null</c> to mark none as spawned.</param>
    public LiveTree(Func<PlaySceneInfo, IReadOnlyCollection<Guid>?>? sceneEntityIds = null)
    {
        this.sceneEntityIds = sceneEntityIds;
    }

    /// <summary>
    /// The rows to show: each shown item after its parent, children in the game's order.
    /// </summary>
    public ObservableCollection<LiveTreeItem> Rows { get; } = [];

    /// <summary>
    /// The root scenes (one, unless the game replaced its root scene and the tree was not sent again yet).
    /// </summary>
    public IReadOnlyList<LiveSceneItem> Roots => roots;

    /// <summary>
    /// The number of live entities.
    /// </summary>
    public int EntityCount => entities.Count;

    /// <summary>
    /// The search: the rows whose name contains it (any case), and those that lead to them; empty for all rows.
    /// </summary>
    public string Filter
    {
        get;
        set
        {
            value ??= string.Empty;
            if (field == value)
                return;
            field = value;
            RefreshRows();
        }
    } = string.Empty;

    /// <summary>
    /// Tells the entities the editor added to the game while it plays (created, duplicated, spawned from a prefab:
    /// <see cref="PlaySession.IsAddedWhilePlaying"/>) by their handle; <c>null</c> for none. Asked when an entity comes in the
    /// tree.
    /// </summary>
    public Func<int, bool>? IsAddedWhilePlaying { get; set; }

    /// <summary>
    /// Finds a live entity by its handle.
    /// </summary>
    public LiveEntityItem? FindEntity(int handle) => entities.GetValueOrDefault(handle);

    /// <summary>
    /// Finds a running scene by its handle.
    /// </summary>
    public LiveSceneItem? FindScene(int handle) => scenes.GetValueOrDefault(handle);

    /// <summary>
    /// Replaces the whole tree.
    /// </summary>
    /// <param name="tree">The tree the agent sent.</param>
    /// <returns>The handles of the entities that are gone.</returns>
    public IReadOnlyCollection<int> Reset(PlayTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);
        var before = new HashSet<int>(entities.Keys);
        deferRows = true;
        try
        {
            ClearItems();
            foreach (var scene in tree.Scenes)
                AddScene(scene);
            foreach (var entity in tree.Entities)
                AddEntity(entity);
        }
        finally
        {
            deferRows = false;
        }
        RefreshRows();
        before.ExceptWith(entities.Keys);
        return before;
    }

    /// <summary>
    /// Applies the changes of a frame.
    /// </summary>
    /// <param name="batch">The changes the agent sent.</param>
    /// <returns>The handles of the entities removed.</returns>
    public IReadOnlyCollection<int> Apply(PlayTreeBatch batch) => Apply([batch]);

    /// <summary>
    /// Applies the changes of several frames, in order, and updates the rows once.
    /// </summary>
    /// <param name="batches">The changes the agent sent.</param>
    /// <returns>The handles of the entities removed.</returns>
    public IReadOnlyCollection<int> Apply(IEnumerable<PlayTreeBatch> batches)
    {
        ArgumentNullException.ThrowIfNull(batches);
        var removed = new HashSet<int>();
        deferRows = true;
        try
        {
            foreach (var batch in batches)
            {
                foreach (var operation in batch.Operations)
                    Apply(operation, removed);
            }
        }
        finally
        {
            deferRows = false;
        }
        RefreshRows();
        return removed;
    }

    /// <summary>
    /// Removes every scene and entity.
    /// </summary>
    public void Clear()
    {
        ClearItems();
        RefreshRows();
    }

    /// <summary>
    /// Shows the children of every item.
    /// </summary>
    /// <param name="expanded">Whether to expand or collapse.</param>
    public void SetAllExpanded(bool expanded)
    {
        deferRows = true;
        try
        {
            foreach (var scene in scenes.Values)
                scene.IsExpanded = expanded || scene.Parent is null;
            foreach (var entity in entities.Values)
                entity.IsExpanded = expanded;
        }
        finally
        {
            deferRows = false;
        }
        RefreshRows();
    }

    /// <summary>
    /// Sorts items in the order of the tree: each after its parent and its previous siblings.
    /// </summary>
    /// <param name="items">Items of this tree.</param>
    public IReadOnlyList<T> InTreeOrder<T>(IEnumerable<T> items) where T : LiveTreeItem
    {
        ArgumentNullException.ThrowIfNull(items);
        var order = new Dictionary<LiveTreeItem, int>();
        var stack = new Stack<LiveTreeItem>(roots.AsEnumerable().Reverse());
        while (stack.TryPop(out var item))
        {
            order[item] = order.Count;
            for (var i = item.ChildList.Count - 1; i >= 0; --i)
                stack.Push(item.ChildList[i]);
        }
        return [.. items.OrderBy(x => order.GetValueOrDefault(x, int.MaxValue))];
    }

    internal void OnExpandedChanged()
    {
        if (!deferRows)
            RefreshRows();
    }

    private void Apply(PlayTreeOperation operation, HashSet<int> removed)
    {
        switch (operation)
        {
            case PlaySceneAdded added:
                AddScene(added.Scene);
                break;
            case PlaySceneRemoved sceneRemoved:
                if (scenes.TryGetValue(sceneRemoved.Handle, out var scene))
                    RemoveScene(scene, removed);
                break;
            case PlayEntityAdded added:
                // An entity added again after a removal in an earlier frame comes back with its handle
                removed.Remove(added.Entity.Handle);
                AddEntity(added.Entity, added.AfterHandle);
                break;
            case PlayEntityRemoved entityRemoved:
                if (entities.TryGetValue(entityRemoved.Handle, out var entity))
                    RemoveEntity(entity, removed);
                break;
            case PlayEntityMoved moved:
                if (entities.TryGetValue(moved.Handle, out var movedEntity))
                {
                    movedEntity.Parent?.RemoveChild(movedEntity);
                    movedEntity.SceneHandle = moved.SceneHandle;
                    AttachEntity(movedEntity, moved.ParentHandle, moved.SceneHandle, moved.AfterHandle);
                    SetSceneOfDescendants(movedEntity, moved.SceneHandle);
                }
                break;
            case PlayEntityComponentsChanged componentsChanged:
                if (entities.TryGetValue(componentsChanged.Handle, out var changed))
                {
                    changed.Components = componentsChanged.Components;
                    changed.ComponentTypeNames = [.. componentsChanged.Components.Select(x => x.TypeName)];
                }
                break;
            case PlayEntityStateChanged stateChanged:
                if (entities.TryGetValue(stateChanged.Handle, out var stated))
                {
                    stated.Name = stateChanged.Name ?? string.Empty;
                    stated.IsEnabled = stateChanged.Enabled;
                }
                break;
        }
    }

    private void AddScene(PlaySceneInfo info)
    {
        if (scenes.ContainsKey(info.Handle))
            return;
        // A scene is shown expanded, for its entities to show
        var scene = new LiveSceneItem(this, info) { IsExpanded = true };
        scenes.Add(info.Handle, scene);
        if (info.ParentHandle != 0 && scenes.TryGetValue(info.ParentHandle, out var parent))
            parent.AddChild(scene);
        else
            roots.Add(scene);

        if (sceneEntityIds?.Invoke(info) is { } ids)
        {
            foreach (var id in ids)
                assetEntities.TryAdd(id, scene);
        }
    }

    private void AddEntity(PlayEntityInfo info, int afterHandle = -1)
    {
        if (entities.ContainsKey(info.Handle))
            return;
        var entity = new LiveEntityItem(this, info) { IsAddedWhilePlaying = IsAddedWhilePlaying?.Invoke(info.Handle) == true };
        entities.Add(info.Handle, entity);
        // The first live entity with the id of an entity of a scene asset is its counterpart; the copies are spawned. An entity
        // the editor added has none, even a duplicate, which keeps the ids of its original
        if (!entity.IsAddedWhilePlaying && assetEntities.TryGetValue(info.Id, out var scene) && counterparts.TryAdd(info.Id, entity))
            entity.CounterpartScene = scene;
        AttachEntity(entity, info.ParentHandle, info.SceneHandle, afterHandle);
    }

    /// <summary>
    /// Puts the entity under its parent, or at the root of its scene, right after the sibling the game names (first among the
    /// entities for 0); last when the game does not name one, or names one the tree does not have there.
    /// </summary>
    private void AttachEntity(LiveEntityItem entity, int parentHandle, int sceneHandle, int afterHandle = -1)
    {
        LiveTreeItem? owner = parentHandle != 0 && entities.TryGetValue(parentHandle, out var parent) ? parent
            : scenes.TryGetValue(sceneHandle, out var scene) ? scene : null;
        // Otherwise it stays out of the rows until the tree is sent again
        if (owner is null)
            return;
        var children = owner.ChildList;
        var index = afterHandle switch
        {
            < 0 => children.Count,
            // First among the entities: child scenes of a scene keep their place
            0 => children.FindIndex(x => x is LiveEntityItem) is var first and >= 0 ? first : children.Count,
            _ => children.FindIndex(x => x is LiveEntityItem && x.Handle == afterHandle) is var previous and >= 0 ? previous + 1 : children.Count,
        };
        owner.InsertChild(index, entity);
    }

    private void RemoveEntity(LiveEntityItem entity, HashSet<int> removed)
    {
        // Its children go with it
        foreach (var child in entity.ChildList.OfType<LiveEntityItem>().ToList())
            RemoveEntity(child, removed);
        entity.Parent?.RemoveChild(entity);
        entities.Remove(entity.Handle);
        if (counterparts.TryGetValue(entity.Id, out var counterpart) && counterpart == entity)
            counterparts.Remove(entity.Id);
        removed.Add(entity.Handle);
    }

    private void RemoveScene(LiveSceneItem scene, HashSet<int> removed)
    {
        foreach (var child in scene.ChildList.ToList())
        {
            switch (child)
            {
                case LiveSceneItem childScene:
                    RemoveScene(childScene, removed);
                    break;
                case LiveEntityItem entity:
                    RemoveEntity(entity, removed);
                    break;
            }
        }
        if (scene.Parent is { } parent)
            parent.RemoveChild(scene);
        else
            roots.Remove(scene);
        scenes.Remove(scene.Handle);
        foreach (var id in assetEntities.Where(x => x.Value == scene).Select(x => x.Key).ToList())
            assetEntities.Remove(id);
        // The entities that moved out of the scene keep their counterpart in its asset, but it cannot be shown any more
        foreach (var entity in entities.Values.Where(x => x.CounterpartScene == scene))
            entity.CounterpartScene = null;
    }

    private void SetSceneOfDescendants(LiveEntityItem entity, int sceneHandle)
    {
        foreach (var child in entity.ChildList.OfType<LiveEntityItem>())
        {
            child.SceneHandle = sceneHandle;
            SetSceneOfDescendants(child, sceneHandle);
        }
    }

    private void ClearItems()
    {
        foreach (var item in scenes.Values.Cast<LiveTreeItem>().Concat(entities.Values))
        {
            item.ChildList.Clear();
            item.Parent = null;
        }
        scenes.Clear();
        entities.Clear();
        roots.Clear();
        assetEntities.Clear();
        counterparts.Clear();
    }

    /// <summary>
    /// Makes <see cref="Rows"/> the rows to show now, changing only what differs.
    /// </summary>
    private void RefreshRows()
    {
        if (deferRows)
            return;
        var filter = Filter;
        // The rows under a collapsed row are not visited: none of them matches any more
        if (filter.Length == 0)
        {
            foreach (var item in scenes.Values.Cast<LiveTreeItem>().Concat(entities.Values))
                item.IsMatch = false;
        }
        var rows = new List<LiveTreeItem>(Rows.Count + 16);
        foreach (var root in roots)
        {
            if (filter.Length == 0)
                AddRows(root, 0, rows);
            else
                AddFilteredRows(root, 0, filter, rows);
        }
        Patch(Rows, rows);
    }

    private static void AddRows(LiveTreeItem item, int depth, List<LiveTreeItem> rows)
    {
        item.Depth = depth;
        rows.Add(item);
        if (!item.IsExpanded)
            return;
        foreach (var child in item.ChildList)
            AddRows(child, depth + 1, rows);
    }

    /// <summary>
    /// Adds the rows of an item that matches the search or leads to a match.
    /// </summary>
    /// <returns>Whether it added any.</returns>
    private static bool AddFilteredRows(LiveTreeItem item, int depth, string filter, List<LiveTreeItem> rows)
    {
        var index = rows.Count;
        item.Depth = depth;
        item.IsMatch = item is LiveEntityItem && item.Name.Contains(filter, StringComparison.OrdinalIgnoreCase);
        rows.Add(item);
        var any = false;
        foreach (var child in item.ChildList)
            any |= AddFilteredRows(child, depth + 1, filter, rows);
        if (any || item.IsMatch)
            return true;
        rows.RemoveAt(index);
        return false;
    }

    /// <summary>
    /// Changes a collection into another list of the same items: removes, moves and inserts, so that the rows that stay are
    /// not removed.
    /// </summary>
    internal static void Patch<T>(ObservableCollection<T> target, IReadOnlyList<T> source) where T : class
    {
        var wanted = new HashSet<T>(source, ReferenceEqualityComparer.Instance);
        for (var i = target.Count - 1; i >= 0; --i)
        {
            if (!wanted.Contains(target[i]))
                target.RemoveAt(i);
        }
        for (var i = 0; i < source.Count; ++i)
        {
            if (i < target.Count && ReferenceEquals(target[i], source[i]))
                continue;
            var index = -1;
            for (var j = i + 1; j < target.Count; ++j)
            {
                if (ReferenceEquals(target[j], source[i]))
                {
                    index = j;
                    break;
                }
            }
            if (index >= 0)
                target.Move(index, i);
            else
                target.Insert(i, source[i]);
        }
    }
}
