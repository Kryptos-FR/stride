// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using Stride.Core.Assets;
using Stride.Core.Quantum;
using Stride.Core.Reflection;
using Stride.Core.Serialization;
using Stride.Core.Yaml;
using Stride.Editor.PlayMode;
using Stride.Engine;
using Stride.PlayMode.Snapshots;

namespace Stride.Assets.Editor.PlayMode;

/// <summary>
/// What applying a snapshot of an entity to the grid's mirror did.
/// </summary>
/// <param name="StructureChanged">Whether the structure differs (a component added or removed, an object replaced by one of
/// another type, a placeholder that changed): nothing was written, the grid is to be rebuilt.</param>
/// <param name="ComparedValues">The values compared.</param>
/// <param name="WrittenValues">The values written.</param>
/// <param name="SkippedValues">The changed values not written because they are protected (under the pointer or with the
/// focus).</param>
/// <param name="StructuralChange">What changed in the structure, for the log.</param>
/// <param name="AddedItems">The items added to a list or a dictionary in place.</param>
/// <param name="RemovedItems">The items removed from a list or a dictionary in place.</param>
[Experimental("STREXP012")]
public sealed record LiveApplyResult(bool StructureChanged, int ComparedValues, int WrittenValues, int SkippedValues, string? StructuralChange, int AddedItems = 0, int RemovedItems = 0)
{
    /// <summary>
    /// Whether anything was written.
    /// </summary>
    public bool HasChanges => WrittenValues + AddedItems + RemovedItems > 0;
}

/// <summary>
/// Compares a new snapshot of a live entity with the mirror the grid shows, through the mirror's Quantum nodes, and writes the
/// values that changed through those nodes, so that the node presenters and the controls follow.
/// </summary>
/// <remarks>
/// <para>
/// References to objects outside of the mirror (stand-ins of other entities and their components) are the same when they stand
/// for the same live object. Content is the same when it has the same URL; content made at run time (no URL) is the same when it
/// has the same type, since its data is not shown.
/// </para>
/// <para>
/// A difference of structure (a component added or removed, an object replaced by one of another type, a placeholder that
/// changed) writes nothing and is reported, so that the grid is rebuilt on the new mirror. A list or a dictionary that gained
/// or lost items is changed in place when <see cref="InPlaceCollections"/> is set.
/// </para>
/// </remarks>
[Experimental("STREXP012")]
public sealed class LiveMirrorApplier
{
    private readonly List<(IGraphNode Node, NodeIndex Index, object? Value)> writes = [];
    // Items added to and removed from a list or a dictionary, applied after the writes, the removals from the last index down
    private readonly List<(IObjectNode Node, NodeIndex Index, object? Value)> additions = [];
    private readonly List<(IObjectNode Node, NodeIndex Index, object? Value)> removals = [];
    private readonly HashSet<object> visited = new(ReferenceEqualityComparer.Instance);
    private int compared;
    private int skipped;

    /// <summary>
    /// The value nodes not to write (the one under the pointer, the one with the focus): a later snapshot writes them once they
    /// are released, since the mirror still differs.
    /// </summary>
    public Func<IGraphNode, NodeIndex, bool>? IsProtected { get; set; }

    /// <summary>
    /// Finds the asset of the session that has a content URL (decision PM4 A: the editor maps the URLs of the game's content to
    /// its assets); <c>null</c> keeps the content as the game sent it.
    /// </summary>
    public Func<string, AssetId?>? FindAsset { get; set; }

    /// <summary>
    /// Whether a list or a dictionary that gained or lost items is changed in place, through its node, instead of being
    /// reported for a rebuild. The components of the entity are always reported. Set by default.
    /// </summary>
    public bool InPlaceCollections { get; set; } = true;

