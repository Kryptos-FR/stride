// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core.Quantum;
using Stride.Core.Reflection;
using Stride.Core.Serialization;
using Stride.Engine;

namespace Stride.GameStudio.Avalonia.PlayModeSpike;

/// <summary>
/// What applying a snapshot to the mirror did.
/// </summary>
public sealed record MirrorApplyResult(bool StructureChanged, int ComparedValues, int WrittenValues, int SkippedValues, string? StructuralChange);

/// <summary>
/// Play mode spike: compares the objects of a new snapshot with the mirror, through the mirror's Quantum nodes, and writes the
/// leaf values that changed through those nodes, so that the node presenters and the controls follow.
/// </summary>
/// <remarks>
/// A difference of structure (a component added or removed, a collection or dictionary that changed its keys, an object
/// replaced by one of another type) writes nothing and is reported, so that the grid is rebuilt on the new objects.
/// </remarks>
public sealed class MirrorApplier
{
    private readonly List<(IGraphNode Node, NodeIndex Index, object? Value)> writes = [];
    private readonly HashSet<object> visited = new(ReferenceEqualityComparer.Instance);
    private int compared;
    private int skipped;

    /// <summary>
    /// The value nodes not to write (the one the user has the focus on, or the pointer over): the next snapshot after they are
    /// released writes them.
    /// </summary>
    public Func<IGraphNode, NodeIndex, bool>? IsProtected { get; set; }

    /// <summary>
    /// Compares <paramref name="incoming"/> with the mirror whose root node is <paramref name="mirrorRoot"/> and, when the structure
    /// is the same, writes the changed values.
    /// </summary>
    public MirrorApplyResult Apply(IObjectNode mirrorRoot, Entity incoming)
    {
        ArgumentNullException.ThrowIfNull(mirrorRoot);
        ArgumentNullException.ThrowIfNull(incoming);
        writes.Clear();
        visited.Clear();
        compared = 0;
        skipped = 0;
        var mirror = (Entity)mirrorRoot.Retrieve()!;
        var structural = CompareObject(mirrorRoot, mirror, incoming, new MirrorReferences(mirror, incoming));
        if (structural is not null)
        {
            writes.Clear();
            return new MirrorApplyResult(true, compared, 0, 0, structural);
        }

        foreach (var (node, index, value) in writes)
        {
            if (node is IMemberNode member)
                member.Update(value);
            else
                ((IObjectNode)node).Update(value, index);
        }
        var count = writes.Count;
        writes.Clear();
        return new MirrorApplyResult(false, compared, count, skipped, null);
    }

    private string? CompareObject(IObjectNode node, object mirror, object incoming, MirrorReferences references)
    {
        if (mirror.GetType() != incoming.GetType())
            return $"{node.Type.Name}: {mirror.GetType().Name} became {incoming.GetType().Name}";
        // An object of the mirror referenced twice (a component that references another component of the entity) is compared once
        if (!mirror.GetType().IsValueType && !visited.Add(mirror))
            return null;

        foreach (var member in node.Members)
        {
            var descriptor = member.MemberDescriptor;
            var newValue = descriptor.Get(incoming);
            var oldValue = member.Retrieve();
            var change = CompareValue(member, NodeIndex.Empty, member.Target, oldValue, newValue, references);
            if (change is not null)
                return $"{member.Name}.{change}";
        }

        if (node.IsEnumerable && TypeDescriptorFactory.Default.Find(incoming.GetType()) is CollectionBaseDescriptor collection)
        {
            var mirrorKeys = node.Indices.ToList();
            var incomingKeys = collection.EnumerateKeys(incoming).Select(x => new NodeIndex(x)).ToList();
            if (mirrorKeys.Count != incomingKeys.Count)
                return $"[count {mirrorKeys.Count} became {incomingKeys.Count}]";
            for (var i = 0; i < mirrorKeys.Count; i++)
            {
                if (mirrorKeys[i] != incomingKeys[i])
                    return $"[key {mirrorKeys[i]} became {incomingKeys[i]}]";
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

    private string? CompareValue(IGraphNode owner, NodeIndex index, IObjectNode? target, object? oldValue, object? newValue, MirrorReferences references)
    {
        ++compared;
        if (oldValue is null && newValue is null)
            return null;

        // A reference to an object outside of the mirror (another entity or its component): same object if same id
        if (references.IsOutside(oldValue) || references.IsOutside(newValue))
        {
            if (!Equals(references.KeyOf(oldValue), references.KeyOf(newValue)))
                Write(owner, index, newValue);
            return null;
        }

        if (target is not null && oldValue is not null && newValue is not null)
            return CompareObject(target, oldValue, newValue, references);

        if (target is not null || oldValue is null != newValue is null && !IsLeafType(owner, index))
            return $" (an object {(oldValue is null ? "appeared" : "disappeared")})";

        if (!LeafEquals(oldValue, newValue))
            Write(owner, index, newValue);
        return null;
    }

    private static bool IsLeafType(IGraphNode owner, NodeIndex index)
    {
        // A member or item without target node holds a primitive (a value, a string, content by URL)
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
            return oldReference?.Url == newReference?.Url && oldReference?.Id == newReference?.Id;
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
    /// snapshot.
    /// </summary>
    private sealed class MirrorReferences(Entity mirror, Entity incoming)
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
            return value switch
            {
                Entity entity => entity == mirror || entity == incoming ? "self" : entity.Id,
                EntityComponent component => (component.GetType(), component.Id),
                _ => value,
            };
        }
    }
}
