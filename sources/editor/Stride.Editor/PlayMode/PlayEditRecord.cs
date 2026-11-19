// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using Stride.PlayMode.Protocol;

namespace Stride.Editor.PlayMode;

/// <summary>
/// An edit the game applied, as <see cref="PlayEditRecord"/> keeps it.
/// </summary>
/// <param name="EntityHandle">The agent's handle of the entity.</param>
/// <param name="ComponentId">The component's id; <see cref="Guid.Empty"/> for a member of the entity itself.</param>
/// <param name="Path">The path of the member set, or of the collection whose items were added, removed or moved
/// (<see cref="PlayPathSegment.Format"/>).</param>
/// <param name="Operation">What the edit did.</param>
[Experimental("STREXP012")]
public sealed record PlayEditRecordEntry(int EntityHandle, Guid ComponentId, string Path, PlayEditOperation Operation)
{
    /// <summary>
    /// Whether the edit added, removed or moved items of the collection at <see cref="Path"/>.
    /// </summary>
    public bool IsCollectionChange => Operation != PlayEditOperation.Set;
}

/// <summary>
/// What the user changed in the running game, for the life of a play session: each edit of a value the game applied, and each
/// component added or removed from the editor. Apply to asset tells "edited live" from "changed by the game" with it.
/// </summary>
/// <remarks>
/// <para>
/// An edit is kept once the game answers it <see cref="PlayEditStatus.Applied"/> or <see cref="PlayEditStatus.Coalesced"/> (a
/// later set of the same member in the same frame replaced it): a refused edit, one cut short by a stop, or one still on its
/// way is not kept. A set keeps the path of its member; an item added, inserted, removed or moved keeps the path of its
/// collection.
/// </para>
/// <para>
/// The session fills it on its reading task; it can be read from any thread.
/// </para>
/// </remarks>
[Experimental("STREXP012")]
public sealed class PlayEditRecord
{
    private readonly object @lock = new();
    private readonly List<PlayEditRecordEntry> entries = [];
    private readonly HashSet<(int Handle, Guid ComponentId)> addedComponents = [];
    private readonly HashSet<(int Handle, Guid ComponentId)> removedComponents = [];

    /// <summary>
    /// Raised after an edit or a component is recorded, on the thread that recorded it.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// The number of edits kept.
    /// </summary>
    public int Count
    {
        get
        {
            lock (@lock)
                return entries.Count;
        }
    }

    /// <summary>
    /// The edits kept of an entity, in the order the game applied them.
    /// </summary>
    public IReadOnlyList<PlayEditRecordEntry> GetEdits(int entityHandle)
    {
        lock (@lock)
            return entries.Where(x => x.EntityHandle == entityHandle).ToList();
    }

    /// <summary>
    /// Whether the user changed anything of an entity: a value, an item, or a component added or removed.
    /// </summary>
    public bool HasEdits(int entityHandle)
    {
        lock (@lock)
            return entries.Any(x => x.EntityHandle == entityHandle) || addedComponents.Any(x => x.Handle == entityHandle) || removedComponents.Any(x => x.Handle == entityHandle);
    }

    /// <summary>
    /// Whether the user changed the member at <paramref name="path"/>, something inside it, or an object that holds it (a new
    /// instance set in place of its owner, an item of a collection added or moved).
    /// </summary>
    /// <param name="entityHandle">The agent's handle of the entity.</param>
    /// <param name="componentId">The component's id; <see cref="Guid.Empty"/> for a member of the entity itself.</param>
    /// <param name="path">The member's path (<see cref="PlayPathSegment.Format"/>).</param>
    public bool IsEdited(int entityHandle, Guid componentId, string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        lock (@lock)
        {
            if (addedComponents.Contains((entityHandle, componentId)))
                return true;
            return entries.Any(x => x.EntityHandle == entityHandle && x.ComponentId == componentId && (IsSameOrInside(x.Path, path) || IsSameOrInside(path, x.Path)));
        }
    }

    /// <summary>
    /// Whether the user added, removed or moved items of the collection at <paramref name="path"/>, or set the collection itself:
    /// its items may no longer be at the positions they have in the asset.
    /// </summary>
    public bool HasCollectionChange(int entityHandle, Guid componentId, string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        lock (@lock)
        {
            return entries.Any(x => x.EntityHandle == entityHandle && x.ComponentId == componentId
                && (x.IsCollectionChange && x.Path == path || x.Operation == PlayEditOperation.Set && IsSameOrInside(path, x.Path)));
        }
    }

    /// <summary>
    /// Whether the user added this component to the entity while the game runs.
    /// </summary>
    public bool IsComponentAdded(int entityHandle, Guid componentId)
    {
        lock (@lock)
            return addedComponents.Contains((entityHandle, componentId));
    }

    /// <summary>
    /// Whether the user removed this component from the entity while the game runs.
    /// </summary>
    public bool IsComponentRemoved(int entityHandle, Guid componentId)
    {
        lock (@lock)
            return removedComponents.Contains((entityHandle, componentId));
    }

    /// <summary>
    /// Keeps an edit the game applied.
    /// </summary>
    public void Add(PlayEdit edit)
    {
        ArgumentNullException.ThrowIfNull(edit);
        var path = edit.Operation is PlayEditOperation.Set or PlayEditOperation.Add ? edit.Path : edit.Path.Take(edit.Path.Count - 1);
        var entry = new PlayEditRecordEntry(edit.EntityHandle, edit.ComponentId, PlayPathSegment.Format(path), edit.Operation);
        lock (@lock)
            entries.Add(entry);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Keeps a change of components the game applied: a component added (its id is in the result) or removed.
    /// </summary>
    public void Add(PlayStructureEdit edit, PlayStructureResult result)
    {
        ArgumentNullException.ThrowIfNull(edit);
        ArgumentNullException.ThrowIfNull(result);
        if (result.Status != PlayEditStatus.Applied)
            return;
        lock (@lock)
        {
            switch (edit.Operation)
            {
                case PlayStructureOperation.AddComponent:
                    addedComponents.Add((edit.EntityHandle, result.ComponentId));
                    break;
                case PlayStructureOperation.RemoveComponent:
                    removedComponents.Add((edit.EntityHandle, edit.ComponentId));
                    break;
                default:
                    return;
            }
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Whether <paramref name="path"/> is <paramref name="owner"/> or a path inside it (<c>Position.X</c> in <c>Position</c>,
    /// <c>Values[2]</c> in <c>Values</c>); an empty owner holds every path.
    /// </summary>
    private static bool IsSameOrInside(string path, string owner)
    {
        if (owner.Length == 0 || path == owner)
            return true;
        return path.Length > owner.Length && path.StartsWith(owner, StringComparison.Ordinal) && path[owner.Length] is '.' or '[';
    }
}