    /// <summary>
    /// Compares <paramref name="incoming"/> with <paramref name="current"/>, the mirror the grid shows, whose root node is
    /// <paramref name="mirrorRoot"/>, and, when the structure is the same, writes the changed values into the mirror.
    /// </summary>
    /// <param name="mirrorRoot">The node of <paramref name="current"/>'s entity.</param>
    /// <param name="current">The mirror the grid shows, with the stand-ins of the references written into it since it was built.</param>
    /// <param name="incoming">The new snapshot of the same entity.</param>
    public LiveApplyResult Apply(IObjectNode mirrorRoot, LiveMirrorReferences current, LiveEntityMirror incoming)
    {
        ArgumentNullException.ThrowIfNull(mirrorRoot);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(incoming);
        writes.Clear();
        additions.Clear();
        removals.Clear();
        visited.Clear();
        compared = 0;
        skipped = 0;
        try
        {
            var mirror = (Entity)mirrorRoot.Retrieve()!;
            var references = new References(mirror, incoming.Entity, current, incoming);
            var structural = CompareObject(mirrorRoot, mirror, incoming.Entity, references);
            if (structural is not null)
                return new LiveApplyResult(true, compared, 0, 0, structural);

            foreach (var (node, index, value) in writes)
            {
                ResolveContent(value);
                current.Learn(value, incoming);
                if (node is IMemberNode member)
                    member.Update(value);
                else
                    ((IObjectNode)node).Update(value, index);
            }
            foreach (var (node, index, value) in removals.OrderByDescending(x => x.Index))
                node.Remove(value, index);
            foreach (var (node, index, value) in additions)
            {
                ResolveContentDeep(value);
                current.Learn(value, incoming);
                node.Add(value, index);
            }
            return new LiveApplyResult(false, compared, writes.Count, skipped, null, additions.Count, removals.Count);
        }
        finally
        {
            writes.Clear();
            additions.Clear();
            removals.Clear();
            visited.Clear();
        }
    }

    /// <summary>
    /// Maps the content of a mirror to the session's assets (see <see cref="FindAsset"/>), before the grid is built on it.
    /// </summary>
    public void ResolveContent(LiveEntityMirror mirror)
    {
        ArgumentNullException.ThrowIfNull(mirror);
        visited.Clear();
        try
        {
            foreach (var component in mirror.Entity.Components)
                ResolveContentDeep(component);
        }
        finally
        {
            visited.Clear();
        }
    }

    private void ResolveContent(object? value)
    {
        if (FindAsset is null || AttachedReferenceManager.GetAttachedReference(value) is not { } reference)
            return;
        if (reference.Id == AssetId.Empty && !string.IsNullOrEmpty(reference.Url) && FindAsset(reference.Url) is { } id)
            reference.Id = id;
    }

    private void ResolveContentDeep(object? value)
    {
        if (FindAsset is null || value is null || value.GetType().IsPrimitive || value is string)
            return;
        if (AttachedReferenceManager.GetAttachedReference(value) is not null)
        {
            ResolveContent(value);
            return;
        }
        // Stand-ins of other entities are light: their members are not followed. The mirror's own entity is reached
        // through its components.
        if (value is Entity || !visited.Add(value))
            return;
        var descriptor = TypeDescriptorFactory.Default.Find(value.GetType());
        if (descriptor is ObjectDescriptor objectDescriptor)
        {
            foreach (var member in objectDescriptor.Members)
            {
                if (member.HasSet || member.Type.IsClass || member.Type.IsInterface)
                    ResolveContentDeep(SafeGet(member, value));
            }
        }
        if (descriptor is CollectionBaseDescriptor collection)
        {
            foreach (var key in collection.EnumerateKeys(value).ToList())
                ResolveContentDeep(collection.GetValue(value, key));
        }
    }

    private static object? SafeGet(IMemberDescriptor member, object instance)
    {
        try
        {
            return member.Get(instance);
        }
        catch (Exception)
        {
            // A getter that fails on a mirror (it needs the game): nothing to resolve there
            return null;
        }
    }

