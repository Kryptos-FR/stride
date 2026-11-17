// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core;
using Stride.Core.Serialization;
using Stride.Engine;

namespace Stride.PlayMode.Snapshots;

/// <summary>
/// The state of one component's write or read, which the serializers of the "PlayMirror" profile share through the
/// serialization context.
/// </summary>
internal sealed class PlayMirrorContext
{
    public static readonly PropertyKey<PlayMirrorContext> Key = new("PlayMirrorContext", typeof(PlayMirrorContext));

    private readonly Dictionary<object, int> indices = new(ReferenceEqualityComparer.Instance);
    private readonly Func<Entity, int>? handleOf;
    private readonly Func<PlayMirrorReference, object?>? resolve;
    private object?[] resolved = [];

    private PlayMirrorContext(SerializerSelector inner, object? root, Func<Entity, int>? handleOf, Func<PlayMirrorReference, object?>? resolve, List<PlayMirrorReference> references)
    {
        Inner = inner;
        Root = root;
        this.handleOf = handleOf;
        this.resolve = resolve;
        References = references;
    }

    /// <summary>
    /// The selector of the root component's own serializers (the default profile), whose members are serialized with the
    /// "PlayMirror" selector.
    /// </summary>
    public SerializerSelector Inner { get; }

    /// <summary>
    /// The component written or read in full. On read, the instance to read into, when the reader has one.
    /// </summary>
    public object? Root { get; set; }

    /// <summary>
    /// Whether the root's serialization has started: any later meeting of the root is a reference to it.
    /// </summary>
    public bool RootStarted { get; set; }

    /// <summary>
    /// Set when a reference was written or read in place of an object, so that the serialization that follows does nothing.
    /// </summary>
    public bool SkipNextSerialize { get; set; }

    /// <summary>
    /// The descriptors of the objects outside the root, by index.
    /// </summary>
    public List<PlayMirrorReference> References { get; }

    public static PlayMirrorContext Of(SerializationStream stream)
        => stream.Context.Get(Key) ?? throw new InvalidOperationException("A serializer of the PlayMirror profile runs outside of a snapshot.");

    public static PlayMirrorContext ForWrite(SerializerSelector inner, EntityComponent root, Func<Entity, int> handleOf)
        => new(inner, root, handleOf, null, []);

    public static PlayMirrorContext ForRead(SerializerSelector inner, EntityComponent? root, IReadOnlyList<PlayMirrorReference> references, Func<PlayMirrorReference, object?> resolve)
        => new(inner, root, null, resolve, [.. references]) { resolved = new object?[references.Count] };

    /// <summary>
    /// Gives the index of the descriptor of an object outside the root, adding it the first time.
    /// </summary>
    public int AddReference(object value)
    {
        if (indices.TryGetValue(value, out var index))
            return index;
        index = References.Count;
        References.Add(Describe(value));
        indices.Add(value, index);
        return index;
    }

    /// <summary>
    /// Gives the object a descriptor stands for on the reader's side.
    /// </summary>
    public object? GetReference(int index)
    {
        if ((uint)index >= (uint)References.Count)
            throw new InvalidOperationException($"A snapshot refers to object {index} of {References.Count}.");
        return resolved[index] ??= resolve!(References[index]);
    }

    private PlayMirrorReference Describe(object value)
    {
        switch (value)
        {
            case Entity entity:
                return new PlayMirrorReference(PlayMirrorReferenceKind.Entity, handleOf!(entity), entity.Id, entity.Name, Guid.Empty, null);
            case EntityComponent component:
                var owner = component.Entity;
                return new PlayMirrorReference(PlayMirrorReferenceKind.Component, owner is null ? 0 : handleOf!(owner), owner?.Id ?? Guid.Empty, owner?.Name, component.Id, PlayMirrorSerializer.TypeNameOf(component.GetType()));
            default:
                throw new InvalidOperationException($"{value.GetType().FullName} cannot be written as a reference.");
        }
    }
}