    private string? CompareObject(IObjectNode node, object mirror, object incoming, References references)
    {
        if (mirror is IUnloadable || incoming is IUnloadable)
            return ComparePlaceholders(mirror, incoming);
        if (mirror.GetType() != incoming.GetType())
            return $"{node.Type.Name}: {mirror.GetType().Name} became {incoming.GetType().Name}";
        // An object of the mirror referenced twice (a component that references another component of the entity) is compared once
        if (!mirror.GetType().IsValueType && !visited.Add(mirror))
            return null;

        foreach (var member in node.Members)
        {
            var newValue = member.MemberDescriptor.Get(incoming);
            var oldValue = member.Retrieve();
            var change = CompareValue(member, NodeIndex.Empty, member.Target, oldValue, newValue, references);
            if (change is not null)
                return $"{member.Name}.{change}";
        }

        if (node.IsEnumerable && TypeDescriptorFactory.Default.Find(incoming.GetType()) is CollectionBaseDescriptor collection)
        {
            var mirrorKeys = node.Indices.ToList();
            var incomingKeys = collection.EnumerateKeys(incoming).Select(x => new NodeIndex(x)).ToList();
            if (!mirrorKeys.SequenceEqual(incomingKeys))
            {
                if (!InPlaceCollections || incoming is EntityComponentCollection)
                {
                    return mirrorKeys.Count != incomingKeys.Count
                        ? $"[count {mirrorKeys.Count} became {incomingKeys.Count}]"
                        : $"[keys {string.Join(",", mirrorKeys)} became {string.Join(",", incomingKeys)}]";
                }
                // The common keys are compared below; the others are added or removed
                var incomingSet = incomingKeys.ToHashSet();
                var mirrorSet = mirrorKeys.ToHashSet();
                foreach (var key in mirrorKeys.Where(x => !incomingSet.Contains(x)))
                    removals.Add((node, key, node.Retrieve(key)));
                foreach (var key in incomingKeys.Where(x => !mirrorSet.Contains(x)))
                    additions.Add((node, key, collection.GetValue(incoming, key.Value!)));
                mirrorKeys = mirrorKeys.Where(incomingSet.Contains).ToList();
            }
            foreach (var index in mirrorKeys)
            {
                var oldItem = node.Retrieve(index);
                var newItem = collection.GetValue(incoming, index.Value!);
                var change = CompareValue(node, index, node.ItemReferences is null ? null : node.IndexedTarget(index), oldItem, newItem, references);
                if (change is not null)
                    return $"[{index}]{change}";
            }
        }
        return null;
    }

    private static string? ComparePlaceholders(object mirror, object incoming)
    {
        // A placeholder shows the type and why it could not be read: any change of these, or a component read at last (the
        // game's code reloaded), is a new header
        if (mirror is IUnloadable oldPlaceholder && incoming is IUnloadable newPlaceholder
            && oldPlaceholder.TypeName == newPlaceholder.TypeName
            && oldPlaceholder.AssemblyName == newPlaceholder.AssemblyName
            && oldPlaceholder.Error == newPlaceholder.Error)
        {
            return null;
        }
        return $" (placeholder of {(mirror as IUnloadable)?.TypeName ?? (incoming as IUnloadable)?.TypeName} changed)";
    }

    private string? CompareValue(IGraphNode owner, NodeIndex index, IObjectNode? target, object? oldValue, object? newValue, References references)
    {
        ++compared;
        if (oldValue is null && newValue is null)
            return null;

        // A reference to an object outside of the mirror (another entity or one of its components): the same if it stands for
        // the same live object
        if (references.IsOutside(oldValue) || references.IsOutside(newValue))
        {
            if (!Equals(references.KeyOf(oldValue), references.KeyOf(newValue)))
                Write(owner, index, newValue);
            return null;
        }

        if (target is not null && oldValue is not null && newValue is not null)
            return CompareObject(target, oldValue, newValue, references);

        if (target is not null || oldValue is null != newValue is null && !IsLeaf(owner, index))
            return $" (an object {(oldValue is null ? "appeared" : "disappeared")})";

        if (!LeafEquals(oldValue, newValue))
            Write(owner, index, newValue);
        return null;
    }

    private static bool IsLeaf(IGraphNode owner, NodeIndex index)
    {
        // A member or item without a target node holds a primitive (a value, a string, content)
        return owner switch
        {
            IMemberNode member => member.Target is null,
            IObjectNode node => node.ItemReferences is null || node.IndexedTarget(index) is null,
            _ => true,
        };
    }

    private static bool LeafEquals(object? oldValue, object? newValue)
    {
        // Content travels by URL: each snapshot makes new proxies of the same content
        var oldReference = AttachedReferenceManager.GetAttachedReference(oldValue);
        var newReference = AttachedReferenceManager.GetAttachedReference(newValue);
        if (oldReference is not null || newReference is not null)
            return oldReference?.Url == newReference?.Url;
        // Content made at run time: each snapshot makes a new object, whose data the grid does not show
        if (oldValue is not null && newValue is not null && oldValue.GetType() == newValue.GetType() && AssetRegistry.IsExactContentType(oldValue.GetType()))
            return true;
        return Equals(oldValue, newValue);
    }

    private void Write(IGraphNode node, NodeIndex index, object? value)
    {
        if (IsProtected?.Invoke(node, index) == true)
        {
            ++skipped;
            return;
        }
        writes.Add((node, index, value));
    }

    /// <summary>
    /// Tells the objects of the mirror (its entity and components) from those outside of it, in the mirror and in the incoming
    /// snapshot, and what live object each one outside stands for.
    /// </summary>
    private sealed class References(Entity mirror, Entity incoming, LiveMirrorReferences current, LiveEntityMirror incomingMirror)
    {
        public bool IsOutside(object? value)
        {
            return value switch
            {
                Entity entity => entity != mirror && entity != incoming,
                EntityComponent component => component.Entity != mirror && component.Entity != incoming,
                _ => false,
            };
        }

        public object? KeyOf(object? value)
        {
            if (value is null)
                return null;
            if (value == mirror || value == incoming)
                return "self";
            var reference = current.Find(value) ?? (incomingMirror.StandIns.TryGetValue(value, out var found) ? found : null);
            if (reference is { } known)
                return (known.Kind, known.EntityHandle, known.EntityId, known.ComponentId);
            // A stand-in the reader could not tell: compare what it says of itself
            return value switch
            {
                Entity entity => (entity.Id, entity.Name),
                EntityComponent component => (component.GetType(), component.Id),
                _ => value,
            };
        }
    }
}

/// <summary>
/// The stand-ins a mirror shown in the grid refers to, with the live object each stands for: those of the snapshot the grid was
/// built on, and those written into it since.
/// </summary>
[Experimental("STREXP012")]
public sealed class LiveMirrorReferences
{
    private readonly Dictionary<object, PlayMirrorReference> references = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Initializes a new instance of the <see cref="LiveMirrorReferences"/> class, with the stand-ins of the mirror the grid is
    /// built on.
    /// </summary>
    public LiveMirrorReferences(LiveEntityMirror mirror)
    {
        ArgumentNullException.ThrowIfNull(mirror);
        Mirror = mirror;
        foreach (var (standIn, reference) in mirror.StandIns)
            references[standIn] = reference;
    }

    /// <summary>
    /// The mirror the grid was built on.
    /// </summary>
    public LiveEntityMirror Mirror { get; }

    /// <summary>
    /// What a stand-in of the mirror stands for, or <c>null</c>.
    /// </summary>
    public PlayMirrorReference? Find(object standIn) => references.TryGetValue(standIn, out var reference) ? reference : null;

    /// <summary>
    /// Keeps what a stand-in written into the mirror stands for.
    /// </summary>
    internal void Learn(object? value, LiveEntityMirror incoming)
    {
        if (value is not null && incoming.StandIns.TryGetValue(value, out var reference))
        {
            references[value] = reference;
            // A component's stand-in sits on its entity's stand-in
            if (value is EntityComponent { Entity: { } owner } && incoming.StandIns.TryGetValue(owner, out var ownerReference))
                references[owner] = ownerReference;
        }
    }
}
